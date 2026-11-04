#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// The operations of devices that have no position (a keyboard, a gamepad, a joystick, a sensor), on the Input
	/// System of <see cref="InputTestFixture"/>. One frame is an input update, the event system, and
	/// <see cref="AutoTapBase.Update(float)"/>, as in the player loop.
	/// </summary>
	public class AutoTapDeviceTests : InputTestFixture
	{
		const float k_Dt = 0.04f;

		TestAutoTap _autoTap;
		TestScene _scene;
		InputChangeReader _reader;
		List<InputChange> _changes;
		List<UiEvent> _ui;
		readonly List<AutoTapBase.LogEvent> _allLogs = new List<AutoTapBase.LogEvent>();

		public override void Setup()
		{
			base.Setup();
			_scene = new TestScene(withInputModule: true);
			_autoTap = new TestAutoTap();
			_autoTap.SetupMarkers(_scene.Canvas, null);
			_autoTap.Start();
			_reader = new InputChangeReader();
			_changes = new List<InputChange>();
			_reader.Changed += _changes.Add;
			_ui = new List<UiEvent>();
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
			_allLogs.AddRange(_autoTap.GetLatestLogs());
			_scene.ProcessUi();
			_autoTap.Update(dt);
		}

		void Frames(int count)
		{
			for (var i = 0; i < count; ++i)
			{
				Frame();
			}
		}

		void RunUntilCompleted(AutoTapBase.LogItem item)
		{
			for (var i = 0; i < 100 && !item.Completed; ++i)
			{
				Frame();
			}

			Assert.That(item.Completed, Is.True);
			Frame();
		}

		Recorder AddSelected(string name)
		{
			var recorder = _scene.Add<Recorder>(name, 0.1f, 0.1f, 0.3f, 0.3f);
			recorder.Events = _ui;
			_scene.EventSystem.SetSelectedGameObject(recorder.gameObject);
			_ui.Clear();
			return recorder;
		}

		string[] UiOf(string name)
		{
			return _ui.Where(e => e.Name == name).Select(e => e.Kind).ToArray();
		}

		InputAction BackAction(List<int> frames)
		{
			var action = new InputAction("back", InputActionType.Button, "*/{Cancel}");
			action.performed += _ => frames.Add(_frame);
			action.Enable();
			return action;
		}

		int _frame;

		[Test]
		public void APressedKeyIsReleasedInALaterInputUpdateAndEndsTheOperationOnceTheReleaseWasProcessed()
		{
			var item = _autoTap.Key(Key.Space);

			Assert.That(item, Is.Not.Null);
			Assert.That(item.Kind, Is.EqualTo(AutoTapBase.LogItemKind.Device));
			Assert.That(item.Index, Is.EqualTo(-1));
			Assert.That(item.Device, Is.InstanceOf<Keyboard>());
			Assert.That(item.Sequence, Is.Not.Null);
			Assert.That(item.Gesture, Is.Null);
			Assert.That(Keyboard.current, Is.Not.Null, "the keyboard was added by Fire");
			Assert.That(Keyboard.current.spaceKey.isPressed, Is.False, "nothing is processed by Fire");
			Frame();
			Assert.That(Keyboard.current.spaceKey.isPressed, Is.True);
			Assert.That(item.Order, Is.Not.Null);
			Assert.That(item.Completed, Is.False);
			Frame();

			Assert.That(Keyboard.current.spaceKey.isPressed, Is.False);
			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(item.CompletedOrder, Is.GreaterThan(item.Order));
			Assert.That(_changes.Select(c => c.GetType()), Is.EqualTo(new[] { typeof(ButtonDown), typeof(ButtonUp) }));
			Assert.That(_changes.OfType<ButtonDown>().Single().ControlPath, Is.EqualTo("space"));
			Assert.That(_allLogs.Select(e => e.Kind), Is.EqualTo(new[]
			{
				AutoTapBase.LogEventKind.Started,
				AutoTapBase.LogEventKind.Completed,
			}));
		}

		[Test]
		public void AKeyIsNeverPressedAndReleasedByTheSameInputUpdate()
		{
			var item = _autoTap.Key(Key.Space);

			RunUntilCompleted(item);

			var down = _changes.OfType<ButtonDown>().Single();
			var up = _changes.OfType<ButtonUp>().Single();
			Assert.That(up.UpdateCount, Is.GreaterThan(down.UpdateCount));
		}

		[Test]
		public void TheDurationOfAKeyIsHeld()
		{
			var item = _autoTap.Key(Key.Space, 0.1f);

			Frame();
			Frame();
			Frame();
			Assert.That(Keyboard.current.spaceKey.isPressed, Is.True, "held for 0.1 s: 0.04, 0.08");
			RunUntilCompleted(item);

			Assert.That(Keyboard.current.spaceKey.isPressed, Is.False);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void ModifiersArePressedBeforeTheKeyAndReleasedAfterIt()
		{
			var item = _autoTap.Key(Key.R, 0.04f, Key.LeftCtrl, Key.LeftShift);

			RunUntilCompleted(item);

			Assert.That(
				_changes.Select(c => $"{c.GetType().Name} {((ControlChange)c).ControlPath}"),
				Is.EqualTo(new[]
				{
					"ButtonDown leftCtrl", "ButtonDown leftShift", "ButtonDown r", "ButtonUp r", "ButtonUp leftShift", "ButtonUp leftCtrl",
				}));
			Assert.That(_changes.Select(c => c.EventId).Distinct().Count(), Is.EqualTo(6), "every step is an event of its own");
		}

		[Test]
		public void FireQueuesEveryStepAtTheStartThatTakesNoTimeAsAnEventOfItsOwn()
		{
			var item = _autoTap.Key(Key.R, 0.2f, Key.LeftCtrl, Key.LeftShift);

			Assert.That(_changes, Is.Empty, "Fire processes nothing");
			Frame();

			Assert.That(
				_changes.Select(c => ((ControlChange)c).ControlPath), Is.EqualTo(new[] { "leftCtrl", "leftShift", "r" }),
				"in the order of the plan, although only the first step comes first");
			Assert.That(_changes.Select(c => c.UpdateCount).Distinct().Count(), Is.EqualTo(1), "one input update processes them all");
			Assert.That(_changes.Select(c => c.EventId).Distinct().Count(), Is.EqualTo(3), "each is an event of its own");
			Assert.That(item.Completed, Is.False);
		}

		[Test]
		public void AStepThatTakesNoTimeIsQueuedByFireWhereverItIsInTheSequence()
		{
			var item = _autoTap.Device(
				"Gamepad", new ControlSequence().Hold(0f).Set("leftStick", Vector2.right).Press(GamepadButton.South).Hold(0.2f));

			Frame();

			Assert.That(_changes.Select(c => ((ControlChange)c).ControlPath), Is.EqualTo(new[] { "leftStick", "buttonSouth" }));
			Assert.That(_changes.Select(c => c.UpdateCount).Distinct().Count(), Is.EqualTo(1));
			Assert.That(item.Order, Is.EqualTo(_changes.First().Order));
		}

		[Test]
		public void AReleaseThatFollowsAPressOfTheSameFrameWaitsForTheNextInputUpdate()
		{
			var item = _autoTap.Device(
				"Keyboard", new ControlSequence().Press(Key.A).Release(Key.A).Press(Key.B).Release(Key.B));

			RunUntilCompleted(item);

			var changes = _changes.OfType<ControlChange>().ToList();
			Assert.That(
				changes.Select(c => $"{c.GetType().Name} {c.ControlPath}"),
				Is.EqualTo(new[] { "ButtonDown a", "ButtonUp a", "ButtonDown b", "ButtonUp b" }));
			Assert.That(changes[0].UpdateCount, Is.LessThan(changes[1].UpdateCount), "the key is down for one input update at least");
			Assert.That(changes[1].UpdateCount, Is.EqualTo(changes[2].UpdateCount), "the release goes with the next press, in that order");
			Assert.That(changes[2].UpdateCount, Is.LessThan(changes[3].UpdateCount));
		}

		[Test]
		public void AModifierIsPressedLongBeforeTheKeyForOneModifierShortcuts()
		{
			InputSystem.settings.shortcutKeysConsumeInput = true;
			var performed = new List<int>();
			var shortcut = new InputAction("restart", InputActionType.Button);
			shortcut.AddCompositeBinding("OneModifier")
				.With("Modifier", "<Keyboard>/ctrl")
				.With("Binding", "<Keyboard>/r");
			shortcut.performed += _ => performed.Add(1);
			shortcut.Enable();
			try
			{
				RunUntilCompleted(_autoTap.Key(Key.R, 0.08f, Key.LeftCtrl));
				Assert.That(performed, Has.Count.EqualTo(1), "ctrl first");

				// The key first: the modifier of the shortcut is pressed after it, which an ordered shortcut rejects.
				performed.Clear();
				var wrongOrder = new ControlSequence().Press(Key.R).Hold(0.04f).Press(Key.LeftCtrl).Hold(0.04f);
				RunUntilCompleted(_autoTap.Device("Keyboard", wrongOrder));
				Assert.That(performed, Is.Empty, "the key first");
			}
			finally
			{
				shortcut.Disable();
				shortcut.Dispose();
			}
		}

		[Test]
		public void TheBackKeyCancelsTheSelectedObject()
		{
			AddSelected("Dialog");

			RunUntilCompleted(_autoTap.Key(Key.Escape));

			// The module delivers a cancel in every frame after the press in which the action was performed, and the
			// frame count of a test does not advance, so it is only the delivery that is compared here.
			Assert.That(UiOf("Dialog"), Does.Contain("cancel").And.Not.Contain("submit"));
		}

		[Test]
		public void TheBackKeyCanBePressedOnceEveryTwoFramesAndEveryPressIsACancel()
		{
			AddSelected("Dialog");
			var performed = new List<int>();
			var action = new InputAction("back", InputActionType.Button, "*/{Cancel}");
			action.performed += _ => performed.Add(_frame);
			action.Enable();
			var fired = new List<int>();
			_autoTap.PreUpdate = _ =>
			{
				if (_autoTap.Key(Key.Escape) != null)
				{
					fired.Add(_frame);
				}
			};

			try
			{
				for (_frame = 0; _frame < 21; ++_frame)
				{
					Frame();
				}
			}
			finally
			{
				action.Disable();
				action.Dispose();
			}

			var intervals = fired.Zip(fired.Skip(1), (a, b) => b - a).Distinct().ToArray();
			TestContext.WriteLine($"Escape was fired in frames {string.Join(", ", fired)}; the intervals are {string.Join(", ", intervals)}");
			Assert.That(intervals, Is.EqualTo(new[] { 2 }), "once per two frames at best");
			Assert.That(fired, Has.Count.GreaterThan(8));
			Assert.That(
				performed, Has.Count.EqualTo(fired.Count - 1),
				$"the action is performed for every press that was processed; fired in {string.Join(",", fired)}, performed in {string.Join(",", performed)}");
			Assert.That(performed.Zip(performed.Skip(1), (a, b) => b - a).Distinct().ToArray(), Is.EqualTo(new[] { 2 }));
			Assert.That(UiOf("Dialog"), Does.Contain("cancel"));
		}

		[Test]
		public void OneSequenceThatPressesTheBackKeyAgainAndAgainPressesItOnceEveryFrame()
		{
			// A sequence is one operation, so the device is not busy between its presses. A release is made in a later
			// input update than its press, but it can share an input update with the next press.
			var performed = new List<int>();
			var action = new InputAction("back", InputActionType.Button, "*/{Cancel}");
			action.performed += _ => performed.Add(_frame);
			action.Enable();
			var sequence = new ControlSequence().Press(Key.Escape);
			for (var i = 0; i < 4; ++i)
			{
				sequence.Release(Key.Escape).Press(Key.Escape);
			}

			sequence.Release(Key.Escape);
			var item = _autoTap.Device("Keyboard", sequence);

			try
			{
				for (_frame = 0; _frame < 8; ++_frame)
				{
					Frame();
				}
			}
			finally
			{
				action.Disable();
				action.Dispose();
			}

			TestContext.WriteLine($"The back key was pressed in the frames {string.Join(", ", performed)}");
			Assert.That(performed, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }), "five presses, one per frame");
			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			var presses = _changes.OfType<ButtonDown>().ToList();
			var releases = _changes.OfType<ButtonUp>().ToList();
			Assert.That(presses, Has.Count.EqualTo(5));
			Assert.That(releases, Has.Count.EqualTo(5));
			for (var i = 0; i < 5; ++i)
			{
				Assert.That(releases[i].UpdateCount, Is.GreaterThan(presses[i].UpdateCount), $"the release of press {i} is in a later input update");
			}
		}

		[Test]
		public void TheBackKeyOfTheDeviceItselfCanBePressedAndReleasedInOneInputUpdate()
		{
			var performed = new List<int>();
			var action = new InputAction("back", InputActionType.Button, "*/{Cancel}");
			action.performed += _ => performed.Add(1);
			action.Enable();
			var keyboard = VirtualInput.Acquire().GetDevice("Keyboard");
			try
			{
				for (var i = 0; i < 3; ++i)
				{
					keyboard.Set("escape", 1f);
					keyboard.Set("escape", 0f);
				}

				InputSystem.Update();

				Assert.That(performed, Has.Count.EqualTo(3), "every press is performed, although they are in one input update");
			}
			finally
			{
				action.Disable();
				action.Dispose();
			}
		}

		[Test]
		public void AGamepadButtonIsPressedAndTheEastButtonCancels()
		{
			AddSelected("Dialog");
			var performed = new List<int>();
			var action = BackAction(performed);
			try
			{
				RunUntilCompleted(_autoTap.Gamepad(new ControlSequence().Press(GamepadButton.East)));
				Assert.That(performed, Has.Count.EqualTo(1));
				RunUntilCompleted(_autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South)));
			}
			finally
			{
				action.Disable();
				action.Dispose();
			}

			Assert.That(UiOf("Dialog"), Does.Contain("cancel").And.Contain("submit"));
			Assert.That(UiOf("Dialog").First(k => k == "cancel" || k == "submit"), Is.EqualTo("cancel"));
			Assert.That(_changes.OfType<ButtonDown>().Select(c => c.ControlPath), Is.EqualTo(new[] { "buttonEast", "buttonSouth" }));
		}

		[Test]
		public void AGamepadStickChangesOverTimeInAStraightLineAndIsCenteredAtTheEnd()
		{
			var item = _autoTap.Gamepad(new ControlSequence().Set("leftStick", new Vector2(1, 0), 0.12f).Hold(0.04f));
			var values = new List<Vector2>();

			for (var i = 0; i < 12 && !item.Completed; ++i)
			{
				Frame();
				values.Add(Gamepad.current.leftStick.ReadUnprocessedValue());
			}

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			// One event per frame: a third, two thirds and the end of the way, then back to the center.
			Assert.That(values[0].x, Is.EqualTo(0f).Within(0.001f), "the first step is not a part of the way");
			Assert.That(values[1].x, Is.EqualTo(1f / 3f).Within(0.01f));
			Assert.That(values[2].x, Is.EqualTo(2f / 3f).Within(0.01f));
			Assert.That(values[3].x, Is.EqualTo(1f).Within(0.001f));
			Assert.That(values.All(v => Mathf.Abs(v.y) < 0.001f), Is.True);
			Assert.That(Gamepad.current.leftStick.ReadUnprocessedValue(), Is.EqualTo(Vector2.zero), "reset at the end");
			Assert.That(item.Order, Is.Not.Null);
			Assert.That(item.CompletedOrder, Is.GreaterThan(item.Order));
		}

		[Test]
		public void ATriggerIsSetToAValueAndABoxedNumberIsConverted()
		{
			var item = _autoTap.Gamepad(new ControlSequence().Set("rightTrigger", 1, 0.08f).Hold(0.04f));

			Frame();
			Frame();
			var half = Gamepad.current.rightTrigger.ReadValue();
			RunUntilCompleted(item);

			Assert.That(half, Is.EqualTo(0.5f).Within(0.01f));
			Assert.That(Gamepad.current.rightTrigger.ReadValue(), Is.Zero);
		}

		[Test]
		public void WhatIsStillPressedAtTheEndIsReleasedInTheOppositeOrderAndEverythingIsReset()
		{
			var item = _autoTap.Gamepad(
				new ControlSequence().Press(GamepadButton.South).Press("buttonWest").Set("leftStick", Vector2.one).Hold(0.04f));

			Frame();
			Assert.That(Gamepad.current.buttonSouth.isPressed, Is.True);
			Assert.That(Gamepad.current.buttonWest.isPressed, Is.True);
			RunUntilCompleted(item);

			Assert.That(Gamepad.current.buttonSouth.isPressed, Is.False);
			Assert.That(Gamepad.current.buttonWest.isPressed, Is.False);
			Assert.That(Gamepad.current.leftStick.ReadUnprocessedValue(), Is.EqualTo(Vector2.zero));
			Assert.That(_changes.OfType<ButtonUp>().Select(c => c.ControlPath), Is.EqualTo(new[] { "buttonWest", "buttonSouth" }));
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void AStickHeldToTheRightMovesTheSelectionOfTheUi()
		{
			var first = AddSelected("First");
			var second = _scene.Add<Recorder>("Second", 0.5f, 0.1f, 0.7f, 0.3f);
			second.Events = _ui;

			RunUntilCompleted(_autoTap.Gamepad(new ControlSequence().Set("leftStick", Vector2.right).Hold(0.04f)));

			Assert.That(UiOf("First"), Does.Contain("move Right"));
			Assert.That(first, Is.Not.Null);
		}

		[Test]
		public void AJoystickIsOperatedThroughThePathsOfItsControls()
		{
			var item = _autoTap.Device(
				"Joystick", new ControlSequence().Set("stick", new Vector2(0.5f, 0.5f)).Press("trigger").Hold(0.04f));

			Frame();
			Assert.That(Joystick.current.trigger.isPressed, Is.True, "the trigger is pressed by the update that moved the stick");
			Assert.That(Joystick.current.stick.ReadUnprocessedValue().x, Is.EqualTo(0.5f).Within(0.01f));
			RunUntilCompleted(item);

			Assert.That(Joystick.current.trigger.isPressed, Is.False);
			Assert.That(Joystick.current.stick.ReadUnprocessedValue(), Is.EqualTo(Vector2.zero));
			Assert.That(item.Device, Is.InstanceOf<Joystick>());
		}

		[Test]
		public void ASensorIsSetAndKeepsItsValueAfterTheOperation()
		{
			var item = _autoTap.Device("Accelerometer", new ControlSequence().Set("acceleration", new Vector3(0, -1, 0)));

			RunUntilCompleted(item);

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(Accelerometer.current.acceleration.ReadValue(), Is.EqualTo(new Vector3(0, -1, 0)), "noisy controls are not reset");
			Assert.That(item.Order, Is.Not.Null);
		}

		[Test]
		public void AnAttitudeSensorChangesOverTimeBySphericalInterpolation()
		{
			var target = Quaternion.Euler(0, 90, 0);
			var item = _autoTap.Device("AttitudeSensor", new ControlSequence().Set("attitude", target, 0.08f));

			Frame();
			Frame();
			var half = AttitudeSensor.current.attitude.ReadValue();
			RunUntilCompleted(item);

			Assert.That(Quaternion.Angle(half, Quaternion.Euler(0, 45, 0)), Is.LessThan(0.5f));
			Assert.That(Quaternion.Angle(AttitudeSensor.current.attitude.ReadValue(), target), Is.LessThan(0.5f));
		}

		[Test]
		public void TextAndCompositionAreEnteredInTheOrderOfTheStepsWithTheKeys()
		{
			var typed = new List<string>();
			var item = _autoTap.Device(
				"Keyboard",
				new ControlSequence().Press(Key.A).Composition("か").Text("a").Composition("").Hold(0.04f).Release(Key.A));
			Keyboard.current.onTextInput += c => typed.Add($"text {c}");
			Keyboard.current.onIMECompositionChange += c => typed.Add($"composition {c}");
			RunUntilCompleted(item);

			Assert.That(
				_changes.Select(c => c is ControlChange cc ? $"{c.GetType().Name} {cc.ControlPath}" : c.GetType().Name),
				Is.EqualTo(new[]
				{
					"ButtonDown a", "CompositionChanged", "TextEntered", "CompositionChanged", "ButtonUp a",
				}));
			Assert.That(_changes.Select(c => c.Order), Is.Ordered);
			Assert.That(typed, Is.EqualTo(new[] { "composition か", "text a", "composition " }));
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void ASequenceThatOnlyEntersTextEndsOnceTheTextWasProcessed()
		{
			var typed = new List<char>();
			var item = _autoTap.Device("Keyboard", new ControlSequence().Text("hi"));
			Keyboard.current.onTextInput += typed.Add;

			Frame();
			Assert.That(typed, Is.EqualTo(new[] { 'h', 'i' }));
			Assert.That(item.Order, Is.Not.Null, "the text is a change of the device");
			Frame();
			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void AStepThatWaitsComesFirstAndNothingIsQueuedUntilItIsOver()
		{
			var item = _autoTap.Device("Keyboard", new ControlSequence().Hold(0.1f).Press(Key.A).Hold(0.04f));

			Frame();
			Frame();
			Assert.That(item.Order, Is.Null);
			Assert.That(Keyboard.current.aKey.isPressed, Is.False);
			RunUntilCompleted(item);

			Assert.That(_changes.OfType<ButtonDown>().Select(c => c.ControlPath), Is.EqualTo(new[] { "a" }));
			Assert.That(_changes.OfType<ButtonUp>().Select(c => c.ControlPath), Is.EqualTo(new[] { "a" }));
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void AHoldUntilThatTimesOutBeforeAnythingWasWrittenEndsWithoutAChange()
		{
			var item = _autoTap.Device("Keyboard", new ControlSequence().HoldUntil(() => false, 0.05f).Press(Key.A));

			RunUntilCompleted(item);

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.TimedOut));
			Assert.That(item.Order, Is.Null);
			Assert.That(item.CompletedOrder, Is.Null);
			Assert.That(_changes, Is.Empty);
			Assert.That(_allLogs.Count(e => e.Item == item && e.Kind == AutoTapBase.LogEventKind.Completed), Is.EqualTo(1));
		}

		[Test]
		public void AHoldUntilThatTimesOutReleasesWhatIsPressed()
		{
			var item = _autoTap.Device("Keyboard", new ControlSequence().Press(Key.A).HoldUntil(() => false, 0.05f));

			RunUntilCompleted(item);

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.TimedOut));
			Assert.That(_changes.OfType<ButtonUp>().Select(c => c.ControlPath), Is.EqualTo(new[] { "a" }));
			Assert.That(Keyboard.current.aKey.isPressed, Is.False);
		}

		[Test]
		public void AHoldUntilWaitsForItsCondition()
		{
			var ready = false;
			var item = _autoTap.Device("Keyboard", new ControlSequence().Press(Key.A).HoldUntil(() => ready, 5f));

			Frames(5);
			Assert.That(item.Completed, Is.False);
			Assert.That(Keyboard.current.aKey.isPressed, Is.True);
			ready = true;
			RunUntilCompleted(item);

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(Keyboard.current.aKey.isPressed, Is.False);
		}

		[Test]
		public void ADeviceHoldsOneOperationAtATime()
		{
			var first = _autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South).Hold(0.2f));
			Frame();

			Assert.That(_autoTap.Gamepad(new ControlSequence().Press(GamepadButton.East)), Is.Null);
			Assert.That(_autoTap.Gamepad(new ControlSequence().Press(GamepadButton.East), 1), Is.Not.Null, "another gamepad");
			Assert.That(_autoTap.Key(Key.A), Is.Not.Null, "another device");
			RunUntilCompleted(first);
			Assert.That(_autoTap.Gamepad(new ControlSequence().Press(GamepadButton.East)), Is.Not.Null, "the end was processed");
		}

		[Test]
		public void AKeyboardAndAPointerAndAFingerRunAtTheSameTime()
		{
			var key = _autoTap.Key(Key.A, 0.1f);
			var mouse = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.5f, 0.5f)).Hold(0.1f));
			var finger = _autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0.1f);

			RunUntilCompleted(key);
			RunUntilCompleted(mouse);
			RunUntilCompleted(finger);

			Assert.That(key.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(mouse.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(finger.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			var started = _allLogs.Where(e => e.Kind == AutoTapBase.LogEventKind.Started).Select(e => e.Item).ToArray();
			Assert.That(started, Is.EqualTo(new[] { key, finger, mouse }).Or.EqualTo(new[] { key, mouse, finger }));
		}

		[Test]
		public void ShouldNotPressOnIsNotAskedForTheKeysOfADevice()
		{
			var asked = 0;
			_autoTap.Refuse = _ =>
			{
				++asked;
				return true;
			};

			var item = _autoTap.Key(Key.A);
			RunUntilCompleted(item);

			Assert.That(asked, Is.Zero);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void ASequenceThatDoesNotFitTheDeviceThrowsAndTheDeviceIsNotTaken()
		{
			var pad = new ControlSequence();

			Assert.Throws<ArgumentException>(() => _autoTap.Gamepad(new ControlSequence().Press("noSuchButton")));
			Assert.Throws<ArgumentException>(() => _autoTap.Gamepad(new ControlSequence().Press("leftStick")), "a stick is not a button");
			Assert.Throws<ArgumentException>(() => _autoTap.Gamepad(new ControlSequence().Set("noSuchControl", 1f)));
			Assert.Throws<ArgumentException>(() => _autoTap.Gamepad(new ControlSequence().Set("leftStick", 1f)), "the wrong type");
			Assert.Throws<ArgumentException>(() => _autoTap.Gamepad(new ControlSequence().Press(Key.A)), "a key needs a keyboard");
			Assert.Throws<ArgumentException>(() => _autoTap.Gamepad(new ControlSequence().Text("a")), "a gamepad receives no text");
			Assert.Throws<ArgumentException>(() => _autoTap.Gamepad(new ControlSequence().Composition("a")));
			Assert.Throws<ArgumentException>(() => _autoTap.Device("Keyboard", new ControlSequence().Press(GamepadButton.South)), "a gamepad button needs a gamepad");
			Assert.Throws<ArgumentException>(() => _autoTap.Device("Keyboard", new ControlSequence().Press(Key.None)));
			Assert.Throws<ArgumentException>(() => _autoTap.Device("Keyboard", new ControlSequence().Composition(new string('a', 64))));
			Assert.Throws<ArgumentException>(() => _autoTap.Device("Mouse", new ControlSequence().Press("leftButton")), "a pointer is fired with FirePointer");
			Assert.Throws<ArgumentException>(() => _autoTap.Device("Touchscreen", new ControlSequence().Press("primaryTouch/press")));
			Assert.Throws<ArgumentException>(() => _autoTap.Device("NoSuchLayout", new ControlSequence().Press("a")));
			Assert.Throws<ArgumentException>(() => _autoTap.Gamepad(pad), "a sequence that writes nothing");
			Assert.Throws<ArgumentException>(() => _autoTap.Gamepad(new ControlSequence().Hold(1f)));
			Assert.Throws<ArgumentNullException>(() => _autoTap.Gamepad(null));
			Assert.Throws<ArgumentException>(() => _autoTap.Device("", new ControlSequence().Press("a")));
			Assert.That(_autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South)), Is.Not.Null, "the device was not taken by the failed calls");
		}

		[Test]
		public void FireOnADisabledAutoTapReturnsNull()
		{
			_autoTap.Stop();

			Assert.That(_autoTap.Key(Key.A), Is.Null);
			Assert.That(_autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South)), Is.Null);
		}

		[Test]
		public void StopReleasesWhatADeviceHoldsAndEndsTheOperationAsInterrupted()
		{
			var item = _autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South).Set("leftStick", Vector2.one).Hold(10f));
			Frames(2);
			Assert.That(Gamepad.current.buttonSouth.isPressed, Is.True);

			_autoTap.Stop();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(item.CompletedOrder, Is.Null);
			InputSystem.Update();
			var logs = _autoTap.GetLatestLogs().ToList();
			Assert.That(logs.Select(e => e.Item), Is.EqualTo(new[] { item }));
			Assert.That(logs[0].Kind, Is.EqualTo(AutoTapBase.LogEventKind.Completed));
			Assert.That(logs[0].Order, Is.Null);
			Assert.That(_changes.OfType<ButtonUp>().Select(c => c.ControlPath), Is.EqualTo(new[] { "buttonSouth" }));
			Assert.That(Gamepad.current.leftStick.ReadUnprocessedValue(), Is.EqualTo(Vector2.zero));
		}

		[Test]
		public void AnOperationWhoseEventsWereDroppedEndsAsDropped()
		{
			var item = _autoTap.Key(Key.A, 1f);
			InputSystem.DisableDevice(Keyboard.current);

			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Dropped));
			Assert.That(item.Order, Is.Null);
			Assert.That(_allLogs.Count(e => e.Item == item), Is.EqualTo(1));
			Assert.That(_allLogs.Single(e => e.Item == item).Kind, Is.EqualTo(AutoTapBase.LogEventKind.Completed));
			Assert.That(_autoTap.Key(Key.A), Is.Not.Null, "the device is free");
		}

		[Test]
		public void AResetOfTheDeviceInterruptsTheOperation()
		{
			var item = _autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South).Hold(1f));
			Frame();

			InputSystem.ResetDevice(Gamepad.current);
			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(item.CompletedOrder, Is.Not.Null, "the reset is the change that ended it");
			Assert.That(Gamepad.current.buttonSouth.isPressed, Is.False);
		}

		[Test]
		public void EveryDeviceItemEndsExactlyOnceInTheLogs()
		{
			var finished = _autoTap.Key(Key.A);
			var timedOut = _autoTap.Device("Keyboard", new ControlSequence().HoldUntil(() => false, 0.05f).Press(Key.B), 1);
			var running = _autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South).Hold(5f));
			Frames(5);
			var late = _autoTap.Key(Key.C, 5f);
			Frame();
			_autoTap.Stop();
			InputSystem.Update();
			_allLogs.AddRange(_autoTap.GetLatestLogs());
			InputSystem.Update();
			_allLogs.AddRange(_autoTap.GetLatestLogs());

			foreach (var item in new[] { finished, timedOut, running, late })
			{
				Assert.That(item.Completed, Is.True, item.ToString());
				var ends = _allLogs.Count(e => e.Item == item && e.Kind == AutoTapBase.LogEventKind.Completed);
				Assert.That(ends, Is.EqualTo(1), $"{item.Device.name} {item.EndReason}");
			}
		}

		[Test]
		public void TheLogOfDeviceOperationsIsInTheOrderOfTheChangesOfAllDevices()
		{
			var pad = _autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South).Hold(1f));
			var finger = _autoTap.Tap(0, TestScene.At(0.4f, 0.4f), 1f);
			var key = _autoTap.Key(Key.A, 1f);

			Frame();

			var started = _allLogs.Where(e => e.Kind == AutoTapBase.LogEventKind.Started).ToList();
			Assert.That(started.Select(e => e.Item), Is.EqualTo(new[] { pad, finger, key }));
			Assert.That(started.Select(e => e.Order), Is.Ordered);
		}

		[Test]
		public void OnlyTheDevicesThatAreUsedAreAdded()
		{
			var item = _autoTap.Key(Key.A);

			RunUntilCompleted(item);

			Assert.That(Gamepad.current, Is.Null, "only the devices that are used are added");
			Assert.That(Mouse.current, Is.Null);
			Assert.That(Touchscreen.current, Is.Null);
		}

		[Test]
		public void TheDevicesAreRemovedWhenTheAutoTapStops()
		{
			RunUntilCompleted(_autoTap.Key(Key.A));
			RunUntilCompleted(_autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South)));

			Assert.That(InputSystem.devices, Is.Not.Empty);
			_autoTap.Stop();

			Assert.That(InputSystem.devices, Is.Empty, "nothing is held, so they are removed at once");
		}
	}
}
#endif
