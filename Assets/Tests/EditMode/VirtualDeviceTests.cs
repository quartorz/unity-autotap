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
	/// <see cref="VirtualDevice"/>, which queues events to a virtual keyboard, gamepad, joystick or sensor, on the
	/// Input System of <see cref="InputTestFixture"/>.
	/// </summary>
	public class VirtualDeviceTests : InputTestFixture
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
		public void TheSameLayoutAndNumberGiveTheSameDevice()
		{
			var keyboard = _session.GetDevice("Keyboard");

			Assert.That(_session.GetDevice("Keyboard"), Is.SameAs(keyboard));
			Assert.That(_session.GetDevice("Keyboard", 1), Is.Not.SameAs(keyboard));
			Assert.That(keyboard.Device, Is.InstanceOf<Keyboard>());
			Assert.That(keyboard.Device.name, Is.EqualTo("AutoTap-Keyboard-0"));
			Assert.That(_session.GetDevice("Gamepad").Device, Is.InstanceOf<Gamepad>());
			Assert.That(VirtualInput.IsVirtual(keyboard.Device), Is.True);
		}

		[Test]
		public void ATypeAndTheNameOfItsLayoutGiveTheSameDevice()
		{
			var keyboard = _session.GetDevice<Keyboard>();
			var pad = _session.GetDevice("Gamepad");

			Assert.That(_session.GetDevice("Keyboard"), Is.SameAs(keyboard), "added by the type, given by the name");
			Assert.That(_session.GetDevice("keyboard"), Is.SameAs(keyboard), "layout names do not depend on the case");
			Assert.That(_session.GetDevice<Gamepad>(), Is.SameAs(pad), "added by the name, given by the type");
			Assert.That(_session.GetDevice<Keyboard>(1), Is.SameAs(_session.GetDevice("Keyboard", 1)));
			Assert.That(_session.GetDevice<Keyboard>(1), Is.Not.SameAs(keyboard));
			Assert.That(keyboard.Device, Is.InstanceOf<Keyboard>());
			Assert.That(keyboard.Device.name, Is.EqualTo("AutoTap-Keyboard-0"));
			Assert.That(InputSystem.devices.Count(VirtualInput.IsVirtual), Is.EqualTo(3), "Keyboard 0, Keyboard 1 and Gamepad 0");
		}

		[Test]
		public void APointerTypeIsRefusedAndNothingIsLeftBehind()
		{
			Assert.Throws<ArgumentException>(() => _session.GetDevice<Mouse>());
			Assert.Throws<ArgumentException>(() => _session.GetDevice<Pen>());
			Assert.Throws<ArgumentException>(() => _session.GetDevice<Touchscreen>());
			_session.GetPointer<Mouse>();
			Assert.Throws<ArgumentException>(() => _session.GetDevice<Mouse>(), "the mouse of that number was given as a pointer");

			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
		}

		[Test]
		public void PointersAreGivenByGetPointerAndNothingIsLeftBehindWhenItFails()
		{
			Assert.Throws<ArgumentException>(() => _session.GetDevice("Mouse"));
			Assert.Throws<ArgumentException>(() => _session.GetDevice("Pen"));
			Assert.Throws<ArgumentException>(() => _session.GetDevice("Touchscreen"));
			Assert.Throws<ArgumentException>(() => _session.GetDevice("NoSuchLayout"));
			Assert.Throws<ArgumentException>(() => _session.GetDevice(""));
			_session.GetPointer("Mouse");
			Assert.Throws<ArgumentException>(() => _session.GetDevice("Mouse"), "the mouse of that number was given as a pointer");
			Assert.Throws<ArgumentException>(() => _session.GetPointer("Keyboard"));

			Assert.That(InputSystem.devices, Has.Count.EqualTo(1));
		}

		[Test]
		public void TheDeviceIsAddedWhenItIsGivenAndMakesItselfCurrent()
		{
			Assert.That(Gamepad.all, Is.Empty);

			var pad = _session.GetDevice("Gamepad");

			Assert.That(Gamepad.all, Has.Count.EqualTo(1));
			Assert.That(Gamepad.current, Is.SameAs(pad.Device));
		}

		[Test]
		public void EventsAreBuiltFromWhatTheStateWillBeSoKeysPressedByEarlierEventsStay()
		{
			var keyboard = _session.GetDevice("Keyboard");

			keyboard.Set("leftCtrl", 1f);
			keyboard.Set("r", 1f);
			InputSystem.Update();

			Assert.That(Keyboard.current.leftCtrlKey.isPressed, Is.True);
			Assert.That(Keyboard.current.rKey.isPressed, Is.True);
			Assert.That(_changes.OfType<ButtonDown>().Select(c => c.ControlPath), Is.EqualTo(new[] { "leftCtrl", "r" }));
			Assert.That(_changes[0].EventId, Is.Not.EqualTo(_changes[1].EventId));
		}

		[Test]
		public void AKeyIsPressedAndReleasedByTheEventsInTheOrderTheyWereQueued()
		{
			var keyboard = _session.GetDevice("Keyboard");

			keyboard.Set("escape", 1f);
			keyboard.Set("escape", 0f);
			keyboard.Set("escape", 1f);
			InputSystem.Update();

			Assert.That(_changes.Select(c => c.GetType()), Is.EqualTo(new[] { typeof(ButtonDown), typeof(ButtonUp), typeof(ButtonDown) }));
			Assert.That(Keyboard.current.escapeKey.isPressed, Is.True);
		}

		[Test]
		public void ABoxedNumberIsConvertedToTheTypeOfTheControl()
		{
			var pad = _session.GetDevice("Gamepad");

			pad.Set("leftTrigger", 1);
			pad.Set("buttonSouth", true);
			InputSystem.Update();

			Assert.That(Gamepad.current.leftTrigger.ReadValue(), Is.EqualTo(1f));
			Assert.That(Gamepad.current.buttonSouth.isPressed, Is.True);
		}

		[Test]
		public void AGamepadStickAndButtonsAreWritten()
		{
			var pad = _session.GetDevice("Gamepad");

			pad.Set("leftStick", new Vector2(0.5f, -0.5f));
			pad.Set("buttonSouth", 1f);
			pad.Set("dpad", Vector2.up);
			pad.Set("rightStick/x", 1f);
			InputSystem.Update();

			var gamepad = Gamepad.current;
			Assert.That(gamepad.leftStick.ReadUnprocessedValue().x, Is.EqualTo(0.5f).Within(0.001f));
			Assert.That(gamepad.leftStick.ReadUnprocessedValue().y, Is.EqualTo(-0.5f).Within(0.001f));
			Assert.That(gamepad.buttonSouth.isPressed, Is.True);
			Assert.That(gamepad.dpad.up.isPressed, Is.True);
			Assert.That(gamepad.rightStick.ReadUnprocessedValue(), Is.EqualTo(new Vector2(1, 0)));
			Assert.That(_changes.OfType<ValueChanged>().Select(c => c.ControlPath), Does.Contain("leftStick").And.Contain("rightStick"));
			Assert.That(_changes.OfType<ButtonDown>().Select(c => c.ControlPath), Does.Contain("buttonSouth").And.Contain("dpad/up"));
		}

		[Test]
		public void QueueWritesSeveralControlsWithOneEvent()
		{
			var pad = _session.GetDevice("Gamepad");

			pad.Queue(
				ControlValue.Of("leftStick", new Vector2(1, 0)),
				ControlValue.Of("buttonEast", 1f));
			InputSystem.Update();

			Assert.That(_changes.Select(c => c.EventId).Distinct().Count(), Is.EqualTo(1));
			Assert.That(_changes.OfType<ButtonDown>().Select(c => c.ControlPath), Is.EqualTo(new[] { "buttonEast" }));
			Assert.That(_changes.OfType<ValueChanged>().Select(c => c.ControlPath), Does.Contain("leftStick"));
		}

		[Test]
		public void ResetToDefaultReleasesAndCentersWhatWasWritten()
		{
			var pad = _session.GetDevice("Gamepad");
			pad.Queue(
				ControlValue.Of("leftStick", new Vector2(1, 0)),
				ControlValue.Of("buttonSouth", 1f),
				ControlValue.Of("rightTrigger", 1f));
			InputSystem.Update();
			Assert.That(Gamepad.current.buttonSouth.isPressed, Is.True);

			pad.ResetToDefault();
			InputSystem.Update();

			Assert.That(Gamepad.current.buttonSouth.isPressed, Is.False);
			Assert.That(Gamepad.current.leftStick.ReadUnprocessedValue(), Is.EqualTo(Vector2.zero));
			Assert.That(Gamepad.current.rightTrigger.ReadValue(), Is.Zero);
		}

		[Test]
		public void AJoystickIsWrittenThroughThePathsOfItsControls()
		{
			var joystick = _session.GetDevice("Joystick");

			joystick.Set("stick", new Vector2(0.5f, 0.25f));
			joystick.Set("trigger", 1f);
			InputSystem.Update();

			var device = Joystick.current;
			Assert.That(device, Is.Not.Null);
			Assert.That(Joystick.all, Has.Count.EqualTo(1));
			Assert.That(device.stick.ReadUnprocessedValue().x, Is.EqualTo(0.5f).Within(0.01f));
			Assert.That(device.stick.ReadUnprocessedValue().y, Is.EqualTo(0.25f).Within(0.01f));
			Assert.That(device.trigger.isPressed, Is.True);
			Assert.That(
				_changes.OfType<ValueChanged>().Select(c => c.ControlPath), Is.EqualTo(new[] { "stick" }),
				"the reader reports a stick as one change, not as stick/x and stick/y");
		}

		[Test]
		public void ASensorIsWrittenAndKeepsItsValueWhenItIsReset()
		{
			var accelerometer = _session.GetDevice("Accelerometer");

			accelerometer.Set("acceleration", new Vector3(0.25f, -1f, 0.5f));
			InputSystem.Update();
			Assert.That(Accelerometer.current.acceleration.ReadValue(), Is.EqualTo(new Vector3(0.25f, -1f, 0.5f)));

			accelerometer.ResetToDefault();
			InputSystem.Update();

			// The values of a sensor are noise for the Input System, which a reset does not touch.
			Assert.That(Accelerometer.current.acceleration.ReadValue(), Is.EqualTo(new Vector3(0.25f, -1f, 0.5f)));
		}

		[Test]
		public void AnAttitudeSensorTakesAQuaternion()
		{
			var attitude = _session.GetDevice("AttitudeSensor");
			var rotation = Quaternion.Euler(10, 20, 30);

			attitude.Set("attitude", rotation);
			InputSystem.Update();

			var read = AttitudeSensor.current.attitude.ReadValue();
			Assert.That(Quaternion.Angle(read, rotation), Is.LessThan(0.1f));
			Assert.That(_changes.OfType<ValueChanged>().Select(c => c.ControlPath), Is.EqualTo(new[] { "attitude" }));
		}

		[Test]
		public void TextIsOneEventPerCharacterAndKeepsTheOrderWithTheKeys()
		{
			var keyboard = _session.GetDevice("Keyboard");
			var typed = new List<char>();
			Keyboard.current.onTextInput += typed.Add;

			keyboard.Set("a", 1f);
			keyboard.Text("ab");
			keyboard.Set("a", 0f);
			InputSystem.Update();

			Assert.That(typed, Is.EqualTo(new[] { 'a', 'b' }));
			Assert.That(
				_changes.Select(c => c.GetType()),
				Is.EqualTo(new[] { typeof(ButtonDown), typeof(TextEntered), typeof(TextEntered), typeof(ButtonUp) }));
			Assert.That(_changes.OfType<TextEntered>().Select(c => c.Text), Is.EqualTo(new[] { "a", "b" }));
			Assert.That(_changes.Select(c => c.Order), Is.Ordered);
		}

		[Test]
		public void ACharacterOutsideTheBasicPlaneIsOneTextEventForTheReaderAndTwoCharactersForTheKeyboard()
		{
			var keyboard = _session.GetDevice("Keyboard");
			var typed = new List<char>();
			Keyboard.current.onTextInput += typed.Add;

			keyboard.Text("\U0001F600");
			InputSystem.Update();

			Assert.That(_changes.OfType<TextEntered>().Select(c => c.Text), Is.EqualTo(new[] { "\U0001F600" }));
			Assert.That(typed, Has.Count.EqualTo(2));
		}

		[Test]
		public void ACompositionIsOneEventAndAnEmptyStringEndsIt()
		{
			var keyboard = _session.GetDevice("Keyboard");
			var compositions = new List<string>();
			Keyboard.current.onIMECompositionChange += c => compositions.Add(c.ToString());

			keyboard.Composition("か");
			keyboard.Composition("かな");
			keyboard.Composition("");
			InputSystem.Update();

			Assert.That(compositions, Is.EqualTo(new[] { "か", "かな", "" }));
			Assert.That(_changes.OfType<CompositionChanged>().Select(c => c.Composition), Is.EqualTo(new[] { "か", "かな", "" }));
		}

		[Test]
		public void OnlyADeviceThatReceivesTextTakesTextAndCompositions()
		{
			var pad = _session.GetDevice("Gamepad");

			Assert.Throws<InvalidOperationException>(() => pad.Text("a"));
			Assert.Throws<InvalidOperationException>(() => pad.Composition("a"));
			Assert.Throws<ArgumentNullException>(() => _session.GetDevice("Keyboard").Text(null));
			Assert.Throws<ArgumentNullException>(() => _session.GetDevice("Keyboard").Composition(null));
			Assert.Throws<ArgumentException>(() => _session.GetDevice("Keyboard").Composition(new string('a', 64)));
		}

		[Test]
		public void EmptyTextQueuesNothing()
		{
			var keyboard = _session.GetDevice("Keyboard");

			keyboard.Text("");
			InputSystem.Update();

			Assert.That(_changes, Is.Empty);
		}

		[Test]
		public void APathThatIsNotAControlOrAValueThatDoesNotFitMakesTheCallThrowAndQueueNothing()
		{
			var pad = _session.GetDevice("Gamepad");

			Assert.Throws<ArgumentException>(() => pad.Set("noSuchControl", 1f));
			Assert.Throws<ArgumentException>(() => pad.Set("leftStick", 1f));
			Assert.Throws<ArgumentException>(() => pad.Set("buttonSouth", new Vector2(1, 1)));
			Assert.Throws<ArgumentException>(() => pad.Set("", 1f));
			Assert.Throws<ArgumentException>(
				() => pad.Queue(ControlValue.Of("buttonSouth", 1f), ControlValue.Of("noSuchControl", 1f)));
			InputSystem.Update();

			Assert.That(Gamepad.current.buttonSouth.isPressed, Is.False);
			Assert.That(_changes, Is.Empty);
		}

		[Test]
		public void DisposeReleasesWhatADeviceHoldsAndRemovesTheDevicesAfterTheUiHadTime()
		{
			var pad = _session.GetDevice("Gamepad");
			var keyboard = _session.GetDevice("Keyboard");
			pad.Set("buttonSouth", 1f);
			pad.Set("leftStick", Vector2.one);
			keyboard.Set("escape", 1f);
			InputSystem.Update();

			_session.Dispose();
			_session = null;

			Assert.That(InputSystem.devices, Has.Count.EqualTo(2), "the releases are only queued");
			InputSystem.Update();
			Assert.That(Gamepad.current.buttonSouth.isPressed, Is.False);
			Assert.That(Gamepad.current.leftStick.ReadUnprocessedValue(), Is.EqualTo(Vector2.zero));
			Assert.That(Keyboard.current.escapeKey.isPressed, Is.False);
			Assert.That(InputSystem.devices, Has.Count.EqualTo(2));
			InputSystem.Update();
			Assert.That(InputSystem.devices, Has.Count.EqualTo(2));
			InputSystem.Update();
			Assert.That(InputSystem.devices, Is.Empty);
		}

		[Test]
		public void DisposeRemovesDevicesThatHoldNothingAtOnce()
		{
			_session.GetDevice("Gamepad").Set("buttonSouth", 1f);
			_session.GetDevice("Gamepad").Set("buttonSouth", 0f);
			InputSystem.Update();

			_session.Dispose();
			_session = null;

			Assert.That(InputSystem.devices, Is.Empty);
		}

		[Test]
		public void TheEventsOfADeviceAndOfAPointerKeepTheOrderTheyWereQueuedIn()
		{
			var keyboard = _session.GetDevice("Keyboard");
			var mouse = _session.GetPointer("Mouse");

			keyboard.Set("a", 1f);
			mouse.Press();
			keyboard.Text("x");
			mouse.Scroll(new Vector2(0, 1));
			keyboard.Set("a", 0f);
			InputSystem.Update();

			Assert.That(
				_changes.Select(c => c.GetType().Name),
				Is.EqualTo(new[] { "ButtonDown", "ButtonDown", "TextEntered", "Scrolled", "ButtonUp" }));
			Assert.That(_changes.Select(c => c.Device).Distinct().Count(), Is.EqualTo(2));
			Assert.That(_changes.Select(c => c.Order), Is.Ordered);
		}
	}
}
#endif
