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
	/// <see cref="VirtualPointer"/>, which queues events to a virtual mouse or pen, on the Input System of
	/// <see cref="InputTestFixture"/>.
	/// </summary>
	public class VirtualPointerTests : InputTestFixture
	{
		VirtualInputSession _session;
		List<InputChange> _changes;

		public override void Setup()
		{
			base.Setup();
			_session = VirtualInput.Acquire();
			_changes = new List<InputChange>();
			_session.Reader.Changed += _changes.Add;
		}

		public override void TearDown()
		{
			_session?.Dispose();
			_session = null;
			base.TearDown();
		}

		[Test]
		public void TheSameLayoutAndNumberGiveTheSamePointer()
		{
			var mouse = _session.GetPointer("Mouse");

			Assert.That(_session.GetPointer("Mouse"), Is.SameAs(mouse));
			Assert.That(_session.GetPointer("mouse"), Is.SameAs(mouse), "layout names do not depend on the case");
			Assert.That(_session.GetPointer("Mouse", 1), Is.Not.SameAs(mouse));
			Assert.That(mouse.Device, Is.InstanceOf<Mouse>());
			Assert.That(mouse.Device.name, Is.EqualTo("AutoTap-Mouse-0"));
			Assert.That(VirtualInput.IsVirtual(mouse.Device), Is.True);
			Assert.That(_session.GetPointer("Pen").Device, Is.InstanceOf<Pen>());
		}

		[Test]
		public void OnlyPointersCanBeGivenAndNothingIsLeftBehindWhenItFails()
		{
			Assert.Throws<ArgumentException>(() => _session.GetPointer("Touchscreen"));
			Assert.Throws<ArgumentException>(() => _session.GetPointer("Keyboard"));
			Assert.Throws<ArgumentException>(() => _session.GetPointer("NoSuchLayout"));
			Assert.Throws<ArgumentException>(() => _session.GetPointer(""));

			Assert.That(InputSystem.devices, Is.Empty);
		}

		[Test]
		public void TheDeviceIsAddedWhenThePointerIsGiven()
		{
			Assert.That(InputSystem.devices, Is.Empty);

			_session.GetPointer("Mouse");

			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
			Assert.That(Mouse.current, Is.Not.Null);
			Assert.That(Pointer.current, Is.SameAs(Mouse.current));
		}

		[Test]
		public void NothingIsProcessedUntilTheInputUpdate()
		{
			var mouse = _session.GetPointer("Mouse");

			mouse.MoveTo(new Vector2(100, 200));
			mouse.Press();

			Assert.That(Mouse.current.position.ReadValue(), Is.EqualTo(Vector2.zero));
			Assert.That(Mouse.current.leftButton.isPressed, Is.False);
			Assert.That(_changes, Is.Empty);
		}

		[Test]
		public void EventsAreBuiltFromWhatTheStateWillBeAndNotFromTheStateOfTheDevice()
		{
			var mouse = _session.GetPointer("Mouse");

			mouse.Press("rightButton");
			mouse.MoveTo(new Vector2(100, 200));
			mouse.Press();
			InputSystem.Update();

			Assert.That(Mouse.current.rightButton.isPressed, Is.True, "not undone by the event that moved the mouse");
			Assert.That(Mouse.current.leftButton.isPressed, Is.True);
			Assert.That(Mouse.current.position.ReadValue(), Is.EqualTo(new Vector2(100, 200)));
			Assert.That(mouse.Position, Is.EqualTo(new Vector2(100, 200)));
		}

		[Test]
		public void EachCallIsAnEventOfItsOwn()
		{
			var mouse = _session.GetPointer("Mouse");
			// A button follows wasPressedThisFrame once it was asked for it.
			Assert.That(Mouse.current.leftButton.wasPressedThisFrame, Is.False);

			mouse.Press();
			mouse.Release();
			InputSystem.Update();

			Assert.That(_changes.Select(c => c.GetType()), Is.EqualTo(new[] { typeof(ButtonDown), typeof(ButtonUp) }));
			Assert.That(_changes[0].EventId, Is.Not.EqualTo(_changes[1].EventId));
			Assert.That(_changes[0].UpdateCount, Is.EqualTo(_changes[1].UpdateCount), "the same input update");
			Assert.That(Mouse.current.leftButton.isPressed, Is.False);
			Assert.That(Mouse.current.leftButton.wasPressedThisFrame, Is.True);
		}

		[Test]
		public void QueueWritesSeveralControlsWithOneEvent()
		{
			var mouse = _session.GetPointer("Mouse");

			mouse.Queue(
				ControlValue.Of("position", new Vector2(10, 20)),
				ControlValue.Of("leftButton", 1f),
				ControlValue.Of("clickCount", 1));
			InputSystem.Update();

			Assert.That(_changes.Select(c => c.EventId).Distinct().Count(), Is.EqualTo(1), "simultaneous");
			Assert.That(_changes.OfType<ValueChanged>().Select(c => c.ControlPath), Does.Contain("position"));
			Assert.That(_changes.OfType<ButtonDown>().Select(c => c.ControlPath), Is.EqualTo(new[] { "leftButton" }));
			Assert.That(Mouse.current.clickCount.ReadValue(), Is.EqualTo(1));
		}

		[Test]
		public void MoveToWritesTheDeltaOfTheMoveAndNothingElseDoes()
		{
			var mouse = _session.GetPointer("Mouse");

			mouse.MoveTo(new Vector2(100, 50));
			InputSystem.Update();
			Assert.That(Mouse.current.delta.ReadValue(), Is.EqualTo(new Vector2(100, 50)));

			mouse.MoveTo(new Vector2(130, 40));
			InputSystem.Update();
			Assert.That(Mouse.current.delta.ReadValue(), Is.EqualTo(new Vector2(30, -10)));

			mouse.MoveTo(new Vector2(131, 40));
			mouse.MoveTo(new Vector2(133, 40));
			mouse.Press();
			InputSystem.Update();
			Assert.That(Mouse.current.delta.ReadValue(), Is.EqualTo(new Vector2(3, 0)), "the deltas of one input update add up once");

			InputSystem.Update();
			Assert.That(Mouse.current.delta.ReadValue(), Is.EqualTo(Vector2.zero), "and are gone in the next one");
			Assert.That(Mouse.current.position.ReadValue(), Is.EqualTo(new Vector2(133, 40)));
		}

		[Test]
		public void AScrollIsInTheStateOfTheInputUpdateThatProcessedItOnly()
		{
			var mouse = _session.GetPointer("Mouse");

			mouse.Scroll(new Vector2(0, -120));
			mouse.Press();
			InputSystem.Update();

			Assert.That(Mouse.current.scroll.ReadValue(), Is.EqualTo(new Vector2(0, -120)), "the press does not write it again");
			Assert.That(_changes.OfType<Scrolled>().Select(c => c.Delta), Is.EqualTo(new[] { new Vector2(0, -120) }));
			InputSystem.Update();
			Assert.That(Mouse.current.scroll.ReadValue(), Is.EqualTo(Vector2.zero));
		}

		[Test]
		public void ResetToDefaultReleasesWhatIsPressedAndKeepsThePosition()
		{
			var mouse = _session.GetPointer("Mouse");
			mouse.MoveTo(new Vector2(100, 50));
			mouse.Press();
			mouse.Press("middleButton");
			InputSystem.Update();

			mouse.ResetToDefault();
			InputSystem.Update();

			Assert.That(Mouse.current.leftButton.isPressed, Is.False);
			Assert.That(Mouse.current.middleButton.isPressed, Is.False);
			Assert.That(Mouse.current.position.ReadValue(), Is.EqualTo(new Vector2(100, 50)), "the position is not reset");
			Assert.That(mouse.Position, Is.EqualTo(new Vector2(100, 50)));
			Assert.That(_changes.OfType<ButtonUp>().Select(c => c.ControlPath), Is.EquivalentTo(new[] { "leftButton", "middleButton" }));
		}

		[Test]
		public void AResetOfThePenReturnsEverythingButThePosition()
		{
			var pen = _session.GetPointer("Pen");
			pen.Queue(
				ControlValue.Of("position", new Vector2(10, 20)),
				ControlValue.Of("pressure", 0.5f),
				ControlValue.Of("tilt", new Vector2(0.25f, 0.5f)),
				ControlValue.Of("twist", 0.75f),
				ControlValue.Of("inRange", 1f),
				ControlValue.Of("tip", 1f));
			InputSystem.Update();
			var state = Pen.current;
			Assert.That(state.inRange.isPressed, Is.True);

			pen.ResetToDefault();
			InputSystem.Update();

			Assert.That(state.pressure.ReadValue(), Is.Zero);
			Assert.That(state.tilt.ReadValue(), Is.EqualTo(Vector2.zero));
			Assert.That(state.twist.ReadValue(), Is.Zero);
			Assert.That(state.inRange.isPressed, Is.False);
			Assert.That(state.tip.isPressed, Is.False);
			Assert.That(state.position.ReadValue(), Is.EqualTo(new Vector2(10, 20)));
		}

		[Test]
		public void APenWritesPressureTiltTwistAndTheButtons()
		{
			var pen = _session.GetPointer("Pen");

			pen.Set("pressure", 0.5f);
			pen.Set("tilt", new Vector2(0.25f, -0.5f));
			pen.Press("eraser");
			pen.Press("barrel2");
			pen.Press();
			InputSystem.Update();

			var device = Pen.current;
			Assert.That(device.pressure.ReadValue(), Is.EqualTo(0.5f));
			Assert.That(device.tilt.ReadValue(), Is.EqualTo(new Vector2(0.25f, -0.5f)));
			Assert.That(device.eraser.isPressed, Is.True);
			Assert.That(device.secondBarrelButton.isPressed, Is.True);
			Assert.That(device.tip.isPressed, Is.True, "null is the tip");
		}

		[Test]
		public void TheInRangeOfAPenIsWrittenAlthoughTheReaderDoesNotReportItBecauseItIsSynthetic()
		{
			var pen = _session.GetPointer("Pen");

			pen.Set("inRange", 1f);
			InputSystem.Update();

			Assert.That(Pen.current.inRange.isPressed, Is.True);
			Assert.That(_changes, Is.Empty);

			pen.ResetToDefault();
			InputSystem.Update();

			Assert.That(Pen.current.inRange.isPressed, Is.False);
			Assert.That(_changes, Is.Empty);
		}

		[Test]
		public void TheButtonThatNullStandsForIsTheOneWithTheUsagePrimaryAction()
		{
			var mouse = _session.GetPointer("Mouse");
			var pen = _session.GetPointer("Pen");

			mouse.Press();
			pen.Press();
			InputSystem.Update();

			Assert.That(Mouse.current.leftButton.isPressed, Is.True);
			Assert.That(Pen.current.tip.isPressed, Is.True);
			Assert.That(mouse.Prediction, Is.SameAs(mouse.GetPrediction("leftButton")));
			Assert.That(mouse.Prediction, Is.Not.SameAs(mouse.GetPrediction("rightButton")));
			Assert.That(pen.Prediction, Is.SameAs(pen.GetPrediction("tip")));
		}

		[Test]
		public void AControlThatIsNotAButtonCannotBePressed()
		{
			var mouse = _session.GetPointer("Mouse");

			Assert.Throws<ArgumentException>(() => mouse.Press("position"));
			Assert.Throws<ArgumentException>(() => mouse.Press("noSuchButton"));
			Assert.Throws<ArgumentException>(() => mouse.Release("tip"));
			Assert.Throws<ArgumentException>(() => _session.GetPointer("Pen").Press("leftButton"));
		}

		[Test]
		public void APathThatIsNotAControlOrAValueThatDoesNotFitMakesTheCallThrowAndQueueNothing()
		{
			var mouse = _session.GetPointer("Mouse");
			mouse.MoveTo(new Vector2(10, 20));

			Assert.Throws<ArgumentException>(() => mouse.Set("noSuchControl", 1f));
			Assert.Throws<ArgumentException>(() => mouse.Set("position", 1f));
			Assert.Throws<ArgumentException>(() => mouse.Set("leftButton", new Vector2(1, 1)));
			Assert.Throws<ArgumentException>(() => mouse.Set("", 1f));
			Assert.Throws<ArgumentException>(
				() => mouse.Queue(ControlValue.Of("leftButton", 1f), ControlValue.Of("noSuchControl", 1f)));
			Assert.Throws<ArgumentNullException>(() => mouse.Queue(null));
			InputSystem.Update();

			Assert.That(Mouse.current.leftButton.isPressed, Is.False, "the first value of the failed Queue was not written");
			Assert.That(_changes.OfType<ButtonDown>(), Is.Empty);
		}

		[Test]
		public void ANumberOfAnotherTypeIsConvertedToTheTypeOfTheControl()
		{
			var pen = _session.GetPointer("Pen");

			pen.Set("pressure", 1);
			pen.Set("twist", 0.5);
			InputSystem.Update();

			Assert.That(Pen.current.pressure.ReadValue(), Is.EqualTo(1f));
			Assert.That(Pen.current.twist.ReadValue(), Is.EqualTo(0.5f));
		}

		[Test]
		public void AScrollNeedsAScrollControl()
		{
			var pen = _session.GetPointer("Pen");

			Assert.Throws<InvalidOperationException>(() => pen.Scroll(Vector2.up));
		}

		[Test]
		public void AResetOfTheDeviceTakesTheStateThatTheDeviceHasAfterwards()
		{
			var mouse = _session.GetPointer("Mouse");
			mouse.Press();
			mouse.Press("rightButton");
			InputSystem.Update();

			InputSystem.ResetDevice(Mouse.current);
			mouse.MoveTo(new Vector2(5, 5));
			InputSystem.Update();

			Assert.That(Mouse.current.leftButton.isPressed, Is.False, "the event is not built from the state before the reset");
			Assert.That(Mouse.current.rightButton.isPressed, Is.False);
			Assert.That(Mouse.current.position.ReadValue(), Is.EqualTo(new Vector2(5, 5)));
		}

		[Test]
		public void DisposeReleasesWhatAPointerHoldsAndRemovesTheDevicesAfterTheUiHadTime()
		{
			var mouse = _session.GetPointer("Mouse");
			mouse.Press();
			mouse.Press("rightButton");
			InputSystem.Update();

			_session.Dispose();
			_session = null;

			Assert.That(InputSystem.devices, Has.Count.EqualTo(1), "the release is only queued");
			Assert.That(Mouse.current.leftButton.isPressed, Is.True);
			InputSystem.Update();
			Assert.That(Mouse.current.leftButton.isPressed, Is.False, "the release was processed");
			Assert.That(Mouse.current.rightButton.isPressed, Is.False);
			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
			InputSystem.Update();
			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
			InputSystem.Update();
			Assert.That(InputSystem.devices, Is.Empty);
		}

		[Test]
		public void DisposeRemovesAPointerThatHoldsNothingAtOnce()
		{
			var mouse = _session.GetPointer("Mouse");
			mouse.MoveTo(new Vector2(100, 50));
			InputSystem.Update();

			_session.Dispose();
			_session = null;

			Assert.That(InputSystem.devices, Is.Empty);
		}

		[Test]
		public void AnEventThatIsStillQueuedWhenTheSessionEndsIsProcessedBeforeTheDeviceIsRemoved()
		{
			var mouse = _session.GetPointer("Mouse");
			mouse.Press();

			_session.Dispose();
			_session = null;
			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
			InputSystem.Update();

			Assert.That(_changes.OfType<ButtonDown>().Count(), Is.EqualTo(1));
			Assert.That(_changes.OfType<ButtonUp>().Count(), Is.EqualTo(1));
			Assert.That(Mouse.current.leftButton.isPressed, Is.False);
		}

		[Test]
		public void APointerIsKeptWhileTheSessionIsAcquiredAgainDuringTheDrain()
		{
			var mouse = _session.GetPointer("Mouse");
			mouse.Press();
			InputSystem.Update();
			_session.Dispose();

			_session = VirtualInput.Acquire();
			_session.Reader.Changed += _changes.Add;
			InputSystem.Update();
			InputSystem.Update();
			InputSystem.Update();
			InputSystem.Update();

			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
			Assert.That(_session.GetPointer("Mouse"), Is.SameAs(mouse));
		}
	}
}
#endif
