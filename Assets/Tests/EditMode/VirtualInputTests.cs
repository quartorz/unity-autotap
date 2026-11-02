#if ENABLE_AUTOTAP
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityAutoTap.Tests
{
	public class VirtualInputTests : InputTestFixture
	{
		VirtualInputSession _session;

		public override void TearDown()
		{
			_session?.Dispose();
			_session = null;
			base.TearDown();
		}

		[Test]
		public void AcquireDoesNotAddDevicesUntilTheyAreUsed()
		{
			_session = VirtualInput.Acquire();

			Assert.That(InputSystem.devices, Is.Empty);
		}

		[Test]
		public void TheFirstFingerAddsAVirtualTouchscreen()
		{
			_session = VirtualInput.Acquire();

			var finger = _session.NewFinger();

			Assert.That(finger, Is.Not.Null);
			var touchscreen = Touchscreen.current;
			Assert.That(touchscreen, Is.Not.Null);
			Assert.That(touchscreen.name, Is.EqualTo("AutoTap-Touchscreen-0"));
			Assert.That(VirtualInput.IsVirtual(touchscreen), Is.True);
			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
		}

		[Test]
		public void IsVirtualIsFalseForRealDevicesAndNull()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();

			Assert.That(VirtualInput.IsVirtual(keyboard), Is.False);
			Assert.That(VirtualInput.IsVirtual(null), Is.False);
		}

		[Test]
		public void AtMostTenFingersAreGivenAtATime()
		{
			_session = VirtualInput.Acquire();
			var fingers = new VirtualFinger[10];
			for (var i = 0; i < fingers.Length; ++i)
			{
				fingers[i] = _session.NewFinger();
				Assert.That(fingers[i], Is.Not.Null);
				fingers[i].Press(new Vector2(10 + i, 10));
			}

			Assert.That(_session.NewFinger(), Is.Null);

			InputSystem.Update();
			fingers[0].Release();
			InputSystem.Update();

			Assert.That(fingers[0].State, Is.EqualTo(FingerState.Released));
			Assert.That(_session.NewFinger(), Is.Not.Null);
		}

		[Test]
		public void TouchIdsAreNotReused()
		{
			_session = VirtualInput.Acquire();
			var first = _session.NewFinger();
			first.Press(new Vector2(10, 10));
			first.Release();
			InputSystem.Update();

			var second = _session.NewFinger();

			Assert.That(second.TouchId, Is.Not.EqualTo(first.TouchId));
		}

		[Test]
		public void AFingerFollowsItsEventsThroughQueuedPressedAndReleased()
		{
			_session = VirtualInput.Acquire();
			var finger = _session.NewFinger();

			finger.Press(new Vector2(10, 20));
			Assert.That(finger.State, Is.EqualTo(FingerState.Queued));
			Assert.That(Touchscreen.current.touches[0].isInProgress, Is.False, "nothing is processed by Press");

			InputSystem.Update();
			Assert.That(finger.State, Is.EqualTo(FingerState.Pressed));
			Assert.That(finger.Position, Is.EqualTo(new Vector2(10, 20)));
			Assert.That(Touchscreen.current.touches[0].isInProgress, Is.True);
			Assert.That(Touchscreen.current.touches[0].touchId.ReadValue(), Is.EqualTo(finger.TouchId));

			finger.MoveTo(new Vector2(30, 40));
			finger.Release();
			Assert.That(finger.State, Is.EqualTo(FingerState.ReleaseQueued));

			InputSystem.Update();
			Assert.That(finger.State, Is.EqualTo(FingerState.Released));
			Assert.That(Touchscreen.current.touches[0].isInProgress, Is.False);
		}

		[Test]
		public void FingerWritesPressureAndRadius()
		{
			_session = VirtualInput.Acquire();
			var finger = _session.NewFinger();
			finger.Press(new Vector2(10, 20));
			InputSystem.Update();

			finger.Set("pressure", 0.25f);
			finger.Set("radius", new Vector2(3, 4));
			InputSystem.Update();

			var touch = Touchscreen.current.touches[0];
			Assert.That(touch.pressure.ReadValue(), Is.EqualTo(0.25f));
			Assert.That(touch.radius.ReadValue(), Is.EqualTo(new Vector2(3, 4)));
		}

		[Test]
		public void FingerRejectsControlsThatTheTouchscreenComputes()
		{
			_session = VirtualInput.Acquire();
			var finger = _session.NewFinger();
			finger.Press(new Vector2(10, 20));

			Assert.Throws<ArgumentException>(() => finger.Set("tapCount", 1));
			Assert.Throws<ArgumentException>(() => finger.Set("pressure", new Vector2(1, 1)));
		}

		[Test]
		public void FingerRefusesCallsInTheWrongOrder()
		{
			_session = VirtualInput.Acquire();
			var finger = _session.NewFinger();

			Assert.Throws<InvalidOperationException>(() => finger.MoveTo(Vector2.zero));
			finger.Press(new Vector2(10, 20));
			Assert.Throws<InvalidOperationException>(() => finger.Press(new Vector2(10, 20)));
			finger.Release();
			Assert.Throws<InvalidOperationException>(() => finger.MoveTo(Vector2.zero));
			Assert.Throws<InvalidOperationException>(() => finger.Release());
		}

		[Test]
		public void AcquireFailsUnlessEventsAreProcessedInTheDynamicUpdate()
		{
			InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;

			Assert.Throws<InvalidOperationException>(() => VirtualInput.Acquire());
		}

		[Test]
		public void TheSettingsAreReplacedWhileAcquiredAndGivenBack()
		{
			var original = InputSystem.settings;
			original.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableAllDevices;
			original.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDevicesRespectGameViewFocus;

			_session = VirtualInput.Acquire();

			Assert.That(InputSystem.settings, Is.Not.SameAs(original));
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.IgnoreFocus));
			Assert.That(
				InputSystem.settings.editorInputBehaviorInPlayMode,
				Is.EqualTo(InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView));
			Assert.That(
				original.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.ResetAndDisableAllDevices),
				"the original settings are not touched");

			_session.Dispose();
			_session = null;

			Assert.That(InputSystem.settings, Is.SameAs(original));
			Assert.That(
				InputSystem.settings.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.ResetAndDisableAllDevices));
		}

		[Test]
		public void TheReferencesAreCounted()
		{
			var original = InputSystem.settings;
			var first = VirtualInput.Acquire();
			var second = VirtualInput.Acquire();

			first.Dispose();
			first.Dispose();
			Assert.That(InputSystem.settings, Is.Not.SameAs(original), "one session is left");

			second.Dispose();
			Assert.That(InputSystem.settings, Is.SameAs(original));
		}

		[Test]
		public void DisposeRemovesAnUnusedDeviceAtOnce()
		{
			_session = VirtualInput.Acquire();
			var finger = _session.NewFinger();
			finger.Press(new Vector2(10, 20));
			finger.Release();
			InputSystem.Update();
			Assert.That(finger.State, Is.EqualTo(FingerState.Released));

			_session.Dispose();
			_session = null;

			Assert.That(InputSystem.devices, Is.Empty);
		}

		[Test]
		public void DisposeReleasesWhatIsPressedAndRemovesTheDevicesAfterTheUiHadTime()
		{
			var original = InputSystem.settings;
			_session = VirtualInput.Acquire();
			var finger = _session.NewFinger();
			finger.Press(new Vector2(10, 20));
			InputSystem.Update();

			_session.Dispose();
			_session = null;

			Assert.That(InputSystem.devices, Has.Count.EqualTo(1), "the release is only queued");
			InputSystem.Update();
			Assert.That(finger.State, Is.EqualTo(FingerState.Released), "the release was processed");
			Assert.That(InputSystem.devices, Has.Count.EqualTo(1), "the pointer is still there for the UI");
			Assert.That(InputSystem.settings, Is.Not.SameAs(original));
			InputSystem.Update();
			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
			InputSystem.Update();
			Assert.That(InputSystem.devices, Is.Empty);
			Assert.That(InputSystem.settings, Is.SameAs(original));
		}

		[Test]
		public void AcquireWhileDrainingKeepsTheDevices()
		{
			_session = VirtualInput.Acquire();
			var finger = _session.NewFinger();
			finger.Press(new Vector2(10, 20));
			InputSystem.Update();
			_session.Dispose();

			_session = VirtualInput.Acquire();
			InputSystem.Update();
			InputSystem.Update();
			InputSystem.Update();
			InputSystem.Update();

			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
			Assert.That(_session.NewFinger(), Is.Not.Null);
		}

		[Test]
		public void LeftoverVirtualDevicesAreRemovedWhenAcquiring()
		{
			var leftover = InputSystem.AddDevice("Keyboard", "AutoTap-Keyboard-0");
			var real = InputSystem.AddDevice<Mouse>();

			_session = VirtualInput.Acquire();

			Assert.That(leftover.added, Is.False);
			Assert.That(real.added, Is.True);
		}

		[Test]
		public void TheReaderOfTheSessionReportsVirtualDevicesOnly()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();
			_session = VirtualInput.Acquire();
			var changes = new System.Collections.Generic.List<InputChange>();
			_session.Reader.Changed += changes.Add;
			var finger = _session.NewFinger();

			Press(keyboard.aKey);
			finger.Press(new Vector2(10, 20));
			InputSystem.Update();

			Assert.That(changes, Has.Count.EqualTo(1));
			Assert.That(changes[0], Is.TypeOf<TouchBegan>());
			Assert.That(changes[0].IsVirtual, Is.True);
		}

		[Test]
		public void InputUpdatedIsRaisedAfterEachDynamicUpdate()
		{
			_session = VirtualInput.Acquire();
			var count = 0;
			_session.InputUpdated += () => ++count;

			InputSystem.Update();
			InputSystem.Update();

			Assert.That(count, Is.EqualTo(2));
		}

		[Test]
		public void AFingerThatWasNotProcessedIsDropped()
		{
			_session = VirtualInput.Acquire();
			var finger = _session.NewFinger();
			InputSystem.DisableDevice(Touchscreen.current);

			finger.Press(new Vector2(10, 20));
			InputSystem.Update();

			Assert.That(finger.State, Is.EqualTo(FingerState.Released));
		}

		[Test]
		public void AResetOfTheTouchscreenReleasesTheFingers()
		{
			_session = VirtualInput.Acquire();
			var finger = _session.NewFinger();
			finger.Press(new Vector2(10, 20));
			InputSystem.Update();

			InputSystem.ResetDevice(Touchscreen.current);

			Assert.That(finger.State, Is.EqualTo(FingerState.Released));
		}
	}
}
#endif
