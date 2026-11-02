#if ENABLE_AUTOTAP
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// <see cref="InputChangeReader"/>: it reports every enabled device, real and virtual, one event at a time in
	/// processing order. Experiment R1.
	/// </summary>
	/// <remarks>
	/// The reader is built on <c>InputState.onChange</c>, which is called once per event for most devices and several
	/// times for one event of a touchscreen (InputManager.cs, 4068; Touchscreen.cs, 748-849), and on
	/// <c>InputSystem.onEvent</c> for text (InputManager.cs, 3461-3465, 3562-3594). It leaves out the changes that no
	/// event caused, which are the zeroing of the deltas at the start of an update (InputManager.cs, 2766-2783).
	/// </remarks>
	public class ReaderTests : AutoTapFixture
	{
		[UnityTest]
		public IEnumerator R1_EveryKindOfChange_IsReported_ForRealAndVirtualDevices_InTheOrderTheyWereQueued()
		{
			yield return BuildRig();
			AcquireSession();
			var all = ReadAllDevices();
			var realKeyboard = AddRealDevice<Keyboard>("Keyboard", "RealKeyboard");
			var keyboard = Session.GetDevice("Keyboard");
			var mouse = Session.GetPointer("Mouse");
			yield return Steps(2);
			all.Items.Clear();

			VirtualFinger first = null;
			VirtualFinger second = null;
			yield return Step(() =>
			{
				Press(realKeyboard.aKey);
				first = Session.NewFinger();
				first.Press(new Vector2(50f, 50f));
				first.MoveTo(new Vector2(60f, 50f));
				first.Release();
				second = Session.NewFinger();
				second.Press(new Vector2(80f, 50f));
				second.Cancel();
				mouse.MoveTo(new Vector2(100f, 100f));
				keyboard.Text("x");
				keyboard.Composition("y");
				Release(realKeyboard.aKey);
			});
			yield return Step();

			var labels = new List<string>();
			var relevant = new List<ChangeRecord>();
			ChangeRecord previous = null;
			foreach (var change in all.Items)
			{
				string label = null;
				if (change.Is<ButtonDown>() && change.Device == realKeyboard && change.ControlPath == "a")
				{
					label = "realDown";
				}
				else if (change.Is<ButtonUp>() && change.Device == realKeyboard && change.ControlPath == "a")
				{
					label = "realUp";
				}
				else if (change.Is<TouchBegan>())
				{
					label = "began";
				}
				else if (change.Is<TouchMoved>())
				{
					label = "moved";
				}
				else if (change.Is<TouchEnded>())
				{
					label = "ended";
				}
				else if (change.Is<TouchCanceled>())
				{
					label = "canceled";
				}
				else if (change.Is<ValueChanged>() && change.Device == mouse.Device && change.ControlPath.StartsWith("position"))
				{
					// x and y of one event are two changes of the same event.
					label = previous != null && previous.EventId == change.EventId && previous.Is<ValueChanged>() ? null : "mouseMoved";
				}
				else if (change.Is<TextEntered>())
				{
					label = "text:" + change.Text;
				}
				else if (change.Is<CompositionChanged>())
				{
					label = "composition:" + change.Composition;
				}

				previous = change;
				if (label != null)
				{
					labels.Add(label);
					relevant.Add(change);
				}
			}

			Assert.That(labels, Is.EqualTo(new[]
			{
				"realDown", "began", "moved", "ended", "began", "canceled", "mouseMoved", "text:x", "composition:y", "realUp",
			}), all.ToString());

			for (var i = 1; i < relevant.Count; ++i)
			{
				Assert.That(relevant[i].Order, Is.GreaterThan(relevant[i - 1].Order));
				Assert.That(relevant[i].EventId, Is.Not.EqualTo(relevant[i - 1].EventId), "each of these is a different event");
			}

			Assert.That(relevant.Select(c => c.IsVirtual), Is.EqualTo(new[]
			{
				false, true, true, true, true, true, true, true, true, false,
			}), "the real keyboard is not virtual, everything else is");
			Assert.That(relevant.Select(c => c.Device == realKeyboard), Is.EqualTo(new[]
			{
				true, false, false, false, false, false, false, false, false, true,
			}));

			var touches = relevant.Where(c => c.Is<TouchBegan>() || c.Is<TouchMoved>() || c.Is<TouchEnded>() || c.Is<TouchCanceled>()).ToList();
			Assert.That(touches.Select(t => t.TouchId), Is.EqualTo(new[]
			{
				touches[0].TouchId, touches[0].TouchId, touches[0].TouchId, touches[4].TouchId, touches[4].TouchId,
			}));
			Assert.That(touches[0].TouchId, Is.Not.EqualTo(touches[3].TouchId), "two fingers, two touch ids");
			Assert.That(touches[0].StartPosition, Is.EqualTo(new Vector2(50f, 50f)));
			Assert.That(touches[1].Position, Is.EqualTo(new Vector2(60f, 50f)));
			Assert.That(touches[1].StartPosition, Is.EqualTo(new Vector2(50f, 50f)));
		}

		[UnityTest]
		public IEnumerator R1_PressAndReleaseInOneUpdate_AreTwoChanges_AndScrollIsReportedPerEvent_AndNotZeroed()
		{
			// A touch that began and ended in one update gives TouchBegan and TouchEnded. The scroll of two events that
			// are not next to each other is two changes whose amounts are the amounts of the events, although the control
			// holds their sum (FastMouse.partial.cs, 18-40). The zeroing at the start of the next update is not reported.
			yield return BuildRig();
			AcquireSession();
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			var keyboard = Session.GetDevice("Keyboard");
			var mouse = Session.GetPointer("Mouse");
			yield return Steps(2);
			changes.Items.Clear();

			yield return Step(() =>
			{
				var finger = Session.NewFinger();
				finger.Press(new Vector2(50f, 50f));
				finger.Release();
				mouse.Scroll(new Vector2(0f, 1f));
				keyboard.Set("b", 1f);
				mouse.Scroll(new Vector2(0f, 2f));
				keyboard.Set("b", 0f);
			});
			yield return Step();

			var touch = changes.Items.Where(c => c.Is<TouchBegan>() || c.Is<TouchEnded>()).ToList();
			Assert.That(touch.Select(t => t.Type), Is.EqualTo(new[] { typeof(TouchBegan), typeof(TouchEnded) }), changes.ToString());
			Assert.That(touch[0].TouchId, Is.EqualTo(touch[1].TouchId));
			Assert.That(touch[0].UpdateCount, Is.EqualTo(touch[1].UpdateCount), "in one input update");

			var scrolled = changes.Of<Scrolled>().ToList();
			Assert.That(scrolled.Select(s => s.Delta), Is.EqualTo(new[] { new Vector2(0f, 1f), new Vector2(0f, 2f) }), changes.ToString());
			Assert.That(mouse.Device is Mouse);
			Assert.That(((Mouse)mouse.Device).scroll.ReadUnprocessedValue(), Is.EqualTo(new Vector2(0f, 3f)), "the control holds the sum");

			var count = changes.Items.Count;
			yield return Step();
			Assert.That(((Mouse)mouse.Device).scroll.ReadUnprocessedValue(), Is.EqualTo(Vector2.zero), "the Input System zeroed it");
			Assert.That(changes.Items.Count, Is.EqualTo(count), "and nothing was reported for that");
		}

		[UnityTest]
		public IEnumerator R1_ScrollEventsThatFollowEachOther_AreMergedIntoOne()
		{
			// Two events of a mouse in a row with the same buttons are merged by the Input System, adding their scroll
			// (FastMouse.partial.cs, 52-73), so the reader reports what the merged event carried.
			yield return BuildRig();
			AcquireSession();
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			var mouse = Session.GetPointer("Mouse");
			yield return Steps(2);
			changes.Items.Clear();

			yield return Step(() =>
			{
				mouse.Scroll(new Vector2(0f, 1f));
				mouse.Scroll(new Vector2(0f, 2f));
			});
			yield return Step();

			Assert.That(changes.Of<Scrolled>().Select(s => s.Delta), Is.EqualTo(new[] { new Vector2(0f, 3f) }), changes.ToString());
		}

		[UnityTest]
		public IEnumerator R1_TheDeviceFilter_NarrowsWhatIsReported()
		{
			// The filter is asked for every change, with devices that were added after the reader was created.
			yield return BuildRig();
			AcquireSession();
			var keyboards = ReadAllDevices(device => device is Keyboard);
			var everything = ReadAllDevices();
			var realKeyboard = AddRealDevice<Keyboard>("Keyboard", "RealKeyboard");
			var virtualKeyboard = Session.GetDevice("Keyboard");
			var mouse = Session.GetPointer("Mouse");
			yield return Steps(2);
			keyboards.Items.Clear();
			everything.Items.Clear();

			yield return Step(() =>
			{
				Press(realKeyboard.bKey);
				virtualKeyboard.Set("c", 1f);
				mouse.MoveTo(new Vector2(90f, 90f));
				virtualKeyboard.Set("c", 0f);
				Release(realKeyboard.bKey);
			});
			yield return Step();

			Assert.That(keyboards.Items, Is.Not.Empty);
			Assert.That(keyboards.Items.All(c => c.Device is Keyboard), Is.True, keyboards.ToString());
			Assert.That(keyboards.Items.Any(c => c.IsVirtual), Is.True);
			Assert.That(keyboards.Items.Any(c => !c.IsVirtual), Is.True);
			Assert.That(everything.Items.Any(c => c.Device is Mouse), Is.True);
			Assert.That(keyboards.Items.Select(c => c.Order), Is.Ordered);
		}
	}
}
#endif
