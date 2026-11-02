#if ENABLE_AUTOTAP
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// What <see cref="AutoTapBase.LogItem"/> predicts about the targets of a press, a drag and a release, following
	/// the rules of the input module.
	/// </summary>
	public class AutoTapPredictionTests : InputTestFixture
	{
		const float k_Dt = 0.04f;

		TestAutoTap _autoTap;
		TestScene _scene;
		readonly List<AutoTapBase.LogEvent> _allLogs = new List<AutoTapBase.LogEvent>();

		public override void Setup()
		{
			base.Setup();
			_scene = new TestScene();
			_autoTap = new TestAutoTap();
			_autoTap.Setup(_scene.Canvas, null);
			_autoTap.Start();
			_allLogs.Clear();
		}

		public override void TearDown()
		{
			_autoTap.Dispose();
			_scene.Dispose();
			base.TearDown();
		}

		void Frame(float dt = k_Dt)
		{
			InputSystem.Update();
			_allLogs.AddRange(_autoTap.GetLatestLogs());
			_autoTap.Update(dt);
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

		[Test]
		public void TheTargetIsThePointerDownReceiverAtTheTimeOfFire()
		{
			var button = _scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);

			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);

			Assert.That(item.Target, Is.SameAs(button.gameObject));
		}

		[Test]
		public void TheTargetIsTheClickReceiverIfNothingHandlesThePointerDown()
		{
			var clickOnly = _scene.Add<ClickOnlyHandler>("Click only", 0.1f, 0.1f, 0.3f, 0.3f);

			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);

			Assert.That(item.Target, Is.SameAs(clickOnly.gameObject));
		}

		[Test]
		public void TheTargetIsTheNearestParentThatHandlesThePointerDown()
		{
			var button = _scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);
			var child = _scene.Add<Plain>("Label", 0.15f, 0.15f, 0.25f, 0.25f, button.transform);

			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);

			Assert.That(item.Target, Is.SameAs(button.gameObject));
			Assert.That(child, Is.Not.Null);
		}

		[Test]
		public void ThePressIsMadeWhereNothingHandlesIt()
		{
			_scene.Add<Plain>("Background", 0f, 0f, 1f, 1f);

			var item = _autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0f);

			Assert.That(item, Is.Not.Null);
			Assert.That(item.Target, Is.Null);
			RunUntilCompleted(item);
			Assert.That(item.ClickExecuted, Is.False);
			Assert.That(Touchscreen.current, Is.Not.Null);
		}

		[Test]
		public void ATapOnAButtonIsPredictedToClickIt()
		{
			var button = _scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);

			RunUntilCompleted(item);

			Assert.That(item.Target, Is.SameAs(button.gameObject));
			Assert.That(item.ClickExecuted, Is.True);
			Assert.That(item.ClickTarget, Is.SameAs(button.gameObject));
			Assert.That(item.Dragged, Is.False);
			Assert.That(item.DragStartedOrder, Is.Null);
			Assert.That(item.DropTarget, Is.Null);
		}

		[Test]
		public void AReleaseOutsideTheButtonIsNotAClick()
		{
			_scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);
			var item = _autoTap.Drag(0, TestScene.At(0.2f, 0.2f), TestScene.At(0.6f, 0.6f), 0.1f);

			RunUntilCompleted(item);

			Assert.That(item.ClickExecuted, Is.False);
			Assert.That(item.ClickTarget, Is.Null);
		}

		[Test]
		public void ADragOverTheThresholdIsPredictedWithItsTargets()
		{
			var source = _scene.Add<DragHandler>("Source", 0.1f, 0.1f, 0.3f, 0.3f);
			var drop = _scene.Add<DropHandler>("Drop", 0.5f, 0.1f, 0.7f, 0.3f);
			var item = _autoTap.Drag(0, TestScene.At(0.2f, 0.2f), TestScene.At(0.6f, 0.2f), 0.1f);

			RunUntilCompleted(item);

			Assert.That(item.Dragged, Is.True);
			Assert.That(item.DragTarget, Is.SameAs(source.gameObject));
			Assert.That(item.DropTarget, Is.SameAs(drop.gameObject));
			Assert.That(item.ClickExecuted, Is.False);
			Assert.That(item.DragStartedOrder, Is.GreaterThan(item.Order));
			Assert.That(item.DragStartedOrder, Is.LessThan(item.CompletedOrder));
			Assert.That(item.DragStartedFrameCount, Is.EqualTo(Time.frameCount));
		}

		[Test]
		public void TheStartOfADragIsInTheLogInTheOrderOfTheChanges()
		{
			_scene.Add<DragHandler>("Source", 0.1f, 0.1f, 0.3f, 0.3f);
			var item = _autoTap.Drag(0, TestScene.At(0.2f, 0.2f), TestScene.At(0.6f, 0.2f), 0.1f);

			RunUntilCompleted(item);

			var mine = _allLogs.Where(e => e.Item == item).ToList();
			Assert.That(
				mine.Select(e => e.Kind),
				Is.EqualTo(new[]
				{
					AutoTapBase.LogEventKind.Started,
					AutoTapBase.LogEventKind.DragStarted,
					AutoTapBase.LogEventKind.Completed,
				}));
			Assert.That(mine[1].Order, Is.EqualTo(item.DragStartedOrder));
			Assert.That(mine[0].Order, Is.LessThan(mine[1].Order));
			Assert.That(mine[1].Order, Is.LessThan(mine[2].Order));
		}

		[Test]
		public void AMoveUnderTheThresholdIsNotADrag()
		{
			var handler = _scene.Add<ClickDragHandler>("Both", 0.1f, 0.1f, 0.3f, 0.3f);
			var from = TestScene.At(0.2f, 0.2f);
			var item = _autoTap.Drag(0, from, from + new Vector2(5, 0), 0.1f);

			RunUntilCompleted(item);

			Assert.That(item.Dragged, Is.False);
			Assert.That(item.DragStartedOrder, Is.Null);
			Assert.That(item.ClickExecuted, Is.True);
			Assert.That(item.ClickTarget, Is.SameAs(handler.gameObject));
			Assert.That(item.DragTarget, Is.SameAs(handler.gameObject), "the drag target is known from the press, whether or not the pointer dragged");
			Assert.That(item.DropTarget, Is.Null);
		}

		[Test]
		public void ADropIsNotPredictedForAPointerThatDidNotDragEvenIfItIsReleasedOverAnObjectThatAcceptsDrops()
		{
			var source = _scene.Add<DragHandler>("Source", 0.1f, 0.1f, 0.3f, 0.3f);
			var drop = _scene.Add<DropHandler>("Drop", 0.3f, 0.1f, 0.5f, 0.3f);
			var edge = TestScene.At(0.3f, 0.2f);

			var shortMove = _autoTap.Drag(0, edge - new Vector2(3, 0), edge + new Vector2(3, 0), 0.1f);
			RunUntilCompleted(shortMove);
			var longMove = _autoTap.Drag(0, TestScene.At(0.2f, 0.2f), TestScene.At(0.4f, 0.2f), 0.1f);
			RunUntilCompleted(longMove);

			Assert.That(shortMove.Dragged, Is.False);
			Assert.That(shortMove.DragTarget, Is.SameAs(source.gameObject));
			Assert.That(shortMove.DropTarget, Is.Null, "the input module delivers no drop for a pointer that did not drag");
			Assert.That(longMove.Dragged, Is.True);
			Assert.That(longMove.DragTarget, Is.SameAs(source.gameObject));
			Assert.That(longMove.DropTarget, Is.SameAs(drop.gameObject), "while it does for one that did");
		}

		[Test]
		public void ADragOnAnObjectThatPressesAndDragsIsStillAClickWhereItIsReleased()
		{
			var handler = _scene.Add<ClickDragHandler>("Both", 0.1f, 0.1f, 0.5f, 0.5f);
			var item = _autoTap.Drag(0, TestScene.At(0.2f, 0.2f), TestScene.At(0.4f, 0.2f), 0.1f);

			RunUntilCompleted(item);

			// The press target is the drag target, so the module keeps the click possible.
			Assert.That(item.Dragged, Is.True);
			Assert.That(item.ClickExecuted, Is.True);
			Assert.That(item.ClickTarget, Is.SameAs(handler.gameObject));
		}

		[Test]
		public void ADragThatStartsOnAChildOfAScrollerCancelsTheClick()
		{
			var scroller = _scene.Add<DragHandler>("Scroller", 0.1f, 0.1f, 0.5f, 0.5f);
			var button = _scene.Add<ClickHandler>("Button", 0.15f, 0.15f, 0.45f, 0.45f, scroller.transform);
			var item = _autoTap.Drag(0, TestScene.At(0.2f, 0.2f), TestScene.At(0.4f, 0.4f), 0.1f);

			RunUntilCompleted(item);

			Assert.That(item.Target, Is.SameAs(button.gameObject));
			Assert.That(item.DragTarget, Is.SameAs(scroller.gameObject));
			Assert.That(item.Dragged, Is.True);
			Assert.That(item.ClickExecuted, Is.False, "the press object is not the drag object, so the click is canceled");
		}

		[Test]
		public void ATapOnASliderStartsADragWithoutMoving()
		{
			var slider = _scene.Add<Slider>("Slider", 0.1f, 0.1f, 0.5f, 0.2f);
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.15f), 0.1f);

			RunUntilCompleted(item);

			Assert.That(item.Dragged, Is.True);
			Assert.That(item.DragTarget, Is.SameAs(slider.gameObject));
		}

		[Test]
		public void AMoveAndAReleaseInOneInputUpdateIsAClickBecauseTheReleaseIsHandledFirst()
		{
			var handler = _scene.Add<ClickDragHandler>("Both", 0.1f, 0.1f, 0.5f, 0.5f);
			_autoTap.Dispose();
			using (var session = VirtualInput.Acquire())
			{
				var finger = session.NewFinger();
				finger.Press(TestScene.At(0.2f, 0.2f));
				InputSystem.Update();

				// The module handles the release before it looks for a drag in the same frame.
				finger.MoveTo(TestScene.At(0.4f, 0.4f));
				finger.Release();
				InputSystem.Update();

				Assert.That(finger.Prediction.ClickTarget, Is.SameAs(handler.gameObject));
				Assert.That(finger.Prediction.Dragging, Is.False);
			}
		}

		[Test]
		public void APressAndAMoveInOneInputUpdateAreHandledAtTheLastPosition()
		{
			var first = _scene.Add<ClickHandler>("First", 0.1f, 0.1f, 0.3f, 0.3f);
			var second = _scene.Add<DragHandler>("Second", 0.5f, 0.5f, 0.7f, 0.7f);
			_autoTap.Dispose();
			using (var session = VirtualInput.Acquire())
			{
				var finger = session.NewFinger();
				finger.Press(TestScene.At(0.2f, 0.2f));
				finger.MoveTo(TestScene.At(0.6f, 0.6f));
				InputSystem.Update();

				// The module raycasts once per frame, at the last position, and presses there.
				Assert.That(finger.Prediction.PointerPress, Is.SameAs(second.gameObject));
				Assert.That(finger.Prediction.PointerDrag, Is.SameAs(second.gameObject));
				Assert.That(finger.Prediction.Dragging, Is.False, "the press position is the last position");
				Assert.That(first, Is.Not.Null);
			}
		}

		[Test]
		public void TheDropTargetIsWhereTheReleaseHappens()
		{
			_scene.Add<DragHandler>("Source", 0.1f, 0.1f, 0.3f, 0.3f);
			_scene.Add<DropHandler>("Drop", 0.5f, 0.1f, 0.7f, 0.3f);
			var elsewhere = _autoTap.Drag(0, TestScene.At(0.2f, 0.2f), TestScene.At(0.9f, 0.9f), 0.1f);

			RunUntilCompleted(elsewhere);

			Assert.That(elsewhere.Dragged, Is.True);
			Assert.That(elsewhere.DropTarget, Is.Null);
		}

		[Test]
		public void TwoFingersAreHandledIndependently()
		{
			// In edit mode Screen is the size of the window that was drawn last, which can be the status bar (40 pixels
			// high), so the drag has to be longer than the drag threshold (10 pixels) at that height.
			var source = _scene.Add<DragHandler>("Source", 0.1f, 0.05f, 0.3f, 0.95f);
			var button = _scene.Add<ClickHandler>("Button", 0.6f, 0.6f, 0.8f, 0.8f);
			var drag = _autoTap.Drag(0, TestScene.At(0.2f, 0.1f), TestScene.At(0.2f, 0.9f), 0.1f);
			var tap = _autoTap.Tap(1, TestScene.At(0.7f, 0.7f), 0.1f);

			RunUntilCompleted(drag);
			RunUntilCompleted(tap);

			Assert.That(drag.Dragged, Is.True);
			Assert.That(drag.DragTarget, Is.SameAs(source.gameObject));
			Assert.That(tap.Dragged, Is.False);
			Assert.That(tap.ClickTarget, Is.SameAs(button.gameObject));
		}

		[Test]
		public void ScenarioTryTapIsTrueWhenThePressIsPredictedToGoToTheTarget()
		{
			var button = _scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);
			var other = _scene.Add<ClickHandler>("Other", 0.5f, 0.1f, 0.7f, 0.3f);
			var cover = _scene.Add<ClickHandler>("Cover", 0.5f, 0.1f, 0.7f, 0.3f);
			var scenario = new TrySenario(_autoTap);

			Assert.That(scenario.Try(0, _scene.Canvas, (RectTransform)button.transform, 0f), Is.True);
			Assert.That(scenario.Try(1, _scene.Canvas, (RectTransform)other.transform, 0f), Is.False, "covered");
			Assert.That(scenario.Try(2, _scene.Canvas, (RectTransform)cover.transform, 0f), Is.True);
			scenario.Dispose();
		}

		[Test]
		public void ScenarioTryTapIsFalseForADestroyedTargetAndWhenTheSlotIsBusy()
		{
			var button = _scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);
			var scenario = new TrySenario(_autoTap);
			Assert.That(scenario.Try(0, _scene.Canvas, (RectTransform)button.transform, 1f), Is.True);

			Assert.That(scenario.Try(0, _scene.Canvas, (RectTransform)button.transform, 1f), Is.False);

			var destroyed = _scene.Add<ClickHandler>("Destroyed", 0.5f, 0.5f, 0.7f, 0.7f);
			var rect = (RectTransform)destroyed.transform;
			Object.DestroyImmediate(destroyed.gameObject);
			Assert.That(scenario.Try(1, _scene.Canvas, rect, 1f), Is.False);
			scenario.Dispose();
		}

		sealed class TrySenario : Scenario
		{
			public TrySenario(AutoTapBase owner) : base(owner)
			{
			}

			public override bool Active { get; protected set; } = true;

			public override Condition RepeatCondition => throw new System.NotSupportedException();

			public override void UpdateActive()
			{
			}

			public bool Try(int index, Canvas canvas, RectTransform target, float duration)
			{
				return TryTap(index, canvas, target, duration);
			}
		}
	}
}
#endif
