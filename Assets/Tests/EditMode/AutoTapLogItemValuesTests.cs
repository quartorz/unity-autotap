#if ENABLE_AUTOTAP
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// The values of a <see cref="AutoTapBase.LogItem"/> that are known so far and change as the operation proceeds:
	/// <c>EndScreenPoint</c>, <c>Duration</c> and <c>CurrentStep</c>, and what <c>ToString</c> writes of them.
	/// </summary>
	public class AutoTapLogItemValuesTests : InputTestFixture
	{
		const float k_Dt = 0.04f;
		const float k_Tolerance = 0.001f;

		TestAutoTap _autoTap;
		TestScene _scene;

		public override void Setup()
		{
			base.Setup();
			_scene = new TestScene();
			_autoTap = new TestAutoTap();
			_autoTap.SetupMarkers(_scene.Canvas, null);
			_autoTap.Start();
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
		}

		/// <summary>
		/// Runs frames until the item ended, and gives the value that <paramref name="read"/> had after the call of
		/// <c>Simulate</c> and after each frame.
		/// </summary>
		List<T> Record<T>(AutoTapBase.LogItem item, System.Func<AutoTapBase.LogItem, T> read)
		{
			var values = new List<T> { read(item) };
			for (var i = 0; i < 100 && !item.Completed; ++i)
			{
				Frame();
				values.Add(read(item));
			}

			Assert.That(item.Completed, Is.True);
			return values;
		}

		static List<T> Distinct<T>(IEnumerable<T> values)
		{
			var result = new List<T>();
			foreach (var value in values)
			{
				if (result.Count == 0 || !Equals(result[result.Count - 1], value))
				{
					result.Add(value);
				}
			}

			return result;
		}

		static void AssertPoint(Vector2? actual, Vector2 expected, string message = null)
		{
			Assert.That(actual.HasValue, Is.True, message);
			Assert.That(actual.Value.x, Is.EqualTo(expected.x).Within(0.01f), message);
			Assert.That(actual.Value.y, Is.EqualTo(expected.y).Within(0.01f), message);
		}

		// EndScreenPoint

		[Test]
		public void TheEndPointOfATapIsItsPointBeforeAndAfterTheEnd()
		{
			var p = TestScene.At(0.2f, 0.2f);

			var item = _autoTap.Tap(0, p, 0.1f);

			Assert.That(item.EndScreenPoint, Is.EqualTo(p));
			RunUntilCompleted(item);
			Assert.That(item.EndScreenPoint, Is.EqualTo(p));
		}

		[Test]
		public void TheEndPointOfADragIsItsDestinationUntilTheEndAndWhereItWasPutAfterwards()
		{
			var from = TestScene.At(0.2f, 0.2f);
			var to = TestScene.At(0.6f, 0.2f);
			var item = _autoTap.Drag(0, from, to, 0.2f);
			Assert.That(item.EndScreenPoint, Is.EqualTo(to), "the plan puts it there");
			Frames(2);
			Assert.That(item.Completed, Is.False);
			Assert.That(item.EndScreenPoint, Is.EqualTo(to));

			_autoTap.Stop();

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			AssertPoint(item.EndScreenPoint, Vector2.Lerp(from, to, 0.4f), "two frames of 0.04 s of a move that takes 0.2 s");
			Assert.That(item.EndScreenPoint.Value.x, Is.LessThan(to.x));
		}

		[Test]
		public void TheEndPointOfAFinishedDragIsItsDestination()
		{
			var from = TestScene.At(0.2f, 0.2f);
			var to = TestScene.At(0.6f, 0.2f);

			var item = _autoTap.Drag(0, from, to, 0.1f);
			RunUntilCompleted(item);

			AssertPoint(item.EndScreenPoint, to);
		}

		[Test]
		public void TheEndPointOfAMoveToAFunctionIsUnknownUntilTheMoveStarts()
		{
			var first = TestScene.At(0.3f, 0.2f);
			var later = TestScene.At(0.7f, 0.2f);
			var destination = first;
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).Hold(0.1f).MoveTo(() => destination, 0.08f));
			Assert.That(item.EndScreenPoint, Is.Null, "the function was not called yet");

			destination = later;
			Frames(2);
			Assert.That(item.EndScreenPoint, Is.Null, "the move did not start");
			Frame(); // the hold is over in this frame and the move starts

			AssertPoint(item.EndScreenPoint, later, "the function is called when the move starts");
			RunUntilCompleted(item);
			AssertPoint(item.EndScreenPoint, later);
		}

		[Test]
		public void AFixedMoveAfterAMoveToAFunctionMakesTheEndKnown()
		{
			var end = TestScene.At(0.6f, 0.2f);

			var item = _autoTap.Do(
				0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).MoveTo(() => TestScene.At(0.4f, 0.2f), 0.1f).MoveTo(end, 0.1f));

			Assert.That(item.EndScreenPoint, Is.EqualTo(end));
		}

		[Test]
		public void AMoveToAFunctionAfterAFixedMoveMakesTheEndUnknown()
		{
			var later = TestScene.At(0.6f, 0.2f);
			var item = _autoTap.Do(
				0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).MoveTo(TestScene.At(0.4f, 0.2f), 0.04f).MoveTo(() => later, 0.04f));

			Assert.That(item.EndScreenPoint, Is.Null);
			Frame();
			Assert.That(item.EndScreenPoint, Is.Null, "the first move is going on");
			Frame();

			AssertPoint(item.EndScreenPoint, later);
		}

		[Test]
		public void TheEndPointOfAHoverIsItsPoint()
		{
			var p = TestScene.At(0.2f, 0.2f);

			var item = _autoTap.Mouse(Gesture.HoverAt(p).Hold(0.1f));

			Assert.That(item.EndScreenPoint, Is.EqualTo(p));
			RunUntilCompleted(item);
			Assert.That(item.EndScreenPoint, Is.EqualTo(p));
		}

		[Test]
		public void TheEndPointOfAPointerFollowsItsLastMove()
		{
			var from = TestScene.At(0.2f, 0.2f);
			var to = TestScene.At(0.6f, 0.4f);

			var item = _autoTap.Mouse(Gesture.PressAt(from).MoveTo(to, 0.1f).Hold(0.04f));

			Assert.That(item.EndScreenPoint, Is.EqualTo(to));
			RunUntilCompleted(item);
			AssertPoint(item.EndScreenPoint, to);
		}

		[Test]
		public void AnOperationOfADeviceHasNoEndPoint()
		{
			var item = _autoTap.Key(Key.A, 0.1f);
			Assert.That(item.EndScreenPoint, Is.Null);

			var values = Record(item, i => i.EndScreenPoint);

			Assert.That(values, Is.All.Null);
			Assert.That(item.ScreenPoint, Is.EqualTo(Vector2.zero));
		}

		[Test]
		public void TheEndPointOfADroppedOperationIsWhereItWasPut()
		{
			var p = TestScene.At(0.2f, 0.2f);
			var item = _autoTap.Tap(0, p, 1f);
			InputSystem.DisableDevice(Touchscreen.current);

			Frame();

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Dropped));
			Assert.That(item.EndScreenPoint, Is.EqualTo(p));
		}

		// Duration

		[Test]
		public void TheDurationOfATapIsItsHoldUntilItEndsAndWhatWasCountedAfterwards()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0.1f);
			Assert.That(item.Duration, Is.EqualTo(0.1f).Within(k_Tolerance));

			Frames(3);

			Assert.That(item.Completed, Is.False, "the release is queued; the next input update processes it");
			Assert.That(item.Duration, Is.EqualTo(0.1f).Within(k_Tolerance), "still the plan");

			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.Duration, Is.EqualTo(0.12f).Within(k_Tolerance), "three frames of 0.04 s counted the hold");
		}

		[Test]
		public void TheDurationIsTheSumOfTheTimesOfTheSteps()
		{
			var item = _autoTap.Do(
				0,
				Gesture.PressAt(TestScene.At(0.2f, 0.2f))
					.Hold(0.1f)
					.MoveTo(TestScene.At(0.4f, 0.2f), 0.2f)
					.Set("pressure", 1f, 0.3f)
					.Hold(0.05f));

			Assert.That(item.Duration, Is.EqualTo(0.65f).Within(k_Tolerance));
		}

		[Test]
		public void TheDurationOfAnOperationThatWasStoppedIsWhatWasCountedUntilThen()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 5f);
			Assert.That(item.Duration, Is.EqualTo(5f).Within(k_Tolerance));
			Frames(3);

			_autoTap.Stop();

			Assert.That(item.Duration, Is.EqualTo(0.12f).Within(k_Tolerance), "three frames of 0.04 s");
		}

		[Test]
		public void TheDurationIsCountedInTheTimePassedToUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0.5f);

			Frame(0.3f);
			Frame(0.3f);
			Frame(0.3f);
			RunUntilCompleted(item);

			Assert.That(item.Duration, Is.EqualTo(0.6f).Within(k_Tolerance), "two frames of 0.3 s used up the hold");
		}

		[Test]
		public void TheDurationOfAHoldUntilIsUnknownUntilItsConditionIsMet()
		{
			var ready = false;
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).HoldUntil(() => ready, 5f));
			Assert.That(item.Duration, Is.Null);

			Frames(2);
			Assert.That(item.Duration, Is.Null, "still waiting");

			ready = true;
			Frame();

			Assert.That(item.Completed, Is.False);
			Assert.That(item.Duration, Is.EqualTo(0.08f).Within(k_Tolerance), "what it waited");
			Frame();
			Assert.That(item.Completed, Is.True);
			Assert.That(item.Duration, Is.EqualTo(0.12f).Within(k_Tolerance));
		}

		[Test]
		public void TheDurationAddsWhatAHoldUntilWaitedToTheTimesOfTheOtherSteps()
		{
			var ready = false;
			var item = _autoTap.Do(
				0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).Hold(0.1f).HoldUntil(() => ready, 5f).Hold(0.2f));
			Assert.That(item.Duration, Is.Null);

			Frames(5);
			Assert.That(item.Duration, Is.Null);
			ready = true;
			Frame();

			Assert.That(item.Duration, Is.EqualTo(0.4f).Within(k_Tolerance), "0.1 s, the 0.1 s that it waited, and 0.2 s");
		}

		[Test]
		public void TheDurationOfAHoldUntilThatTimedOutIsKnownOnlyOnceItEnded()
		{
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).HoldUntil(() => false, 0.1f));

			var values = Record(item, i => i.Duration);

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.TimedOut));
			Assert.That(values.Take(values.Count - 1), Is.All.Null);
			Assert.That(item.Duration, Is.EqualTo(0.12f).Within(k_Tolerance));
		}

		[Test]
		public void TheDurationOfADeviceOperationFollowsTheSameRules()
		{
			var item = _autoTap.Key(Key.A, 0.1f);
			Assert.That(item.Duration, Is.EqualTo(0.1f).Within(k_Tolerance));

			RunUntilCompleted(item);

			Assert.That(item.Duration, Is.EqualTo(0.12f).Within(k_Tolerance));
		}

		[Test]
		public void TheDurationOfADeviceOperationWithAHoldUntilIsUnknownUntilItsConditionIsMet()
		{
			var ready = false;
			var item = _autoTap.Gamepad(
				new ControlSequence().Press(GamepadButton.South).HoldUntil(() => ready, 5f).Release(GamepadButton.South));
			Assert.That(item.Duration, Is.Null);
			Frames(3);
			Assert.That(item.Duration, Is.Null);

			ready = true;
			Frame();

			Assert.That(item.Duration, Is.Not.Null);
			RunUntilCompleted(item);
			Assert.That(item.Duration, Is.GreaterThan(0.1f));
		}

		[Test]
		public void TheDurationOfAnOperationThatNeverRanIsZero()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.DisableDevice(Touchscreen.current);

			Frame();

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Dropped));
			Assert.That(item.Duration, Is.EqualTo(0f));
		}

		[Test]
		public void AnItemIsTheSameObjectInEveryEventAndHasTheValuesOfTheTimeItIsRead()
		{
			var logs = new List<AutoTapBase.LogEvent>();
			var snapshots = new List<(AutoTapBase.LogEventKind Kind, float? Duration, int Step, bool Completed)>();
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0.1f);
			for (var i = 0; i < 10 && !item.Completed; ++i)
			{
				InputSystem.Update();
				_autoTap.Update(k_Dt);
				foreach (var log in _autoTap.GetLatestLogs())
				{
					logs.Add(log);
					snapshots.Add((log.Kind, log.Item.Duration, log.Item.CurrentStep, log.Item.Completed));
				}
			}

			InputSystem.Update();
			_autoTap.Update(k_Dt);
			foreach (var log in _autoTap.GetLatestLogs())
			{
				logs.Add(log);
				snapshots.Add((log.Kind, log.Item.Duration, log.Item.CurrentStep, log.Item.Completed));
			}

			Assert.That(logs.Select(l => l.Kind), Is.EqualTo(new[]
			{
				AutoTapBase.LogEventKind.Started,
				AutoTapBase.LogEventKind.Completed,
			}));
			Assert.That(logs.Select(l => l.Item), Is.All.SameAs(item));
			Assert.That(snapshots[0].Completed, Is.False);
			Assert.That(snapshots[0].Duration, Is.EqualTo(0.1f).Within(k_Tolerance), "at the start: the plan");
			Assert.That(snapshots[0].Step, Is.EqualTo(1), "at the start: under way");
			Assert.That(snapshots[1].Completed, Is.True);
			Assert.That(snapshots[1].Duration, Is.EqualTo(0.12f).Within(k_Tolerance), "at the end: what was counted");
		}

		// CurrentStep

		[Test]
		public void TheCurrentStepOfAFingerIsMinusOneUntilItsPressWasProcessedAndThenFollowsThePlan()
		{
			var gesture = Gesture.PressAt(TestScene.At(0.2f, 0.2f))
				.Hold(0.1f)
				.MoveTo(TestScene.At(0.4f, 0.2f), 0.1f)
				.Hold(0.1f);
			var item = _autoTap.Do(0, gesture);

			var values = Record(item, i => i.CurrentStep);

			Assert.That(Distinct(values), Is.EqualTo(new[] { -1, 1, 2, 3 }));
			Assert.That(values[0], Is.EqualTo(-1), "right after Simulate");
			Assert.That(values[1], Is.EqualTo(1), "the press was processed, and the plan waits in the first hold");
			Assert.That(item.CurrentStep, Is.EqualTo(gesture.Steps.Count - 1), "the plan was carried out to its end");
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void TheCurrentStepIsTheIndexInTheStepsOfTheGesture()
		{
			var gesture = Gesture.PressAt(TestScene.At(0.2f, 0.2f))
				.Hold(0.1f)
				.MoveTo(TestScene.At(0.4f, 0.2f), 0.1f)
				.Hold(0.1f);
			var item = _autoTap.Do(0, gesture);

			var kinds = Record(item, i => i.CurrentStep < 0 ? (Gesture.StepKind?)null : i.Gesture.Steps[i.CurrentStep].Kind);

			Assert.That(Distinct(kinds), Is.EqualTo(new Gesture.StepKind?[]
			{
				null, Gesture.StepKind.Hold, Gesture.StepKind.Move, Gesture.StepKind.Hold,
			}));
		}

		[Test]
		public void TheCurrentStepOfADeviceOperationIsTheIndexInTheStepsOfTheSequence()
		{
			var sequence = new ControlSequence().Press(Key.A).Hold(0.1f).Release(Key.A);
			var item = _autoTap.Device("Keyboard", sequence);

			var values = Record(item, i => i.CurrentStep);

			Assert.That(values[0], Is.EqualTo(-1), "what Simulate queued was not processed yet");
			Assert.That(Distinct(values), Is.EqualTo(new[] { -1, 1, 2 }));
			Assert.That(item.CurrentStep, Is.EqualTo(sequence.Steps.Count - 1));
		}

		[Test]
		public void AnOperationThatWaitsAtItsFirstStepIsAtStepZeroFromTheStart()
		{
			var ready = false;
			var sequence = new ControlSequence().HoldUntil(() => ready, 5f).Press(Key.A).Hold(0.04f).Release(Key.A);

			var item = _autoTap.Device("Keyboard", sequence);

			Assert.That(item.CurrentStep, Is.EqualTo(0), "Simulate queued nothing, so there is nothing to wait for");
			Frames(3);
			Assert.That(item.CurrentStep, Is.EqualTo(0), "still waiting");
			ready = true;
			var values = Record(item, i => i.CurrentStep);
			Assert.That(Distinct(values).First(), Is.EqualTo(0));
			Assert.That(item.CurrentStep, Is.EqualTo(3));
		}

		[Test]
		public void TheCurrentStepOfAnOperationThatTimedOutIsTheHoldUntil()
		{
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).Hold(0.04f).HoldUntil(() => false, 0.08f));

			RunUntilCompleted(item);

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.TimedOut));
			Assert.That(item.CurrentStep, Is.EqualTo(2));
		}

		[Test]
		public void TheCurrentStepOfARefusedOperationIsTheStepThatWasRefused()
		{
			var blocked = _scene.Add<ClickHandler>("Blocked", 0.1f, 0.1f, 0.3f, 0.3f);
			_autoTap.Refuse = topmost => topmost.gameObject == blocked.gameObject;
			var open = TestScene.At(0.7f, 0.7f);

			var item = _autoTap.Mouse(
				Gesture.PressAt(open, "middleButton").MoveTo(TestScene.At(0.2f, 0.2f), 0f).Press("rightButton").Hold(1f));
			RunUntilCompleted(item);

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Refused));
			Assert.That(item.CurrentStep, Is.EqualTo(2));
		}

		[Test]
		public void TheCurrentStepOfAnOperationThatWasStoppedIsTheStepItWasAt()
		{
			var item = _autoTap.Do(
				0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).Hold(0.04f).MoveTo(TestScene.At(0.4f, 0.2f), 1f));
			Frames(3);

			_autoTap.Stop();

			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(item.CurrentStep, Is.EqualTo(2));
		}

		[Test]
		public void TheCurrentStepIsMinusOneForAnOperationThatEndedBeforeItWasUnderWay()
		{
			var dropped = _autoTap.Tap(1, TestScene.At(0.4f, 0.2f), 1f);
			InputSystem.DisableDevice(Touchscreen.current);
			Frame();
			Assert.That(dropped.EndReason, Is.EqualTo(AutoTapBase.EndReason.Dropped));
			Assert.That(dropped.CurrentStep, Is.EqualTo(-1));

			InputSystem.EnableDevice(Touchscreen.current);
			var cutOff = _autoTap.Tap(2, TestScene.At(0.6f, 0.2f), 1f);
			_autoTap.Stop();

			Assert.That(cutOff.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			Assert.That(cutOff.Order, Is.Null);
			Assert.That(cutOff.CurrentStep, Is.EqualTo(-1));
		}

		[Test]
		public void TheCurrentStepOfAnOperationThatChangedNothingIsTheLastStepOnceItEnded()
		{
			// Hovering where the pointer already is writes nothing that changes, so the operation is never started.
			var hover = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f)));
			RunUntilCompleted(hover);
			var again = _autoTap.Mouse(Gesture.HoverAt(TestScene.At(0.2f, 0.2f)).Hold(0.04f));

			RunUntilCompleted(again);

			Assert.That(again.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(again.CurrentStep, Is.EqualTo(1));
		}

		// ToString

		[Test]
		public void ToStringWritesTheDurationAndTheEndPoint()
		{
			var from = TestScene.At(0.2f, 0.2f);
			var to = TestScene.At(0.6f, 0.2f);

			var item = _autoTap.Drag(0, from, to, 0.1f);

			Assert.That(item.ToString(), Does.StartWith("Finger Index: 0 FrameCount: "));
			Assert.That(item.ToString(), Does.Contain(", Duration: 0.10, DateTime: "));
			Assert.That(item.ToString(), Does.EndWith($"\nScreenPoint: {from} → {to} GameObject: (none)"));
			RunUntilCompleted(item);
			Assert.That(item.ToString(), Does.Contain(", Duration: 0.12, DateTime: "));
			Assert.That(item.ToString(), Does.EndWith($"\nScreenPoint: {from} → {item.EndScreenPoint.Value} GameObject: (none)"));
		}

		[Test]
		public void ToStringLeavesOutWhatIsNotKnown()
		{
			var ready = false;
			var from = TestScene.At(0.2f, 0.2f);
			var holding = _autoTap.Do(0, Gesture.PressAt(from).HoldUntil(() => ready, 5f));
			var moving = _autoTap.Do(1, Gesture.PressAt(from).MoveTo(() => TestScene.At(0.4f, 0.2f), 0.1f));

			Assert.That(holding.ToString(), Does.Not.Contain("Duration"));
			Assert.That(holding.ToString(), Does.Contain($"\nScreenPoint: {from} → {from} GameObject: "));
			Assert.That(moving.ToString(), Does.Contain(", Duration: 0.10, "));
			Assert.That(moving.ToString(), Does.Not.Contain("→"));
			Assert.That(moving.ToString(), Does.EndWith($"\nScreenPoint: {from} GameObject: (none)"));
			Assert.That(holding.ToString(), Does.Not.Contain("null"));
			Assert.That(moving.ToString(), Does.Not.Contain("null"));
		}

		[Test]
		public void ToStringKeepsTheObjectThatWasHit()
		{
			var button = _scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);
			var hit = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0.1f);
			var miss = _autoTap.Tap(1, TestScene.At(0.8f, 0.8f), 0.1f);

			Assert.That(hit.Target, Is.SameAs(button.gameObject));
			Assert.That(hit.ToString(), Does.EndWith("GameObject: Button"));
			Assert.That(miss.ToString(), Does.EndWith("GameObject: (none)"));
		}

		[Test]
		public void ToStringOfADeviceOperationWritesTheDurationAndTheDeviceButNoPoints()
		{
			var item = _autoTap.Key(Key.A, 0.1f);

			var text = item.ToString();

			Assert.That(text, Does.StartWith("Device Index: -1 FrameCount: "));
			Assert.That(text, Does.Contain(", Duration: 0.10, DateTime: "));
			Assert.That(text, Does.EndWith("\nDevice: AutoTap-Keyboard-0"));
			Assert.That(text, Does.Not.Contain("ScreenPoint"));
			Assert.That(text, Does.Not.Contain("→"));
		}

		[Test]
		public void ToStringDoesNotWriteTheSteps()
		{
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).Hold(0.1f).MoveTo(TestScene.At(0.4f, 0.2f), 0.1f));

			Assert.That(item.ToString().Split('\n'), Has.Length.EqualTo(2));
			Assert.That(item.ToString(), Does.Not.Contain("Step"));
			Assert.That(item.ToString(), Does.Not.Contain("Hold"));
		}
	}
}
#endif
