#if ENABLE_AUTOTAP
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// <see cref="InputChangeReader"/> on devices that <see cref="VirtualInput"/> did not add.
	/// </summary>
	public class InputChangeReaderTests : InputTestFixture
	{
		InputChangeReader _reader;
		List<InputChange> _changes;

		public override void Setup()
		{
			base.Setup();
			_reader = new InputChangeReader();
			_changes = new List<InputChange>();
			_reader.Changed += _changes.Add;
		}

		public override void TearDown()
		{
			_reader.Dispose();
			base.TearDown();
		}

		[Test]
		public void KeyPressAndReleaseAreReportedAsButtonDownAndUp()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();

			Press(keyboard.spaceKey);
			Release(keyboard.spaceKey);

			Assert.That(_changes, Has.Count.EqualTo(2));
			var down = (ButtonDown)_changes[0];
			var up = (ButtonUp)_changes[1];
			Assert.That(down.Device, Is.SameAs(keyboard));
			Assert.That(down.ControlPath, Is.EqualTo("space"));
			Assert.That(down.IsVirtual, Is.False);
			Assert.That(up.ControlPath, Is.EqualTo("space"));
			Assert.That(up.Order, Is.EqualTo(down.Order + 1));
		}

		[Test]
		public void ChangesOfDifferentDevicesAreInTheOrderTheEventsWereQueued()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();
			var mouse = InputSystem.AddDevice<Mouse>();
			var touchscreen = InputSystem.AddDevice<Touchscreen>();

			Press(keyboard.aKey, queueEventOnly: true);
			BeginTouch(1, new Vector2(10, 20), queueEventOnly: true, screen: touchscreen);
			Set(mouse.scroll, new Vector2(0, 5), queueEventOnly: true);
			Release(keyboard.aKey, queueEventOnly: true);
			InputSystem.Update();

			Assert.That(_changes, Has.Count.EqualTo(4));
			Assert.That(_changes[0], Is.TypeOf<ButtonDown>());
			Assert.That(_changes[1], Is.TypeOf<TouchBegan>());
			Assert.That(_changes[2], Is.TypeOf<Scrolled>());
			Assert.That(_changes[3], Is.TypeOf<ButtonUp>());
			for (var i = 1; i < _changes.Count; ++i)
			{
				Assert.That(_changes[i].Order, Is.EqualTo(_changes[i - 1].Order + 1));
			}
		}

		[Test]
		public void TouchesAreReportedFromTouchesAndOnceForEachEvent()
		{
			var touchscreen = InputSystem.AddDevice<Touchscreen>();

			BeginTouch(7, new Vector2(10, 20), screen: touchscreen);
			MoveTouch(7, new Vector2(30, 40), screen: touchscreen);
			EndTouch(7, new Vector2(30, 40), screen: touchscreen);

			Assert.That(_changes, Has.Count.EqualTo(3));
			var began = (TouchBegan)_changes[0];
			var moved = (TouchMoved)_changes[1];
			var ended = (TouchEnded)_changes[2];
			Assert.That(began.TouchId, Is.EqualTo(7));
			Assert.That(began.Position, Is.EqualTo(new Vector2(10, 20)));
			Assert.That(moved.Position, Is.EqualTo(new Vector2(30, 40)));
			Assert.That(moved.StartPosition, Is.EqualTo(new Vector2(10, 20)));
			Assert.That(ended.TouchId, Is.EqualTo(7));
		}

		[Test]
		public void ATouchThatBeganAndEndedInOneUpdateIsReportedTwice()
		{
			var touchscreen = InputSystem.AddDevice<Touchscreen>();

			BeginTouch(3, new Vector2(10, 20), queueEventOnly: true, screen: touchscreen);
			EndTouch(3, new Vector2(10, 20), queueEventOnly: true, screen: touchscreen);
			InputSystem.Update();

			Assert.That(_changes, Has.Count.EqualTo(2));
			Assert.That(_changes[0], Is.TypeOf<TouchBegan>());
			Assert.That(_changes[1], Is.TypeOf<TouchEnded>());
			Assert.That(_changes[0].UpdateCount, Is.EqualTo(_changes[1].UpdateCount));
		}

		[Test]
		public void TapsDoNotReportMoreThanOneChangeForOneEvent()
		{
			var touchscreen = InputSystem.AddDevice<Touchscreen>();

			BeginTouch(1, new Vector2(10, 20), screen: touchscreen);
			EndTouch(1, new Vector2(10, 20), screen: touchscreen);

			// A tap makes the touch control change twice for the ending event, and the primary touch changes too.
			Assert.That(_changes, Has.Count.EqualTo(2));
			Assert.That(_changes[1], Is.TypeOf<TouchEnded>());
		}

		[Test]
		public void TwoTouchesAreReportedSeparately()
		{
			var touchscreen = InputSystem.AddDevice<Touchscreen>();

			BeginTouch(1, new Vector2(10, 20), screen: touchscreen);
			BeginTouch(2, new Vector2(50, 60), screen: touchscreen);
			MoveTouch(1, new Vector2(11, 20), screen: touchscreen);

			Assert.That(_changes, Has.Count.EqualTo(3));
			Assert.That(((TouchChange)_changes[0]).TouchId, Is.EqualTo(1));
			Assert.That(((TouchChange)_changes[1]).TouchId, Is.EqualTo(2));
			Assert.That(_changes[2], Is.TypeOf<TouchMoved>());
			Assert.That(((TouchChange)_changes[2]).TouchId, Is.EqualTo(1));
		}

		[Test]
		public void ScrollIsReportedWithTheAmountOfTheEvent()
		{
			var mouse = InputSystem.AddDevice<Mouse>();

			Set(mouse.scroll, new Vector2(0, 3));
			Set(mouse.scroll, new Vector2(1, 4));

			var scrolls = _changes.FindAll(c => c is Scrolled);
			Assert.That(scrolls, Has.Count.EqualTo(2));
			Assert.That(((Scrolled)scrolls[0]).Delta, Is.EqualTo(new Vector2(0, 3)));
			Assert.That(((Scrolled)scrolls[1]).Delta, Is.EqualTo(new Vector2(1, 4)));
			Assert.That(((Scrolled)scrolls[0]).ControlPath, Is.EqualTo("scroll"));
		}

		[Test]
		public void TheZeroingOfDeltasAtTheStartOfAnUpdateIsNotReported()
		{
			var mouse = InputSystem.AddDevice<Mouse>();
			Set(mouse.scroll, new Vector2(0, 3));
			_changes.Clear();

			InputSystem.Update();
			InputSystem.Update();

			Assert.That(_changes, Is.Empty);
		}

		[Test]
		public void PositionAndPressureAreValueChanges()
		{
			var mouse = InputSystem.AddDevice<Mouse>();

			Set(mouse.position, new Vector2(100, 200));

			var changes = _changes.FindAll(c => c is ValueChanged);
			Assert.That(changes, Has.Count.EqualTo(1));
			var position = (ValueChanged)changes[0];
			Assert.That(position.ControlPath, Is.EqualTo("position"));
			Assert.That(position.PreviousValue, Is.EqualTo(Vector2.zero));
			Assert.That(position.Value, Is.EqualTo(new Vector2(100, 200)));
		}

		[Test]
		public void GamepadButtonsAndSticksAreReported()
		{
			var gamepad = InputSystem.AddDevice<Gamepad>();

			Press(gamepad.buttonSouth);
			Set(gamepad.leftStick, new Vector2(1, 0));

			Assert.That(_changes[0], Is.TypeOf<ButtonDown>());
			Assert.That(((ButtonDown)_changes[0]).ControlPath, Is.EqualTo("buttonSouth"));
			var stick = _changes.Find(c => c is ValueChanged v && v.ControlPath == "leftStick");
			Assert.That(stick, Is.Not.Null);
			Assert.That(((ValueChanged)stick).Value, Is.EqualTo(new Vector2(1, 0)));
			Assert.That(_changes.FindAll(c => c is ValueChanged v && v.ControlPath.StartsWith("leftStick/")), Is.Empty);
		}

		[Test]
		public void TextAndCompositionAreReportedInOrderWithTheOtherChanges()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();

			Press(keyboard.aKey, queueEventOnly: true);
			InputSystem.QueueTextEvent(keyboard, 'a');
			var composition = IMECompositionEvent.Create(keyboard.deviceId, "ni", InputState.currentTime);
			InputSystem.QueueEvent(ref composition);
			Release(keyboard.aKey, queueEventOnly: true);
			InputSystem.Update();

			Assert.That(_changes, Has.Count.EqualTo(4));
			Assert.That(_changes[0], Is.TypeOf<ButtonDown>());
			Assert.That(((TextEntered)_changes[1]).Text, Is.EqualTo("a"));
			Assert.That(((CompositionChanged)_changes[2]).Composition, Is.EqualTo("ni"));
			Assert.That(_changes[3], Is.TypeOf<ButtonUp>());
		}

		[Test]
		public void EventsOfADisabledDeviceAreNotReported()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();
			InputSystem.DisableDevice(keyboard);

			Press(keyboard.aKey);

			Assert.That(_changes, Is.Empty);
		}

		[Test]
		public void TheResetOfADeviceIsReportedOnceInsteadOfTheControls()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();
			Press(keyboard.aKey);
			Press(keyboard.bKey);
			_changes.Clear();

			InputSystem.ResetDevice(keyboard, alsoResetDontResetControls: true);

			Assert.That(_changes, Has.Count.EqualTo(1), string.Join(", ", _changes.ConvertAll(c => $"{c.GetType().Name}(event {c.EventId})")));
			Assert.That(_changes[0], Is.TypeOf<DeviceReset>());
		}

		[Test]
		public void TheResetOfATouchscreenIsReportedOnce()
		{
			var touchscreen = InputSystem.AddDevice<Touchscreen>();
			BeginTouch(1, new Vector2(10, 20), screen: touchscreen);
			BeginTouch(2, new Vector2(30, 20), screen: touchscreen);
			_changes.Clear();

			InputSystem.ResetDevice(touchscreen);

			Assert.That(_changes, Has.Count.EqualTo(1));
			Assert.That(_changes[0], Is.TypeOf<DeviceReset>());
		}

		[Test]
		public void TheFilterSelectsTheDevices()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();
			var mouse = InputSystem.AddDevice<Mouse>();
			using (var reader = new InputChangeReader(device => device is Keyboard))
			{
				var filtered = new List<InputChange>();
				reader.Changed += filtered.Add;

				Press(mouse.leftButton);
				Press(keyboard.aKey);

				Assert.That(filtered, Has.Count.EqualTo(1));
				Assert.That(filtered[0].Device, Is.SameAs(keyboard));
			}
		}

		[Test]
		public void UpdateEndedIsRaisedAfterTheChangesOfTheUpdate()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();
			var log = new List<string>();
			_reader.Changed += c => log.Add("change");
			_reader.UpdateEnded += () => log.Add("end");

			Press(keyboard.aKey);

			Assert.That(log, Is.EqualTo(new[] { "change", "end" }));
		}

		[Test]
		public void NothingIsReportedAfterDispose()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();
			_reader.Dispose();

			Press(keyboard.aKey);

			Assert.That(_changes, Is.Empty);
		}

		[Test]
		public void ControlChangeCarriesTheControl()
		{
			var keyboard = InputSystem.AddDevice<Keyboard>();

			Press(keyboard.aKey);

			Assert.That(((ControlChange)_changes[0]).Control, Is.SameAs(keyboard.aKey));
		}
	}
}
#endif
