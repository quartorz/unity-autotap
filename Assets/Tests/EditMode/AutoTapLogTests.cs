#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// The order and the ends of <see cref="AutoTapBase.LogItem"/>s, and what <see cref="AutoTapBase.GetLatestLogs"/>
	/// returns.
	/// </summary>
	public class AutoTapLogTests : InputTestFixture
	{
		const float k_Dt = 0.04f;

		TestAutoTap _autoTap;
		TestScene _scene;
		InputChangeReader _reader;
		List<InputChange> _changes;
		readonly List<AutoTapBase.LogEvent> _allLogs = new List<AutoTapBase.LogEvent>();

		public override void Setup()
		{
			base.Setup();
			_scene = new TestScene();
			_autoTap = new TestAutoTap();
			_autoTap.Setup(_scene.Canvas, null);
			_autoTap.Start();
			_reader = new InputChangeReader();
			_changes = new List<InputChange>();
			_reader.Changed += _changes.Add;
			_allLogs.Clear();
		}

		public override void TearDown()
		{
			_reader.Dispose();
			_autoTap.Dispose();
			_scene.Dispose();
			base.TearDown();
		}

		void Frame(float dt = k_Dt)
		{
			InputSystem.Update();
			CollectLogs();
			_autoTap.Update(dt);
		}

		void CollectLogs()
		{
			_allLogs.AddRange(_autoTap.GetLatestLogs());
		}

		List<AutoTapBase.LogEvent> Logs()
		{
			return _autoTap.GetLatestLogs().ToList();
		}

		[Test]
		public void LogsOfSeveralSlotsAreInTheOrderOfTheChanges()
		{
			var first = _autoTap.Tap(1, TestScene.At(0.2f, 0.2f), 0f);
			var second = _autoTap.Tap(0, TestScene.At(0.4f, 0.2f), 0f);

			Frame();
			var started = Logs();
			Frame();
			var completed = Logs();

			// The presses are processed in the order they were queued, and the releases in the order of the slots.
			Assert.That(started.Select(e => e.Item), Is.EqualTo(new[] { first, second }));
			Assert.That(started.Select(e => e.Kind), Is.All.EqualTo(AutoTapBase.LogEventKind.Started));
			Assert.That(started[0].Order, Is.LessThan(started[1].Order));
			Assert.That(completed.Select(e => e.Item), Is.EqualTo(new[] { second, first }));
			Assert.That(completed.Select(e => e.Kind), Is.All.EqualTo(AutoTapBase.LogEventKind.Completed));
			Assert.That(_autoTap.Completed.Select(p => p.Value), Is.EqualTo(new[] { second, first }));
		}

		[Test]
		public void TheLogOfAnInputUpdateWithoutChangesIsEmpty()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			Assert.That(Logs(), Has.Count.EqualTo(1));

			Frame();

			Assert.That(Logs(), Is.Empty);
		}

		[Test]
		public void TheLogCanBeReadAfterUpdateOfTheSameFrame()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);

			InputSystem.Update();
			_autoTap.Update(k_Dt);

			Assert.That(Logs().Select(e => e.Kind), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }));
		}

		[Test]
		public void TheOrdersAreThoseOfTheReader()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);

			Frame();
			Frame();

			var began = _changes.OfType<TouchBegan>().Single();
			var ended = _changes.OfType<TouchEnded>().Single();
			Assert.That(item.Order, Is.EqualTo(began.Order));
			Assert.That(item.CompletedOrder, Is.EqualTo(ended.Order));
			Assert.That(item.StartedFrameCount, Is.EqualTo(Time.frameCount));
			Assert.That(item.CompletedFrameCount, Is.EqualTo(Time.frameCount));
		}

		[Test]
		public void TheItemHasTheDeviceItWasMadeWith()
		{
			var item = _autoTap.Tap(1, TestScene.At(0.2f, 0.2f), 0f);

			Assert.That(item.Device, Is.SameAs(Touchscreen.current));
			Assert.That(item.Index, Is.EqualTo(1));
			Assert.That(item.ScreenPoint, Is.EqualTo(TestScene.At(0.2f, 0.2f)));
			Assert.That(item.FrameCount, Is.EqualTo(Time.frameCount));
			Assert.That(item.Duration, Is.EqualTo(0f));
			Assert.That(item.DragTo, Is.Null);
		}

		[Test]
		public void ADragAndAGestureRecordTheirDestination()
		{
			var from = TestScene.At(0.2f, 0.2f);
			var to = TestScene.At(0.4f, 0.2f);
			var drag = _autoTap.Drag(0, from, to, 0.1f);
			var gesture = Gesture.PressAt(from).MoveTo(to, 0.1f);
			var item = _autoTap.Do(1, gesture);
			var still = _autoTap.Drag(2, from, from, 0.1f);

			Assert.That(drag.Kind, Is.EqualTo(AutoTapBase.LogItemKind.Drag));
			Assert.That(drag.DragTo, Is.EqualTo(to));
			Assert.That(drag.Duration, Is.EqualTo(0.1f));
			Assert.That(drag.Gesture, Is.Null);
			Assert.That(item.Kind, Is.EqualTo(AutoTapBase.LogItemKind.Gesture));
			Assert.That(item.DragTo, Is.EqualTo(to));
			Assert.That(item.Duration, Is.NaN);
			Assert.That(item.Gesture, Is.SameAs(gesture));
			Assert.That(still.DragTo, Is.Null);
		}

		[Test]
		public void StopEndsARunningOperationAsInterruptedAndReportsItOnceInTheNextInputUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();

			_autoTap.Stop();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(item.CompletedOrder, Is.Null);
			Assert.That(item.CompletedFrameCount, Is.EqualTo(Time.frameCount));
			Assert.That(_autoTap.Completed.Select(p => p.Value), Is.EqualTo(new[] { item }), "reported from inside Stop");
			Assert.That(AutoTapBase.Current, Is.Null);
			Assert.That(_autoTap.Enabled, Is.False);

			InputSystem.Update();
			var logs = Logs();
			InputSystem.Update();
			var after = Logs();

			Assert.That(logs, Has.Count.EqualTo(1));
			Assert.That(logs[0].Item, Is.SameAs(item));
			Assert.That(logs[0].Kind, Is.EqualTo(AutoTapBase.LogEventKind.Completed));
			Assert.That(logs[0].Order, Is.Null);
			Assert.That(after, Is.Empty);
			Assert.That(_changes.OfType<TouchEnded>().Count(), Is.EqualTo(1), "the release reached the Input System");
		}

		[Test]
		public void StopBeforeThePressWasProcessedPressesAndReleasesInOneInputUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);

			_autoTap.Stop();
			InputSystem.Update();

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(_changes.OfType<TouchBegan>().Count(), Is.EqualTo(1));
			Assert.That(_changes.OfType<TouchEnded>().Count(), Is.EqualTo(1));
			Assert.That(Logs().Select(e => e.Kind), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
		}

		[Test]
		public void StopWhileTheReleaseIsQueuedStillReportsTheEndInTheNextInputUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);
			Frame(); // the press is processed and the release is queued

			_autoTap.Stop();
			InputSystem.Update();

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(_changes.OfType<TouchEnded>().Count(), Is.EqualTo(1), "the queued release reached the Input System");
			Assert.That(Logs().Select(e => e.Item), Is.EqualTo(new[] { item }));
		}

		[Test]
		public void StopOrdersTheEndsByHowTheItemsWereFired()
		{
			var first = _autoTap.Tap(2, TestScene.At(0.2f, 0.2f), 1f);
			var second = _autoTap.Tap(0, TestScene.At(0.4f, 0.2f), 1f);
			var third = _autoTap.Tap(1, TestScene.At(0.6f, 0.2f), 1f);
			Frame();

			_autoTap.Stop();
			InputSystem.Update();

			var logs = Logs();
			Assert.That(logs.Select(e => e.Item), Is.EqualTo(new[] { first, second, third }));
			Assert.That(logs.Select(e => e.Order), Is.All.Null);
			Assert.That(_autoTap.Completed.Select(p => p.Value), Is.EqualTo(new[] { first, second, third }));
		}

		[Test]
		public void StopReportsWhatHadEndedBeforeWhatItInterrupts()
		{
			var ended = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);
			var running = _autoTap.Tap(1, TestScene.At(0.4f, 0.2f), 1f);
			Frame();
			InputSystem.Update(); // the release of the first is processed; the slot is not reported yet

			_autoTap.Stop();

			Assert.That(_autoTap.Completed.Select(p => p.Value), Is.EqualTo(new[] { ended, running }));
			Assert.That(ended.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(running.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
		}

		[Test]
		public void EveryItemEndsExactlyOnceInTheLogs()
		{
			var finished = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);
			var interrupted = _autoTap.Tap(1, TestScene.At(0.4f, 0.2f), 1f);
			Frame();
			Frame();
			InputSystem.DisableDevice(Touchscreen.current);
			var dropped = _autoTap.Tap(0, TestScene.At(0.6f, 0.2f), 1f);
			Frame();
			InputSystem.EnableDevice(Touchscreen.current);
			var late = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			Frame();
			_autoTap.Stop();
			InputSystem.Update();
			CollectLogs();
			InputSystem.Update();
			CollectLogs();

			var reasons = new[] { finished, interrupted, dropped, late }.Select(i => i.EndReason).ToArray();
			Assert.That(reasons, Is.EqualTo(new[]
			{
				AutoTapBase.EndReason.Finished,
				AutoTapBase.EndReason.Interrupted,
				AutoTapBase.EndReason.Dropped,
				AutoTapBase.EndReason.Interrupted,
			}));
			foreach (var item in new[] { finished, interrupted, dropped, late })
			{
				Assert.That(item.Completed, Is.True, item.ToString());
				var ends = _allLogs.Count(e => e.Item == item && e.Kind == AutoTapBase.LogEventKind.Completed);
				Assert.That(ends, Is.EqualTo(1), $"{item.EndReason} {item.Index} :: " + string.Join(" | ", _allLogs.Select(e => $"{e.Kind} {e.Item.Index}/{e.Item.EndReason} o{e.Order}")));
			}
		}

		[Test]
		public void AnOperationWhoseEventWasDroppedEndsAsDroppedInTheSameInputUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.DisableDevice(Touchscreen.current);

			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Dropped));
			Assert.That(item.Order, Is.Null);
			Assert.That(item.CompletedOrder, Is.Null);
			Assert.That(item.CompletedFrameCount, Is.EqualTo(Time.frameCount));
			var logs = Logs();
			Assert.That(logs, Has.Count.EqualTo(1));
			Assert.That(logs[0].Kind, Is.EqualTo(AutoTapBase.LogEventKind.Completed));
			Assert.That(logs[0].Order, Is.Null);
			Assert.That(_autoTap.Completed.Select(p => p.Value), Is.EqualTo(new[] { item }), "the slot is reported at once");
			Assert.That(_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f), Is.Not.Null, "and free");
		}

		[Test]
		public void DroppedItemsComeAfterTheOnesThatHaveAnOrderInTheOrderTheyWereFired()
		{
			var running = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			Frame();
			var tail = new[]
			{
				_autoTap.Tap(2, TestScene.At(0.6f, 0.2f), 1f),
				_autoTap.Tap(1, TestScene.At(0.4f, 0.2f), 1f),
			};

			// The device is disabled after the presses were queued, so that they are dropped. Disabling resets the
			// device, which ends the running operation.
			InputSystem.DisableDevice(Touchscreen.current);
			Frame();

			var logs = Logs();
			Assert.That(
				logs.Select(e => e.Item), Is.EqualTo(new[] { running, tail[0], tail[1] }),
				string.Join(" | ", logs.Select(e => $"{e.Kind} slot {e.Item.Index} order {e.Order} {e.Item.EndReason}")));
			Assert.That(logs[0].Order, Is.Not.Null, "the reset that came with disabling the device ended it");
			Assert.That(logs.Skip(1).Select(e => e.Order), Is.All.Null);
			Assert.That(running.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(tail.Select(i => i.EndReason), Is.All.EqualTo(AutoTapBase.EndReason.Dropped));
		}

		[Test]
		public void ItemsWithAnOrderComeBeforeItemsWithoutOne()
		{
			var processed = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			Frame();
			var move = _autoTap.Do(1, Gesture.PressAt(TestScene.At(0.4f, 0.2f)).MoveTo(TestScene.At(0.5f, 0.2f), 0.2f));
			Frame(); // the press of slot 1 is processed
			InputSystem.DisableDevice(Touchscreen.current);
			var dropped = _autoTap.Tap(2, TestScene.At(0.6f, 0.2f), 1f);

			_autoTap.Stop();
			InputSystem.Update();

			// Everything was dropped by the disabled device; nothing here has an order.
			var logs = Logs();
			Assert.That(logs.Select(e => e.Item), Is.EqualTo(new[] { processed, move, dropped }));
			Assert.That(logs.Select(e => e.Kind), Is.All.EqualTo(AutoTapBase.LogEventKind.Completed));
		}

		[Test]
		public void AResetOfTheTouchscreenInterruptsTheOperationsThatAreRunning()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();

			InputSystem.ResetDevice(Touchscreen.current);

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(item.CompletedOrder, Is.Not.Null);
			Frame();
			Assert.That(_autoTap.Completed.Select(p => p.Value), Is.EqualTo(new[] { item }));
		}

		[Test]
		public void AFireFromOnTapCompletedIsAccepted()
		{
			AutoTapBase.LogItem second = null;
			_autoTap.OnCompletedHook = (index, item) =>
			{
				if (second == null)
				{
					second = _autoTap.Tap(index, TestScene.At(0.4f, 0.2f), 0f);
				}
			};
			var first = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);

			Frame();
			Frame();

			Assert.That(first.Completed, Is.True);
			Assert.That(second, Is.Not.Null);
			Frame();
			Frame();
			Assert.That(second.Completed, Is.True);
		}

		[Test]
		public void OnTapCompletedIsCalledBeforeTheSlotsAreVisited()
		{
			var calls = new List<string>();
			_autoTap.OnEmptySlot = index => calls.Add($"empty {index}");
			_autoTap.OnCompletedHook = (index, item) => calls.Add($"completed {index}");
			_autoTap.Tap(1, TestScene.At(0.2f, 0.2f), 0f);
			Frame();
			calls.Clear();

			Frame();

			Assert.That(calls, Is.EqualTo(new[] { "completed 1", "empty 0", "empty 1", "empty 2" }));
		}

		[Test]
		public void TheMarkerIsShownWhileTheOperationRuns()
		{
			var marker = _scene.Canvas.transform.Find("Marker 1").gameObject;
			Assert.That(marker.activeSelf, Is.False);

			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0.1f);
			Assert.That(marker.activeSelf, Is.True);
			Frame();
			Frame();
			Frame();
			Frame();
			Assert.That(item.Completed, Is.True);
			Assert.That(marker.activeSelf, Is.False);
		}

		[Test]
		public void TwoFingersWorkAtTheSameTime()
		{
			var first = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0.1f);
			var second = _autoTap.Tap(1, TestScene.At(0.6f, 0.4f), 0.1f);

			Frame();

			var touches = Touchscreen.current.touches;
			Assert.That(touches[0].isInProgress && touches[1].isInProgress, Is.True);
			Assert.That(touches[0].touchId.ReadValue(), Is.Not.EqualTo(touches[1].touchId.ReadValue()));
			Frame();
			Frame();
			Frame();
			Frame();
			Assert.That(first.Completed && second.Completed, Is.True);
		}

		[Test]
		public void TheLifecycleChecksItsCalls()
		{
			Assert.That(AutoTapBase.Current, Is.SameAs(_autoTap));
			_autoTap.Start();
			Assert.That(AutoTapBase.Current, Is.SameAs(_autoTap));

			using (var other = new TestAutoTap())
			{
				other.Setup(null, null);
				Assert.Throws<InvalidOperationException>(() => other.Start());
				Assert.Throws<InvalidOperationException>(() => other.Stop());
			}

			_autoTap.Stop();
			Assert.Throws<InvalidOperationException>(() => _autoTap.Stop());
			using (var unprepared = new TestAutoTap())
			{
				Assert.Throws<InvalidOperationException>(() => unprepared.Start());
			}
		}

		[Test]
		public void DisposeStopsARunningInstance()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();

			_autoTap.Dispose();

			Assert.That(AutoTapBase.Current, Is.Null);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
		}

		[Test]
		public void AnInjectedInputIsNotDisposed()
		{
			_autoTap.Dispose();
			using (var session = VirtualInput.Acquire())
			{
				var other = new TestAutoTap(session);
				other.Setup(null, null);
				other.Start();
				other.Tap(0, TestScene.At(0.2f, 0.2f), 0f);
				other.Stop();
				InputSystem.Update();

				Assert.That(session.NewFinger(), Is.Not.Null);
				other.Dispose();
				Assert.That(session.NewFinger(), Is.Not.Null);
			}
		}

		[Test]
		public void StartFailsWithoutChangingAnythingWhenTheInputSystemCannotBeDriven()
		{
			_autoTap.Dispose();
			InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
			using (var other = new TestAutoTap())
			{
				other.Setup(null, null);

				Assert.Throws<InvalidOperationException>(() => other.Start());

				Assert.That(AutoTapBase.Current, Is.Null);
				Assert.That(other.Enabled, Is.False);
			}
		}
	}
}
#endif
