#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// The operations of pointers other than fingers (a mouse and a pen), on the Input System of
	/// <see cref="InputTestFixture"/>. One frame is an input update, the event system, and
	/// <see cref="AutoTapBase.Update(float)"/>, as in the player loop.
	/// </summary>
	public class AutoTapPointerTests : InputTestFixture
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
			_autoTap.Setup(_scene.Canvas, null);
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

		Recorder AddRecorder(string name, float xMin, float yMin, float xMax, float yMax)
		{
			var recorder = _scene.Add<Recorder>(name, xMin, yMin, xMax, yMax);
			recorder.Events = _ui;
			return recorder;
		}

		string[] UiOf(string name)
		{
			return _ui.Where(e => e.Name == name).Select(e => e.Kind).ToArray();
		}

		[Test]
		public void AFiredHoverMovesTheMouseAndPressesNothing()
		{
			var p = TestScene.At(0.5f, 0.5f);

			var item = _autoTap.Mouse(Gesture.HoverAt(p));
			Frame();

			Assert.That(item, Is.Not.Null);
			Assert.That(item.Kind, Is.EqualTo(AutoTapBase.LogItemKind.Pointer));
			Assert.That(item.Index, Is.EqualTo(-1));
			Assert.That(item.Device, Is.InstanceOf<Mouse>());
			Assert.That(item.ScreenPoint, Is.EqualTo(p));
			Assert.That(Mouse.current.position.ReadValue(), Is.EqualTo(p));
			Assert.That(Mouse.current.leftButton.isPressed, Is.False);
			Assert.That(item.Order, Is.Not.Null);
			Assert.That(_changes.OfType<ValueChanged>().Select(c => c.ControlPath), Is.EqualTo(new[] { "position" }));
			Assert.That(_changes.OfType<ButtonDown>(), Is.Empty);
			Assert.That(item.Target, Is.Null, "nothing is pressed, so nothing is predicted");
		}

		[Test]
		public void AHoverEndsInTheFrameAfterItWasProcessedWithoutAnyChange()
		{
			var item = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.5f, 0.5f)));

			Frame();
			Assert.That(item.Completed, Is.False);
			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(item.CompletedOrder, Is.Null, "the end changed nothing");
			Assert.That(item.CompletedFrameCount, Is.EqualTo(Time.frameCount));
			var logs = _autoTap.GetLatestLogs().ToList();
			Assert.That(logs.Select(e => e.Kind), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
			Assert.That(Mouse.current.position.ReadValue(), Is.EqualTo(TestScene.At(0.5f, 0.5f)), "the mouse stays");
		}

		[Test]
		public void TheUiIsHoveredWhileTheMouseIsOverItAndExitedWhenItLeaves()
		{
			AddRecorder("A", 0.1f, 0.1f, 0.3f, 0.3f);

			_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f)));
			Frames(3);
			var enters = UiOf("A");
			_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.8f, 0.8f)));
			Frames(3);

			Assert.That(enters, Is.EqualTo(new[] { "enter" }));
			Assert.That(UiOf("A"), Is.EqualTo(new[] { "enter", "exit" }));
		}

		[Test]
		public void AddingTheMouseDoesNotHoverAnythingUntilItMoves()
		{
			AddRecorder("A", 0f, 0f, 1f, 1f);
			using (var session = VirtualInput.Acquire())
			{
				var mouse = session.GetPointer("Mouse");

				Frames(3);
				Assert.That(_ui, Is.Empty, "a mouse that was only added is not a pointer of the UI");
				mouse.MoveTo(TestScene.At(0.5f, 0.5f));
				Frames(2);
			}

			Assert.That(UiOf("A"), Is.EqualTo(new[] { "enter" }));
		}

		[Test]
		public void APressOnAButtonIsAClickOfTheLeftButton()
		{
			AddRecorder("A", 0.1f, 0.1f, 0.3f, 0.3f);

			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.2f, 0.2f)));
			RunUntilCompleted(item);

			Assert.That(UiOf("A"), Is.EqualTo(new[] { "enter", "down", "up", "click" }));
			Assert.That(_ui.Where(e => e.Kind == "click").Select(e => e.Button), Is.EqualTo(new[] { PointerEventData.InputButton.Left }));
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(item.ClickExecuted, Is.True);
			Assert.That(item.Target, Is.Not.Null);
			Assert.That(Mouse.current.leftButton.isPressed, Is.False);
		}

		[TestCase("rightButton", PointerEventData.InputButton.Right)]
		[TestCase("middleButton", PointerEventData.InputButton.Middle)]
		public void ARightOrMiddleButtonIsDeliveredAsThatButton(string button, PointerEventData.InputButton expected)
		{
			var target = AddRecorder("A", 0.1f, 0.1f, 0.3f, 0.3f);

			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.2f, 0.2f), button));
			RunUntilCompleted(item);

			Assert.That(UiOf("A"), Is.EqualTo(new[] { "enter", "down", "up", "click" }));
			Assert.That(_ui.Where(e => e.Kind != "enter").Select(e => e.Button).Distinct(), Is.EqualTo(new[] { expected }));
			Assert.That(item.Target, Is.SameAs(target.gameObject));
			Assert.That(item.ClickTarget, Is.SameAs(target.gameObject), "the click is predicted for any button of the mouse");
			Assert.That(_changes.OfType<ButtonDown>().Select(c => c.ControlPath), Is.EqualTo(new[] { button }));
			Assert.That(_changes.OfType<ButtonUp>().Select(c => c.ControlPath), Is.EqualTo(new[] { button }));
		}

		[Test]
		public void TheForwardAndBackButtonsAreNotHandledByTheUiAndAreNotPredicted()
		{
			var target = AddRecorder("A", 0.1f, 0.1f, 0.3f, 0.3f);

			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.2f, 0.2f), "forwardButton"));
			RunUntilCompleted(item);

			Assert.That(Mouse.current.forwardButton.isPressed, Is.False);
			Assert.That(_changes.OfType<ButtonDown>().Select(c => c.ControlPath), Is.EqualTo(new[] { "forwardButton" }));
			Assert.That(UiOf("A"), Is.EqualTo(new[] { "enter" }));
			Assert.That(item.Target, Is.Null);
			Assert.That(item.ClickExecuted, Is.False);
			Assert.That(target, Is.Not.Null);
		}

		[Test]
		public void MoreThanOneButtonCanBeHeldAndTheyAreReleasedInTheOppositeOrder()
		{
			var item = _autoTap.Mouse(
				Gesture.PressAt(TestScene.At(0.2f, 0.2f)).Press("rightButton").Press("middleButton").Hold(0.04f));

			RunUntilCompleted(item);

			var downs = _changes.OfType<ButtonDown>().Select(c => c.ControlPath).ToArray();
			var ups = _changes.OfType<ButtonUp>().Select(c => c.ControlPath).ToArray();
			Assert.That(downs, Is.EqualTo(new[] { "leftButton", "rightButton", "middleButton" }));
			Assert.That(ups, Is.EqualTo(new[] { "middleButton", "rightButton", "leftButton" }));
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void AButtonIsNeverPressedAndReleasedByTheSameInputUpdate()
		{
			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.2f, 0.2f)).Release().Press("rightButton").Release("rightButton"));

			RunUntilCompleted(item);

			var presses = _changes.OfType<ButtonDown>().ToList();
			var releases = _changes.OfType<ButtonUp>().ToList();
			Assert.That(presses, Has.Count.EqualTo(2));
			Assert.That(releases, Has.Count.EqualTo(2));
			foreach (var press in presses)
			{
				var release = releases.First(r => r.ControlPath == press.ControlPath);
				Assert.That(release.UpdateCount, Is.GreaterThan(press.UpdateCount), press.ControlPath);
			}
		}

		[Test]
		public void MovingWhilePressedIsADragAndItsStartAndEndAreInTheLogItem()
		{
			var source = _scene.Add<DragHandler>("Source", 0.1f, 0.1f, 0.3f, 0.3f);
			var drop = _scene.Add<DropHandler>("Drop", 0.6f, 0.6f, 0.9f, 0.9f);
			var from = TestScene.At(0.2f, 0.2f);
			var to = TestScene.At(0.7f, 0.7f);

			var item = _autoTap.Mouse(Gesture.PressAt(from).MoveTo(to, 0.12f));
			RunUntilCompleted(item);

			Assert.That(item.Target, Is.SameAs(source.gameObject));
			Assert.That(item.DragTarget, Is.SameAs(source.gameObject));
			Assert.That(item.Dragged, Is.True);
			Assert.That(item.DragStartedOrder, Is.Not.Null);
			Assert.That(item.DragStartedOrder, Is.GreaterThan(item.Order));
			Assert.That(item.DragStartedOrder, Is.LessThan(item.CompletedOrder));
			Assert.That(item.DragStartedFrameCount, Is.GreaterThan(0));
			Assert.That(item.CompletedFrameCount, Is.GreaterThanOrEqualTo(item.DragStartedFrameCount));
			Assert.That(item.DropTarget, Is.SameAs(drop.gameObject));
			Assert.That(item.ClickExecuted, Is.False);
			Assert.That(_allLogs.Where(e => e.Item == item).Select(e => e.Kind), Is.EqualTo(new[]
			{
				AutoTapBase.LogEventKind.Started,
				AutoTapBase.LogEventKind.DragStarted,
				AutoTapBase.LogEventKind.Completed,
			}));
			var started = _allLogs.Single(e => e.Item == item && e.Kind == AutoTapBase.LogEventKind.DragStarted);
			Assert.That(started.Order, Is.EqualTo(item.DragStartedOrder));
		}

		[Test]
		public void ADragThatDoesNotReachTheThresholdIsNotADrag()
		{
			_scene.Add<DragHandler>("Source", 0.1f, 0.1f, 0.3f, 0.3f);
			var from = TestScene.At(0.2f, 0.2f);

			var item = _autoTap.Mouse(Gesture.PressAt(from).MoveTo(from + new Vector2(2, 0), 0.08f));
			RunUntilCompleted(item);

			Assert.That(item.Dragged, Is.False);
			Assert.That(item.DragStartedOrder, Is.Null);
		}

		[Test]
		public void TheDropIsPredictedForTheButtonThatWasPressed()
		{
			var source = AddRecorder("Source", 0.1f, 0.1f, 0.3f, 0.3f);
			var drop = AddRecorder("Drop", 0.6f, 0.6f, 0.9f, 0.9f);

			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.2f, 0.2f), "rightButton").MoveTo(TestScene.At(0.7f, 0.7f), 0.12f));
			RunUntilCompleted(item);

			Assert.That(UiOf("Source"), Does.Contain("beginDrag").And.Contain("endDrag"));
			Assert.That(UiOf("Drop"), Does.Contain("drop"));
			Assert.That(item.Dragged, Is.True);
			Assert.That(item.DragTarget, Is.SameAs(source.gameObject));
			Assert.That(item.DropTarget, Is.SameAs(drop.gameObject));
			Assert.That(_ui.Where(e => e.Kind == "drop").Select(e => e.Button), Is.EqualTo(new[] { PointerEventData.InputButton.Right }));
		}

		[Test]
		public void APenIsInRangeWhileTheGestureRunsAndTheTipFollowsThePress()
		{
			var item = _autoTap.Pen(Gesture.PressAt(TestScene.At(0.2f, 0.2f)).Set("pressure", 0.6f).Hold(0.08f));

			Frame();
			var pen = Pen.current;
			Assert.That(item.Device, Is.SameAs(pen));
			Assert.That(pen.inRange.isPressed, Is.True);
			Assert.That(pen.tip.isPressed, Is.True);
			Assert.That(pen.position.ReadValue(), Is.EqualTo(TestScene.At(0.2f, 0.2f)));
			Frame();
			Assert.That(pen.pressure.ReadValue(), Is.EqualTo(0.6f).Within(0.0001f));
			RunUntilCompleted(item);

			Assert.That(pen.inRange.isPressed, Is.False, "the pen leaves the range at the end");
			Assert.That(pen.tip.isPressed, Is.False);
			Assert.That(pen.pressure.ReadValue(), Is.Zero, "the pressure goes back to its default");
			Assert.That(pen.position.ReadValue(), Is.EqualTo(TestScene.At(0.2f, 0.2f)), "the position stays");
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void APenHoversAndWritesTiltAndTheBarrelAndEraserButtons()
		{
			var item = _autoTap.Pen(
				Gesture.HoverAt(TestScene.At(0.2f, 0.2f)).Set("tilt", new Vector2(0.5f, -0.25f)).Press("barrel1").Press("eraser").Hold(0.04f));

			Frame();
			var pen = Pen.current;

			Assert.That(pen.tilt.ReadValue(), Is.EqualTo(new Vector2(0.5f, -0.25f)), "the steps that take no time are processed together");
			Assert.That(pen.firstBarrelButton.isPressed, Is.True);
			Assert.That(pen.eraser.isPressed, Is.True);
			Assert.That(pen.tip.isPressed, Is.False);
			RunUntilCompleted(item);
			Assert.That(pen.firstBarrelButton.isPressed, Is.False);
			Assert.That(pen.eraser.isPressed, Is.False);
			Assert.That(pen.tilt.ReadValue(), Is.EqualTo(Vector2.zero));
		}

		[Test]
		public void FireQueuesEveryStepAtTheStartThatTakesNoTimeAndKeepsAReleaseForTheNextInputUpdate()
		{
			var p = TestScene.At(0.5f, 0.5f);

			var item = _autoTap.Mouse(Gesture.HoverAt(p).Press("rightButton").Release("rightButton").Press().Release());
			Assert.That(_changes, Is.Empty, "Fire processes nothing");
			RunUntilCompleted(item);

			var buttons = _changes.OfType<ControlChange>().Where(c => c is ButtonDown || c is ButtonUp).ToList();
			Assert.That(
				buttons.Select(c => $"{c.GetType().Name} {c.ControlPath}"),
				Is.EqualTo(new[] { "ButtonDown rightButton", "ButtonUp rightButton", "ButtonDown leftButton", "ButtonUp leftButton" }));
			var position = _changes.OfType<ValueChanged>().First(c => c.ControlPath == "position");
			Assert.That(position.UpdateCount, Is.EqualTo(buttons[0].UpdateCount), "the hover and the first press are in the update of the first frame");
			Assert.That(buttons[0].UpdateCount, Is.LessThan(buttons[1].UpdateCount));
			Assert.That(buttons[1].UpdateCount, Is.EqualTo(buttons[2].UpdateCount), "the release goes with the next press, in that order");
			Assert.That(buttons[2].UpdateCount, Is.LessThan(buttons[3].UpdateCount));
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void APenHoversTheUiWhateverItsRange()
		{
			AddRecorder("A", 0.1f, 0.1f, 0.3f, 0.3f);

			var item = _autoTap.Pen(Gesture.HoverAt(TestScene.At(0.2f, 0.2f)));
			RunUntilCompleted(item);
			Frames(2);

			Assert.That(Pen.current.inRange.isPressed, Is.False);
			Assert.That(UiOf("A"), Is.EqualTo(new[] { "enter" }), "the pen is still over A after the gesture");
		}

		[Test]
		public void AScrollIsOneEventWhoseAmountIsInTheMouseForOneInputUpdateOnly()
		{
			var item = _autoTap.Scroll(TestScene.At(0.5f, 0.5f), new Vector2(0, -120));

			Frame(); // the hover and the scroll are two events of the same input update
			var scrolled = _changes.OfType<Scrolled>().Single();
			Assert.That(scrolled.Delta, Is.EqualTo(new Vector2(0, -120)));
			Assert.That(Mouse.current.scroll.ReadValue(), Is.EqualTo(new Vector2(0, -120)));
			Frame();

			Assert.That(Mouse.current.scroll.ReadValue(), Is.EqualTo(Vector2.zero));
			Assert.That(_changes.OfType<Scrolled>().Count(), Is.EqualTo(1));
			Assert.That(item.Kind, Is.EqualTo(AutoTapBase.LogItemKind.Pointer));
		}

		[Test]
		public void TwoScrollsOfOneGestureAddUp()
		{
			_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.5f, 0.5f)).Scroll(new Vector2(0, 120)).Scroll(new Vector2(0, 240)));

			Frame();

			Assert.That(Mouse.current.scroll.ReadValue(), Is.EqualTo(new Vector2(0, 360)));
			Assert.That(_changes.OfType<Scrolled>().Select(c => c.Delta), Is.EqualTo(new[] { new Vector2(0, 360) }), "merged by the mouse");
		}

		[Test]
		public void AScrollIsDeliveredToTheUiUnderTheMouse()
		{
			AddRecorder("A", 0.1f, 0.1f, 0.3f, 0.3f);
			AddRecorder("B", 0.6f, 0.6f, 0.9f, 0.9f);

			_autoTap.Scroll(TestScene.At(0.7f, 0.7f), new Vector2(0, 120));
			Frames(3);

			Assert.That(UiOf("A"), Is.Empty);
			Assert.That(UiOf("B"), Is.EqualTo(new[] { "enter", "scroll" }));
			var scroll = _ui.Single(e => e.Kind == "scroll");
			// The input test runtime reports one scroll unit per tick.
			Assert.That(scroll.ScrollDelta.y, Is.EqualTo(120f * _scene.InputModule.scrollDeltaPerTick).Within(0.001f));
		}

		[Test]
		public void AScrollBetweenTwoMovesIsNotWrittenTwice()
		{
			var p = TestScene.At(0.5f, 0.5f);
			_autoTap.Mouse(Gesture.HoverAt(p).Scroll(new Vector2(0, 120)).MoveTo(p + new Vector2(10, 0), 0f));

			Frame();

			Assert.That(Mouse.current.scroll.ReadValue(), Is.EqualTo(new Vector2(0, 120)), "the scroll of the first event is not copied into the second");
		}

		[Test]
		public void AMouseHoldsOneOperationAtATime()
		{
			var first = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.5f, 0.5f)).Hold(0.2f));
			Frame();

			Assert.That(_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f))), Is.Null);
			Assert.That(_autoTap.Scroll(TestScene.At(0.2f, 0.2f), Vector2.up), Is.Null, "a shortcut is the same operation");
			Assert.That(_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f)), 1), Is.Not.Null, "another mouse is another device");
			Assert.That(_autoTap.Pen(Gesture.HoverAt(TestScene.At(0.2f, 0.2f))), Is.Not.Null, "and so is a pen");
			RunUntilCompleted(first);
			Assert.That(_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f))), Is.Not.Null, "the end was processed");
		}

		[Test]
		public void TheDeviceIsFreeInTheFrameThatProcessedTheEnd()
		{
			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)));

			Frame(); // the press is processed; the plan is over, so the release is queued
			Assert.That(item.Completed, Is.False);
			Assert.That(_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f))), Is.Null);
			Frame(); // the release is processed

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(item.CompletedOrder, Is.GreaterThan(item.Order));
			Assert.That(_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f))), Is.Not.Null);
		}

		[Test]
		public void TwoMiceWorkAtTheSameTimeAsTwoPointers()
		{
			var a = AddRecorder("A", 0.1f, 0.1f, 0.3f, 0.3f);
			var b = AddRecorder("B", 0.6f, 0.6f, 0.9f, 0.9f);

			var first = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.2f, 0.2f)).Hold(0.08f));
			var second = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.7f, 0.7f)).Hold(0.08f), 1);
			RunUntilCompleted(first);
			RunUntilCompleted(second);

			Assert.That(first.Device, Is.Not.SameAs(second.Device));
			Assert.That(UiOf("A"), Is.EqualTo(new[] { "enter", "down", "up", "click" }));
			Assert.That(UiOf("B"), Is.EqualTo(new[] { "enter", "down", "up", "click" }));
			Assert.That(a, Is.Not.Null);
			Assert.That(b, Is.Not.Null);
		}

		[Test]
		public void AFingerAMouseAndAPenDoNotRemoveEachOthersPointers()
		{
			AddRecorder("Finger", 0.05f, 0.05f, 0.25f, 0.25f);
			AddRecorder("Mouse", 0.35f, 0.05f, 0.55f, 0.25f);
			AddRecorder("Pen", 0.65f, 0.05f, 0.85f, 0.25f);
			Assert.That(_scene.InputModule.pointerBehavior, Is.EqualTo(UIPointerBehavior.AllPointersAsIs));

			var finger = _autoTap.Tap(0, TestScene.At(0.15f, 0.15f), 0.2f);
			Frame();
			var mouse = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.45f, 0.15f)).Hold(0.08f));
			var pen = _autoTap.Pen(Gesture.PressAt(TestScene.At(0.75f, 0.15f)).Hold(0.08f));
			RunUntilCompleted(mouse);
			RunUntilCompleted(pen);
			RunUntilCompleted(finger);

			Assert.That(UiOf("Finger"), Is.EqualTo(new[] { "enter", "down", "up", "click", "exit" }).Or.EqualTo(new[] { "enter", "down", "up", "click" }));
			Assert.That(UiOf("Finger"), Does.Contain("up").And.Contain("click"));
			Assert.That(UiOf("Mouse"), Is.EqualTo(new[] { "enter", "down", "up", "click" }));
			Assert.That(UiOf("Pen"), Is.EqualTo(new[] { "enter", "down", "up", "click" }));
			var ids = _ui.Where(e => e.Kind == "down").Select(e => e.PointerId).Distinct().ToArray();
			Assert.That(ids, Has.Length.EqualTo(3), "three pointers");
			Assert.That(_ui.Where(e => e.Name == "Mouse" && e.Kind == "down").Select(e => e.PointerId).Single(), Is.EqualTo(Mouse.current.deviceId));
			Assert.That(_ui.Where(e => e.Name == "Pen" && e.Kind == "down").Select(e => e.PointerId).Single(), Is.EqualTo(Pen.current.deviceId));
		}

		[Test]
		public void WithTheDefaultPointerBehaviorAFingerAndAMouseDisturbEachOther()
		{
			// The reason why the virtual input switches to AllPointersAsIs: the same scene as in the test above, with
			// the behavior that the input module has by default.
			AddRecorder("Finger", 0.05f, 0.05f, 0.25f, 0.25f);
			AddRecorder("Mouse", 0.35f, 0.05f, 0.55f, 0.25f);
			_scene.InputModule.pointerBehavior = UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;

			var finger = _autoTap.Tap(0, TestScene.At(0.15f, 0.15f), 0.3f);
			Frame();
			var mouse = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.45f, 0.15f)).Hold(0.08f));
			RunUntilCompleted(mouse);
			RunUntilCompleted(finger);

			Assert.That(
				UiOf("Mouse"), Is.Not.EqualTo(new[] { "enter", "down", "up", "click" }),
				"the mouse and the finger are not two independent pointers any more");
		}

		[Test]
		public void APressInTheMiddleOfAGestureIsRefusedWhenTheTopmostObjectIsRefused()
		{
			var blocked = _scene.Add<ClickHandler>("Blocked", 0.1f, 0.1f, 0.3f, 0.3f);
			_autoTap.Refuse = topmost => topmost.gameObject == blocked.gameObject;
			var open = TestScene.At(0.7f, 0.7f);

			var item = _autoTap.Mouse(
				Gesture.PressAt(open, "middleButton").MoveTo(TestScene.At(0.2f, 0.2f), 0f).Press("rightButton").Hold(1f));
			RunUntilCompleted(item);

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Refused));
			Assert.That(_changes.OfType<ButtonDown>().Select(c => c.ControlPath), Is.EqualTo(new[] { "middleButton" }), "the refused press was not made");
			Assert.That(_changes.OfType<ButtonUp>().Select(c => c.ControlPath), Is.EqualTo(new[] { "middleButton" }), "and what was held was released");
			Assert.That(Mouse.current.middleButton.isPressed, Is.False);
			Assert.That(Mouse.current.rightButton.isPressed, Is.False);
			Assert.That(_autoTap.Mouse(Gesture.HoverAt(open)), Is.Not.Null, "the device is free");
		}

		[Test]
		public void AFirstPressOnARefusedObjectIsNotMadeAndReturnsNull()
		{
			var blocked = _scene.Add<ClickHandler>("Blocked", 0.1f, 0.1f, 0.3f, 0.3f);
			_autoTap.Refuse = topmost => topmost.gameObject == blocked.gameObject;

			var refused = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.2f, 0.2f)));
			var hovered = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f)));

			Assert.That(refused, Is.Null);
			Assert.That(hovered, Is.Not.Null, "a hover is not a press");
		}

		[Test]
		public void AGestureThatDoesNotFitAPointerThrows()
		{
			var p = TestScene.At(0.5f, 0.5f);

			Assert.Throws<ArgumentException>(() => _autoTap.Mouse(Gesture.PressAt(p, "noSuchButton")));
			Assert.Throws<ArgumentException>(() => _autoTap.Mouse(Gesture.PressAt(p, "position")), "not a button");
			Assert.Throws<ArgumentException>(() => _autoTap.Mouse(Gesture.PressAt(p).Set("noSuchControl", 1f)));
			Assert.Throws<ArgumentException>(() => _autoTap.Mouse(Gesture.PressAt(p).Set("position", 1f)), "the wrong type");
			Assert.Throws<ArgumentException>(() => _autoTap.Mouse(Gesture.PressAt(p, "tip")), "a mouse has no tip");
			Assert.Throws<ArgumentException>(() => _autoTap.Pen(Gesture.PressAt(p, "leftButton")), "a pen has no left button");
			Assert.Throws<ArgumentException>(() => _autoTap.Pointer("Touchscreen", Gesture.PressAt(p)));
			Assert.Throws<ArgumentException>(() => _autoTap.Pointer("Keyboard", Gesture.PressAt(p)));
			Assert.Throws<ArgumentException>(() => _autoTap.Pointer("NoSuchLayout", Gesture.PressAt(p)));
			Assert.That(_autoTap.Mouse(Gesture.PressAt(p)), Is.Not.Null, "the device was not taken by the failed calls");
		}

		[Test]
		public void FirePointerOutsideTheScreenReturnsNull()
		{
			Assert.That(_autoTap.Mouse(Gesture.HoverAt(new Vector2(-1, 10))), Is.Null);
			Assert.That(_autoTap.Mouse(Gesture.HoverAt(new Vector2(10, 10)).MoveTo(new Vector2(Screen.width + 1, 10), 0.1f)), Is.Null);
		}

		[Test]
		public void AMoveToAFunctionIsOnlyKnownWhenItStarts()
		{
			var destination = TestScene.At(0.7f, 0.2f);
			var item = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f)).MoveTo(() => destination, 0.04f));

			RunUntilCompleted(item);

			Assert.That(Mouse.current.position.ReadValue(), Is.EqualTo(destination));
			Assert.That(item.DragTo, Is.EqualTo(destination));
		}

		[Test]
		public void TheMarkerOfAPointerIsShownWhileTheOperationRuns()
		{
			var item = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.5f, 0.5f)).Hold(0.2f));
			var marker = _scene.Canvas.transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Marker AutoTap-Mouse"));
			Assert.That(marker, Is.Not.Null);
			Assert.That(marker.gameObject.activeSelf, Is.True);

			RunUntilCompleted(item);

			Assert.That(marker.gameObject.activeSelf, Is.False);
		}

		[Test]
		public void StopEndsARunningPointerOperationAsInterruptedAndReleasesWhatItHolds()
		{
			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Hold(10f));
			Frame();

			_autoTap.Stop();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(item.CompletedOrder, Is.Null);
			Assert.That(item.CompletedFrameCount, Is.EqualTo(Time.frameCount));

			InputSystem.Update();
			var logs = _autoTap.GetLatestLogs().ToList();
			Assert.That(logs.Select(e => e.Item), Is.EqualTo(new[] { item }));
			Assert.That(logs.Single().Kind, Is.EqualTo(AutoTapBase.LogEventKind.Completed));
			Assert.That(logs.Single().Order, Is.Null);
			Assert.That(_changes.OfType<ButtonUp>().Select(c => c.ControlPath), Is.EqualTo(new[] { "leftButton" }));
		}

		[Test]
		public void AnOperationWhoseEventsWereDroppedEndsAsDropped()
		{
			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Hold(1f));
			InputSystem.DisableDevice(Mouse.current);

			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Dropped));
			Assert.That(item.Order, Is.Null);
			var logs = _autoTap.GetLatestLogs().ToList();
			Assert.That(logs.Select(e => e.Kind), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
			Assert.That(logs[0].Order, Is.Null);
			Assert.That(_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f))), Is.Not.Null, "the device is free");
		}

		[Test]
		public void AResetOfTheMouseInterruptsTheOperation()
		{
			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Hold(1f));
			Frame();

			InputSystem.ResetDevice(Mouse.current);
			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(item.CompletedOrder, Is.Not.Null, "the reset is the change that ended it");
			Assert.That(Mouse.current.leftButton.isPressed, Is.False);
		}

		[Test]
		public void EveryPointerItemEndsExactlyOnceInTheLogs()
		{
			var finished = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)));
			var hover = _autoTap.Pen(Gesture.HoverAt(TestScene.At(0.5f, 0.5f)));
			var running = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.5f, 0.5f)).Hold(5f), 1);
			Frames(4);
			var late = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Hold(5f));
			Frame();
			_autoTap.Stop();
			InputSystem.Update();
			_allLogs.AddRange(_autoTap.GetLatestLogs());
			InputSystem.Update();
			_allLogs.AddRange(_autoTap.GetLatestLogs());

			foreach (var item in new[] { finished, hover, running, late })
			{
				Assert.That(item.Completed, Is.True, item.ToString());
				var ends = _allLogs.Count(e => e.Item == item && e.Kind == AutoTapBase.LogEventKind.Completed);
				Assert.That(ends, Is.EqualTo(1), $"{item.Kind} {item.EndReason}");
			}
		}

		[Test]
		public void TheLogOfPointerOperationsIsInTheOrderOfTheChangesOfAllDevices()
		{
			var mouse = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f)));
			var finger = _autoTap.Tap(0, TestScene.At(0.4f, 0.4f), 1f);
			var pen = _autoTap.Pen(Gesture.HoverAt(TestScene.At(0.6f, 0.6f)));

			Frame();

			var started = _allLogs.Where(e => e.Kind == AutoTapBase.LogEventKind.Started).ToList();
			Assert.That(started.Select(e => e.Item), Is.EqualTo(new[] { mouse, finger, pen }));
			Assert.That(started.Select(e => e.Order), Is.Ordered);
			Assert.That(mouse.Order, Is.LessThan(finger.Order));
			Assert.That(finger.Order, Is.LessThan(pen.Order));
		}

		[Test]
		public void FireOfAShortcutOnADisabledAutoTapReturnsNull()
		{
			_autoTap.Stop();

			Assert.That(_autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.5f, 0.5f))), Is.Null);
			Assert.That(_autoTap.Scroll(TestScene.At(0.5f, 0.5f), Vector2.up), Is.Null);
		}

		[Test]
		public void AGestureForAPointerMustStartWithPressOrHover()
		{
			Assert.Throws<ArgumentNullException>(() => _autoTap.Mouse(null));
			Assert.Throws<ArgumentException>(() => _autoTap.Pointer("", Gesture.HoverAt(Vector2.one)));
		}
	}
}
#endif
