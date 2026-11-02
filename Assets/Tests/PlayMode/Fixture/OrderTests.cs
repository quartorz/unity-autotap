#if ENABLE_AUTOTAP
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.TestTools;
using EndReason = UnityAutoTap.AutoTapBase.EndReason;
using LogEventKind = UnityAutoTap.AutoTapBase.LogEventKind;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// The order of what is processed, and what is dropped. Experiments O1, O3, O4 and O5, and the order of
	/// <c>GetLatestLogs</c>.
	/// </summary>
	/// <remarks>
	/// Events are processed in the order they were queued, across devices; <c>InputState.onChange</c> is called right
	/// after a state was written, once per processed event for most devices (InputManager.cs, 3192-3196 and
	/// 3969-4068). That is what the numbering of the changes (<c>InputChange.Order</c>) is built on.
	/// </remarks>
	public class OrderTests : AutoTapFixture
	{
		static string Label(ChangeRecord change)
		{
			if (change.Is<TouchBegan>())
			{
				return "touchBegan";
			}

			if (change.Is<Scrolled>())
			{
				return "scrolled";
			}

			if (change.Is<ButtonDown>() && change.ControlPath == "a")
			{
				return "keyDown";
			}

			if (change.Is<ButtonUp>() && change.ControlPath == "a")
			{
				return "keyUp";
			}

			return null;
		}

		static string Kind(InputDevice device)
		{
			return device is Keyboard ? "Keyboard" : device is Touchscreen ? "Touchscreen" : device is Mouse ? "Mouse" : device.GetType().Name;
		}

		[UnityTest]
		public IEnumerator O1_KeyFingerScrollKey_QueuedInOneFrame_AreProcessedInThatOrder()
		{
			// O1. Four events for three devices, queued in one frame. The Input System processes them in the order of
			// the buffer, so InputState.onChange sees Keyboard, Touchscreen, Mouse, Keyboard (InputManager.cs,
			// 3192-3196). A Touchscreen calls onChange more than once for one event (Touchscreen.cs, 748-849), but the
			// reader reports the touch once.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			AcquireSession();
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			var raw = new RawChangeLog();
			OnTearDown(raw.Dispose);

			var keyboard = Session.GetDevice("Keyboard");
			var mouse = Session.GetPointer("Mouse");
			VirtualFinger finger = null;
			yield return Step(() =>
			{
				finger = Session.NewFinger();
				finger.Press(Rig.EmptyPoint);
				finger.Release();
			});
			yield return Steps(2);
			changes.Items.Clear();
			raw.Items.Clear();

			var fireFrame = 0;
			yield return Step(() =>
			{
				fireFrame = Frame;
				keyboard.Set("a", 1f);
				finger = Session.NewFinger();
				finger.Press(Rig.Center(button.gameObject));
				mouse.Scroll(new Vector2(0f, 1f));
				keyboard.Set("a", 0f);
			});
			yield return Step();

			Assert.That(raw.Devices().Select(Kind), Is.EqualTo(new[] { "Keyboard", "Touchscreen", "Mouse", "Keyboard" }),
				"the order of InputState.onChange");

			var relevant = changes.Items.Where(c => Label(c) != null).ToList();
			Assert.That(relevant.Select(Label), Is.EqualTo(new[] { "keyDown", "touchBegan", "scrolled", "keyUp" }), changes.ToString());
			Assert.That(changes.Of<TouchBegan>().Count(), Is.EqualTo(1), "one touch, reported once");
			for (var i = 1; i < relevant.Count; ++i)
			{
				Assert.That(relevant[i].Order, Is.GreaterThan(relevant[i - 1].Order));
			}

			Assert.That(relevant.Select(c => c.UpdateCount).Distinct().Count(), Is.EqualTo(1), "all in one input update");
			Assert.That(relevant.All(c => c.Frame == fireFrame + 1));
			Assert.That(relevant.All(c => c.IsVirtual));
		}

		[UnityTest]
		public IEnumerator O1_KeyFingerScroll_FiredInOneFrame_AreStartedInThatOrderInGetLatestLogs()
		{
			// The same, through the auto tap: GetLatestLogs lists Started in the order the operations were fired, and
			// each element carries the number of the change that started the item.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			AcquireSession();
			StartAuto();

			LogItem key = null;
			LogItem tap = null;
			LogItem scroll = null;
			yield return Step(() =>
			{
				key = Auto.FireKeyPress(Key.A, 0.25f);
				tap = Auto.FireTap(0, Rig.Center(button.gameObject), 0.25f);
				scroll = Auto.FireScrollAt(Rig.Center(button.gameObject), new Vector2(0f, 1f));
			});
			Assert.That(key, Is.Not.Null);
			Assert.That(tap, Is.Not.Null);
			Assert.That(scroll, Is.Not.Null);
			Assert.That(LatestLogs(), Is.Empty, "nothing was processed yet");

			yield return Step();

			var started = LatestLogs().Where(l => l.Kind == LogEventKind.Started).ToList();
			Assert.That(started.Select(l => l.Item), Is.EqualTo(new[] { key, tap, scroll }), Describe(LatestLogs()));
			Assert.That(started.Select(l => l.Order), Is.Ordered);
			Assert.That(started.Select(l => l.Order), Is.EqualTo(new[] { key.Order, tap.Order, scroll.Order }));
			Assert.That(key.Order, Is.LessThan(tap.Order));
			Assert.That(tap.Order, Is.LessThan(scroll.Order));
			Assert.That(new[] { key.StartedFrameCount, tap.StartedFrameCount, scroll.StartedFrameCount },
				Is.All.EqualTo(key.StartedFrameCount));

			// The list is the same later in the same frame, after Update(float) queued the next steps.
			Assert.That(LatestLogs().Select(l => (l.Item, l.Kind)), Is.EqualTo(started.Select(l => (l.Item, l.Kind))));
		}

		[UnityTest]
		public IEnumerator GetLatestLogs_ListsEverythingThatHappenedInProcessingOrder_OnceEach()
		{
			// Started, DragStarted and Completed of every item appear exactly once (DragStarted only for an item that
			// dragged), in processing order inside a frame, and the numbers agree with the items (documented on
			// AutoTapBase.GetLatestLogs).
			yield return BuildRig();
			var tapButton = Rig.AddButton("TapButton", Rig.Cell(0, 2));
			var source = Rig.Add("Source", Rig.Cell(0, 0));
			var zone = Rig.Add("Zone", Rig.Cell(3, 0));
			var mouseButton = Rig.AddButton("MouseButton", Rig.Cell(1, 2));
			yield return Settle();
			AcquireSession();
			StartAuto();
			CollectLogs = true;

			LogItem tap = null;
			LogItem drag = null;
			LogItem mouse = null;
			LogItem key = null;
			yield return Step(() =>
			{
				tap = Auto.FireTap(0, Rig.Center(tapButton.gameObject), 0.25f);
				drag = Auto.FireFinger(1, Gesture.PressAt(Rig.Center(source)).MoveTo(Rig.Center(zone), 0.4f));
				mouse = Auto.FireMouseGesture(Gesture.PressAt(Rig.Center(mouseButton.gameObject)).Hold(0.25f));
				key = Auto.FireKeyPress(Key.Space, 0.35f);
			});
			var items = new[] { tap, drag, mouse, key };
			Assert.That(items, Is.All.Not.Null);

			yield return Until(() => items.All(i => i.Completed), "all four operations to end");
			yield return Steps(1);

			foreach (var item in items)
			{
				var mine = LogTrace.Where(t => t.Event.Item == item).ToList();
				Assert.That(mine.Count(t => t.Event.Kind == LogEventKind.Started), Is.EqualTo(1), $"Started of {item.Kind}");
				Assert.That(mine.Count(t => t.Event.Kind == LogEventKind.Completed), Is.EqualTo(1), $"Completed of {item.Kind}");
				Assert.That(mine.Count(t => t.Event.Kind == LogEventKind.DragStarted), Is.EqualTo(item == drag ? 1 : 0),
					$"DragStarted of {item.Kind}");

				Assert.That(mine.Single(t => t.Event.Kind == LogEventKind.Started).Event.Order, Is.EqualTo(item.Order));
				Assert.That(mine.Single(t => t.Event.Kind == LogEventKind.Completed).Event.Order, Is.EqualTo(item.CompletedOrder));
				Assert.That(item.Order, Is.LessThan(item.CompletedOrder));
				Assert.That(item.EndReason, Is.EqualTo(EndReason.Finished));
			}

			var dragStarted = LogTrace.Single(t => t.Event.Kind == LogEventKind.DragStarted).Event;
			Assert.That(dragStarted.Order, Is.EqualTo(drag.DragStartedOrder));
			Assert.That(drag.Order, Is.LessThan(drag.DragStartedOrder));
			Assert.That(drag.DragStartedOrder, Is.LessThan(drag.CompletedOrder));

			// Inside every frame, the elements with a number are in the order of the numbers, and those without one
			// come last. Over all frames the numbers only grow.
			foreach (var frame in LogTrace.GroupBy(t => t.Frame))
			{
				var orders = frame.Select(t => t.Event.Order).ToList();
				var firstWithout = orders.FindIndex(o => !o.HasValue);
				if (firstWithout >= 0)
				{
					Assert.That(orders.Skip(firstWithout).All(o => !o.HasValue), Is.True, $"frame {frame.Key}: {string.Join(", ", orders)}");
				}

				var numbered = orders.Where(o => o.HasValue).Select(o => o.Value).ToList();
				Assert.That(numbered, Is.Ordered, $"frame {frame.Key}");
			}

			var all = LogTrace.Where(t => t.Event.Order.HasValue).Select(t => t.Event.Order.Value).ToList();
			Assert.That(all, Is.Ordered);
			Assert.That(all, Is.Unique);

			var firstFrame = LogTrace.Where(t => t.Event.Kind == LogEventKind.Started && t.Frame == tap.StartedFrameCount)
				.Select(t => t.Event.Item);
			Assert.That(firstFrame, Is.EqualTo(new[] { tap, drag, mouse, key }), "started in the order they were fired");
		}

		[UnityTest]
		public IEnumerator O3_ResetOfADevice_HasNoQueuedEventId_AndTheReaderReportsItAsAReset()
		{
			// O3. ResetDevice builds its own state event: with the id -1 for a soft or a hard reset of an ordinary
			// device (InputManager.cs, 1549), and with the id 0 for the touches of a Touchscreen
			// (Touchscreen.cs, 944-975). An id keeps 31 bits (InputEvent.cs, 58 and 137-140), so the -1 reads as
			// int.MaxValue. The ids of queued events start at 1, so a change with no event, with an id of 0 or below, or
			// with int.MaxValue is a reset. The reader reports it as DeviceReset and not as the changes it caused.
			yield return BuildRig();
			yield return Settle();
			AcquireSession();
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			var raw = new RawChangeLog();
			OnTearDown(raw.Dispose);

			var keyboard = Session.GetDevice("Keyboard");
			var mouse = Session.GetPointer("Mouse");
			VirtualFinger finger = null;
			yield return Step(() =>
			{
				keyboard.Set("a", 1f);
				mouse.MoveTo(new Vector2(100f, 100f));
				finger = Session.NewFinger();
				finger.Press(new Vector2(60f, 60f));
			});
			yield return Steps(2);

			Assert.That(raw.Items.Any(r => r.IsQueued), Is.True);
			Assert.That(raw.Items.Where(r => r.EventType != new FourCC()).All(r => r.IsQueued), Is.True,
				"events that were queued have ids from 1");
			var touchscreen = VirtualDeviceOf<Touchscreen>();

			raw.Items.Clear();
			changes.Items.Clear();
			InputSystem.ResetDevice(keyboard.Device);
			InputSystem.ResetDevice(mouse.Device);
			InputSystem.ResetDevice(touchscreen);
			yield return Steps(1);

			Assert.That(raw.Items.Any(r => r.Device == keyboard.Device), Is.True);
			Assert.That(raw.Items.Any(r => r.Device == touchscreen), Is.True);
			foreach (var item in raw.Items)
			{
				Assert.That(item.IsQueued, Is.False, $"{item.Device.name} changed by a reset has no queued event id, but {item.EventId}");
			}

			Assert.That(changes.Of<DeviceReset>().Select(c => c.Device), Does.Contain(keyboard.Device));
			Assert.That(changes.Of<DeviceReset>().Select(c => c.Device), Does.Contain(touchscreen));
			Assert.That(changes.At(keyboard.Device, "a"), Is.Empty, "the key that the reset released is not reported as ButtonUp");
			Assert.That(changes.Of<TouchCanceled>(), Is.Empty, "the touches that the reset canceled are not reported as TouchCanceled");
		}

		[UnityTest]
		public IEnumerator O3_ResetOfTheDevice_InterruptsTheRunningOperations()
		{
			// EndReason.Interrupted is also for a reset of the device while the operation runs. For an operation of a
			// slot, OnTapCompleted follows in the next Update.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem tap = null;
			LogItem key = null;
			yield return Step(() =>
			{
				tap = Auto.FireTap(0, Rig.Center(button.gameObject), 5f);
				key = Auto.FireKeyPress(Key.Space, 5f);
			});
			yield return Until(() => tap.Order.HasValue && key.Order.HasValue, "both presses to be processed");

			InputSystem.ResetDevice(tap.Device);
			InputSystem.ResetDevice(key.Device);
			yield return Steps(2);

			Assert.That(tap.Completed, Is.True);
			Assert.That(tap.EndReason, Is.EqualTo(EndReason.Interrupted));
			Assert.That(key.Completed, Is.True);
			Assert.That(key.EndReason, Is.EqualTo(EndReason.Interrupted));
			Assert.That(Auto.CompletedSlots.Select(c => c.Index), Is.EqualTo(new[] { 0 }));
		}

		[UnityTest]
		public IEnumerator O4_FingerThatTheTouchscreenHasNoRoomFor_IsDropped()
		{
			// O4, the eleventh touch. A Touchscreen keeps 10 touches; a Began without a free one is dropped and
			// onChange is not called for it (Touchscreen.cs, 856-861). Nine touches of another source fill the
			// touchscreen, so the second finger of the auto tap finds no room. The item ends as Dropped, without a
			// change number, and is listed after the elements that have one.
			yield return BuildRig();
			yield return Settle();
			AcquireSession();
			StartAuto();
			var point = Rig.EmptyPoint;

			LogItem first = null;
			LogItem eleventh = null;
			var foreignIds = Enumerable.Range(200, 9).ToArray();
			yield return Step(() =>
			{
				first = Auto.FireTap(0, point, 0.35f);
				var touchscreen = (Touchscreen)first.Device;
				foreach (var id in foreignIds)
				{
					InputSystem.QueueStateEvent(touchscreen, new TouchState
					{
						touchId = id,
						phase = UnityEngine.InputSystem.TouchPhase.Began,
						position = point,
						pressure = 1f,
					});
				}

				eleventh = Auto.FireTap(1, point, 0.35f);
			});
			Assert.That(first, Is.Not.Null);
			if (eleventh == null)
			{
				Assert.Inconclusive("Fire refused the second finger, because the touches of the test are counted as fingers in use.");
			}

			yield return Step();

			Assert.That(first.Order, Is.Not.Null);
			Assert.That(eleventh.Completed, Is.True);
			Assert.That(eleventh.EndReason, Is.EqualTo(EndReason.Dropped));
			Assert.That(eleventh.Order, Is.Null);
			Assert.That(eleventh.CompletedOrder, Is.Null);

			var logs = LatestLogs();
			var droppedAt = logs.FindIndex(l => l.Item == eleventh);
			Assert.That(droppedAt, Is.GreaterThanOrEqualTo(0), Describe(logs));
			Assert.That(logs[droppedAt].Kind, Is.EqualTo(LogEventKind.Completed));
			Assert.That(logs[droppedAt].Order, Is.Null);
			Assert.That(logs.Skip(droppedAt).All(l => !l.Order.HasValue), Is.True, "elements without a number come last");
			Assert.That(logs.Count(l => l.Item == eleventh), Is.EqualTo(1), "the end of an item is listed once");

			yield return Step();
			Assert.That(Auto.CompletedSlots.Select(c => c.Index), Does.Contain(1), "OnTapCompleted is called for a dropped item");

			var touchscreenDevice = (Touchscreen)first.Device;
			foreach (var id in foreignIds)
			{
				InputSystem.QueueStateEvent(touchscreenDevice, new TouchState
				{
					touchId = id,
					phase = UnityEngine.InputSystem.TouchPhase.Ended,
					position = point,
				});
			}

			yield return Until(() => first.Completed, "the first tap to end");
			Assert.That(first.EndReason, Is.EqualTo(EndReason.Finished));
		}

		[UnityTest]
		public IEnumerator O4_TouchWithAnUnknownId_IsNotReported()
		{
			// O4, an unknown touch id. A touch that is not Began and whose id is not in the touchscreen is ignored
			// (Touchscreen.cs, 813): onChange is not called, so the reader reports nothing.
			yield return BuildRig();
			yield return Settle();
			AcquireSession();
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			var raw = new RawChangeLog();
			OnTearDown(raw.Dispose);

			VirtualFinger finger = null;
			yield return Step(() =>
			{
				finger = Session.NewFinger();
				finger.Press(Rig.EmptyPoint);
				finger.Release();
			});
			yield return Steps(2);
			var touchscreen = VirtualDeviceOf<Touchscreen>();
			changes.Items.Clear();
			raw.Items.Clear();

			InputSystem.QueueStateEvent(touchscreen, new TouchState
			{
				touchId = 777,
				phase = UnityEngine.InputSystem.TouchPhase.Moved,
				position = new Vector2(50f, 50f),
			});
			yield return Steps(2);

			Assert.That(raw.Items.Where(r => r.Device == touchscreen && r.IsQueued), Is.Empty, "onChange is not called");
			Assert.That(changes.OfDevice(touchscreen), Is.Empty);
		}

		[UnityTest]
		public IEnumerator O4_EventForADisabledDevice_IsDropped_AndTheOperationEndsAsDropped()
		{
			// O4, a disabled device. Events of a device that is disabled in the runtime are discarded by the event loop
			// (InputManager.cs, 3355-3371), so no change arrives and the item ends as Dropped at the end of that input
			// update. Disabling the device resets it, and the reader reports that reset although the device is disabled
			// by then (InputManager.cs, 1756-1762), so what was collected until the device is disabled is cleared before
			// the operation starts.
			yield return BuildRig();
			yield return Settle();
			AcquireSession();
			StartAuto();
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			var raw = new RawChangeLog();
			OnTearDown(raw.Dispose);

			var keyboard = Session.GetDevice("Keyboard");
			InputSystem.DisableDevice(keyboard.Device);
			OnTearDown(() => InputSystem.EnableDevice(keyboard.Device));
			yield return Step();
			changes.Items.Clear();
			raw.Items.Clear();

			LogItem item = null;
			yield return Step(() => item = Auto.FireKeyPress(Key.Space, 0.25f));
			Assert.That(item, Is.Not.Null);
			yield return Step();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(EndReason.Dropped));
			Assert.That(item.Order, Is.Null);
			Assert.That(item.StartedFrameCount, Is.Zero);
			Assert.That(raw.Items.Where(r => r.Device == keyboard.Device && r.IsQueued), Is.Empty, "onChange is not called");
			Assert.That(changes.OfDevice(keyboard.Device), Is.Empty);

			var logs = LatestLogs();
			Assert.That(logs.Select(l => (l.Item, l.Kind, l.Order)), Is.EqualTo(new[] { (item, LogEventKind.Completed, (long?)null) }),
				Describe(logs));
		}

		[UnityTest]
		public IEnumerator O5_SuccessiveMovesOfOneTouch_AreMergedIntoOneChange_UnlessMergingIsDisabled()
		{
			// O5, a touch. Two Moved events of one touch that follow each other are merged by the touchscreen into the
			// later one (Touchscreen.cs, 983-1003; InputManager.cs, 3373-3434); the reader reports the merged result
			// once. With disableRedundantEventsMerging both are processed.
			yield return BuildRig();
			yield return Settle();
			AcquireSession();
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);

			VirtualFinger finger = null;
			yield return Step(() =>
			{
				finger = Session.NewFinger();
				finger.Press(new Vector2(50f, 50f));
			});
			yield return Steps(2);
			changes.Items.Clear();

			yield return Step(() =>
			{
				finger.MoveTo(new Vector2(60f, 50f));
				finger.MoveTo(new Vector2(70f, 50f));
			});
			yield return Step();
			var merged = changes.Of<TouchMoved>().ToList();
			Assert.That(merged, Has.Count.EqualTo(1), changes.ToString());
			Assert.That(merged[0].Position, Is.EqualTo(new Vector2(70f, 50f)));

			InputSystem.settings.disableRedundantEventsMerging = true;
			OnTearDown(() => InputSystem.settings.disableRedundantEventsMerging = false);
			changes.Items.Clear();
			yield return Step(() =>
			{
				finger.MoveTo(new Vector2(80f, 50f));
				finger.MoveTo(new Vector2(90f, 50f));
			});
			yield return Step();
			var separate = changes.Of<TouchMoved>().ToList();
			Assert.That(separate.Select(m => m.Position), Is.EqualTo(new[] { new Vector2(80f, 50f), new Vector2(90f, 50f) }),
				changes.ToString());
		}

		[UnityTest]
		public IEnumerator O5_SuccessiveMovesOfTheMouse_AreMergedWhenTheButtonsAreTheSame()
		{
			// O5, a mouse. FastMouse merges successive state events whose buttons are the same and adds their deltas
			// (FastMouse.partial.cs, 52-73). The reader reports one event; with merging disabled, two.
			yield return BuildRig();
			yield return Settle();
			AcquireSession();
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			var mouse = Session.GetPointer("Mouse");
			yield return Steps(2);
			changes.Items.Clear();

			yield return Step(() =>
			{
				mouse.MoveTo(new Vector2(50f, 50f));
				mouse.MoveTo(new Vector2(60f, 50f));
			});
			yield return Step();
			Assert.That(PositionEvents(changes, mouse.Device), Is.EqualTo(1), changes.ToString());

			InputSystem.settings.disableRedundantEventsMerging = true;
			OnTearDown(() => InputSystem.settings.disableRedundantEventsMerging = false);
			changes.Items.Clear();
			yield return Step(() =>
			{
				mouse.MoveTo(new Vector2(70f, 50f));
				mouse.MoveTo(new Vector2(80f, 50f));
			});
			yield return Step();
			Assert.That(PositionEvents(changes, mouse.Device), Is.EqualTo(2), changes.ToString());
		}

		static int PositionEvents(ChangeLog changes, InputDevice device)
		{
			return changes.OfDevice(device)
				.Where(c => c.ControlPath != null && c.ControlPath.StartsWith("position"))
				.Select(c => c.EventId)
				.Distinct()
				.Count();
		}
	}
}
#endif
