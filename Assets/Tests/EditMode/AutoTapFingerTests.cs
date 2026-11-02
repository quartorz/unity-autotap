#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// The operations of fingers in slots, on the Input System of <see cref="InputTestFixture"/>. One frame is an
	/// input update followed by <see cref="AutoTapBase.Update(float)"/>, as in the player loop.
	/// </summary>
	public class AutoTapFingerTests : InputTestFixture
	{
		const float k_Dt = 0.04f;

		TestAutoTap _autoTap;
		TestScene _scene;
		InputChangeReader _reader;
		List<InputChange> _changes;

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
			_autoTap.Update(dt);
		}

		List<AutoTapBase.LogEvent> Logs()
		{
			return _autoTap.GetLatestLogs().ToList();
		}

		[Test]
		public void FireQueuesThePressAndDoesNotProcessIt()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0.1f);

			Assert.That(item, Is.Not.Null);
			Assert.That(item.Index, Is.EqualTo(0));
			Assert.That(item.Kind, Is.EqualTo(AutoTapBase.LogItemKind.Tap));
			Assert.That(item.Order, Is.Null);
			Assert.That(item.Completed, Is.False);
			Assert.That(Touchscreen.current.touches[0].isInProgress, Is.False);
			Assert.That(_changes, Is.Empty);
		}

		[Test]
		public void ATapIsPressedInOneInputUpdateAndReleasedInALaterOne()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0f);

			Frame();
			var first = Logs();
			Frame();
			var second = Logs();

			Assert.That(first.Select(e => e.Kind), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }));
			Assert.That(first[0].Item, Is.SameAs(item));
			Assert.That(first[0].Order, Is.EqualTo(item.Order));
			Assert.That(second.Select(e => e.Kind), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
			Assert.That(item.CompletedOrder, Is.GreaterThan(item.Order));
			Assert.That(second[0].Order, Is.EqualTo(item.CompletedOrder));
		}

		[Test]
		public void TheSlotIsFreeAndReportedInTheFrameThatProcessedTheEnd()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0f);
			var empty = new List<string>();
			_autoTap.OnEmptySlot = index => empty.Add($"empty {index}");

			Frame();
			Assert.That(_autoTap.Completed, Is.Empty);
			Frame();

			Assert.That(_autoTap.Completed.Select(p => p.Value), Is.EqualTo(new[] { item }));
			Assert.That(_autoTap.Completed[0].Key, Is.EqualTo(0));
			Assert.That(empty, Does.Contain("empty 0"));
			Assert.That(_autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0f), Is.Not.Null);
		}

		[Test]
		public void FireOnARunningSlotReturnsNull()
		{
			_autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 1f);
			Frame();

			Assert.That(_autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 1f), Is.Null);
			Assert.That(_autoTap.Tap(1, TestScene.At(0.5f, 0.5f), 1f), Is.Not.Null);
		}

		[Test]
		public void FireOutsideTheScreenReturnsNull()
		{
			Assert.That(_autoTap.Tap(0, new Vector2(-1, 10), 0f), Is.Null);
			Assert.That(_autoTap.Tap(0, new Vector2(10, Screen.height + 1), 0f), Is.Null);
			Assert.That(_autoTap.Drag(0, new Vector2(10, 10), new Vector2(Screen.width + 1, 10), 0.1f), Is.Null);
			Assert.That(Touchscreen.current, Is.Null.Or.Property("added").False.Or.Matches<Touchscreen>(t => !t.touches[0].isInProgress));
		}

		[Test]
		public void FireWhileNotStartedReturnsNull()
		{
			_autoTap.Stop();

			Assert.That(_autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0f), Is.Null);
		}

		[Test]
		public void FireOnASlotThatDoesNotExistThrows()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => _autoTap.Tap(3, TestScene.At(0.5f, 0.5f), 0f));
			Assert.Throws<ArgumentOutOfRangeException>(() => _autoTap.Tap(-1, TestScene.At(0.5f, 0.5f), 0f));
		}

		[Test]
		public void AHoldReleasesInTheFirstFrameThatReachesItsTime()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0.1f);

			Frame(); // the press is processed; 0.04 s are held
			Frame(); // 0.08 s
			Assert.That(item.Completed, Is.False);
			Frame(); // 0.12 s: the release is queued by Update, and processed by the next input update
			Assert.That(item.Completed, Is.False);
			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void ADragQueuesOnePositionPerFrameAndEndsAtTheDestination()
		{
			var from = TestScene.At(0.2f, 0.2f);
			var to = TestScene.At(0.6f, 0.4f);
			var item = _autoTap.Drag(0, from, to, 0.1f);
			Assert.That(item.DragTo, Is.EqualTo(to));

			Frame(); // press processed; the first Update queues t = 0.4
			Frame(); // processes t = 0.4; queues t = 0.8
			var afterFirstMove = _changes.OfType<TouchMoved>().Last().Position;
			Frame(); // processes t = 0.8; queues the destination and the release
			var afterSecondMove = _changes.OfType<TouchMoved>().Last().Position;
			Frame(); // processes the destination and the release

			Assert.That(Vector2.Distance(afterFirstMove, Vector2.Lerp(from, to, 0.4f)), Is.LessThan(0.01f));
			Assert.That(Vector2.Distance(afterSecondMove, Vector2.Lerp(from, to, 0.8f)), Is.LessThan(0.01f));
			var ended = _changes.OfType<TouchEnded>().Single();
			Assert.That(Vector2.Distance(ended.Position, to), Is.LessThan(0.01f));
			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void ATimeThatTheStepDoesNotUseGoesToTheNextStep()
		{
			var p = TestScene.At(0.2f, 0.2f);
			var q = TestScene.At(0.4f, 0.2f);
			var r = TestScene.At(0.4f, 0.4f);
			var gesture = Gesture.PressAt(p).MoveTo(q, 0.05f).MoveTo(r, 0.05f);
			_autoTap.Do(0, gesture);

			Frame(0.04f); // t of the first move = 0.8
			Frame(0.04f); // the first move ends with 0.03 left; the second starts with t = 0.6
			Frame(0.04f); // processes it; the second move ends with 0.03 left

			var positions = _changes.OfType<TouchMoved>().Select(m => m.Position).ToList();
			Assert.That(Vector2.Distance(positions[0], Vector2.Lerp(p, q, 0.8f)), Is.LessThan(0.01f));
			// The two events of the second frame are merged by the touchscreen into the last one.
			Assert.That(Vector2.Distance(positions.Last(), Vector2.Lerp(q, r, 0.6f)), Is.LessThan(0.01f));
		}

		[Test]
		public void HoldUntilWaitsForTheCondition()
		{
			var ready = false;
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.5f, 0.5f)).HoldUntil(() => ready, 1f));

			Frame();
			Frame();
			Frame();
			Assert.That(item.Completed, Is.False);
			ready = true;
			Frame(); // the release is queued
			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void HoldUntilTimesOutAndReleases()
		{
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.5f, 0.5f)).HoldUntil(() => false, 0.1f));

			for (var i = 0; i < 3; ++i)
			{
				Frame();
				Assert.That(item.Completed, Is.False);
			}

			Frame(); // 0.12 s: timed out and the release is queued
			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.TimedOut));
			Assert.That(_changes.OfType<TouchEnded>().Count(), Is.EqualTo(1));
		}

		[Test]
		public void AMoveToAFunctionTakesItsDestinationWhenTheMoveStarts()
		{
			var destination = TestScene.At(0.3f, 0.3f);
			var from = TestScene.At(0.5f, 0.5f);
			var item = _autoTap.Do(0, Gesture.PressAt(from).Hold(0.05f).MoveTo(() => destination, 0.04f));

			Frame(); // press processed; 0.04 s held
			destination = TestScene.At(0.7f, 0.2f);
			Frame(); // the hold ends with 0.03 left; the move starts and queues t = 0.75 of the new destination
			Frame();

			Assert.That(item.DragTo, Is.EqualTo(destination));
			var moved = _changes.OfType<TouchMoved>().First();
			Assert.That(Vector2.Distance(moved.Position, Vector2.Lerp(from, destination, 0.75f)), Is.LessThan(0.01f));
		}

		[Test]
		public void ASetStepWritesPressure()
		{
			_autoTap.Do(0, Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Set("pressure", 0.2f).Hold(0.2f));

			Frame(); // the press and the pressure are processed by the same input update

			Assert.That(Touchscreen.current.touches[0].pressure.ReadValue(), Is.EqualTo(0.2f).Within(0.0001f));
		}

		[Test]
		public void TheStepsThatFollowThePressAndTakeNoTimeAreQueuedByFireAsEventsOfTheirOwn()
		{
			var p = TestScene.At(0.2f, 0.2f);
			var q = TestScene.At(0.6f, 0.4f);

			var item = _autoTap.Do(0, Gesture.PressAt(p).Set("pressure", 0.5f).MoveTo(q, 0f).Hold(0.2f));

			Assert.That(item, Is.Not.Null);
			Assert.That(_changes, Is.Empty, "Fire processes nothing");
			Assert.That(item.DragTo, Is.EqualTo(q), "the move was carried out by Fire");
			Frame();

			Assert.That(_changes.Select(c => c.UpdateCount).Distinct().Count(), Is.EqualTo(1), "one input update processes them all");
			Assert.That(_changes.First(), Is.TypeOf<TouchBegan>());
			Assert.That(((TouchBegan)_changes.First()).Position, Is.EqualTo(p));
			Assert.That(_changes.Skip(1), Is.All.TypeOf<TouchMoved>());
			var last = (TouchMoved)_changes.Last();
			Assert.That(last.Position, Is.EqualTo(q), "in the order of the plan");
			Assert.That(Touchscreen.current.touches[0].pressure.ReadValue(), Is.EqualTo(0.5f).Within(0.0001f));
			Assert.That(item.Order, Is.EqualTo(_changes.First().Order));
			Assert.That(item.Completed, Is.False);
		}

		[Test]
		public void AStepThatTakesTimeIsLeftToTheFramesAfterFire()
		{
			var p = TestScene.At(0.2f, 0.2f);
			var q = TestScene.At(0.6f, 0.4f);
			var item = _autoTap.Do(0, Gesture.PressAt(p).MoveTo(q, 0.1f));

			Frame(); // the press is processed; the move starts with this frame's time
			Assert.That(_changes.OfType<TouchMoved>(), Is.Empty);
			Frame();

			Assert.That(Vector2.Distance(_changes.OfType<TouchMoved>().First().Position, Vector2.Lerp(p, q, 0.4f)), Is.LessThan(0.01f));
			Assert.That(item.Completed, Is.False);
		}

		[Test]
		public void AHoldUntilIsAskedByFireAndTheGestureIsOverAtOnceIfItIsMet()
		{
			var asked = 0;
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.5f, 0.5f)).HoldUntil(() => ++asked > 0, 1f));

			Assert.That(asked, Is.EqualTo(1), "Fire carried out the step");
			Frame(); // the press is processed
			Assert.That(item.Completed, Is.False, "what is pressed is not released by the update that pressed it");
			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void ASetStepThatTakesTimeChangesTheValueLinearly()
		{
			_autoTap.Do(0, Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Set("pressure", 0f).Set("pressure", 1f, 0.1f));

			Frame(); // the press and the 0 are processed; 0.4 of the way is queued
			Frame(); // 0.4 of the way is processed; 0.8 is queued

			Assert.That(Touchscreen.current.touches[0].pressure.ReadValue(), Is.EqualTo(0.4f).Within(0.0001f));
			Frame();
			Assert.That(Touchscreen.current.touches[0].pressure.ReadValue(), Is.EqualTo(0.8f).Within(0.0001f));
		}

		[Test]
		public void AGestureThatDoesNotFitAFingerThrows()
		{
			var p = TestScene.At(0.5f, 0.5f);

			Assert.Throws<ArgumentException>(() => _autoTap.Do(0, Gesture.HoverAt(p)));
			Assert.Throws<ArgumentException>(() => _autoTap.Do(0, Gesture.PressAt(p).Scroll(Vector2.up)));
			Assert.Throws<ArgumentException>(() => _autoTap.Do(0, Gesture.PressAt(p, "rightButton")));
			Assert.Throws<ArgumentException>(() => _autoTap.Do(0, Gesture.PressAt(p).Press("rightButton")));
			Assert.Throws<ArgumentException>(() => _autoTap.Do(0, Gesture.PressAt(p).Set("tapCount", 1)));
			Assert.Throws<ArgumentException>(() => _autoTap.Do(0, Gesture.PressAt(p).Set("pressure", Vector2.one)));
			Assert.That(_autoTap.Tap(0, p, 0f), Is.Not.Null, "the slot was not used by the failed calls");
		}

		[Test]
		public void AGestureThatEndsWithAReleaseIsTheSameAsOneThatDoesNot()
		{
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Release());

			Frame();
			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Finished));
		}

		[Test]
		public void ShouldNotPressOnRefusesAPressOnTheTopmostObject()
		{
			var blocked = _scene.Add<ClickHandler>("Blocked", 0.1f, 0.1f, 0.3f, 0.3f);
			var allowed = _scene.Add<ClickHandler>("Allowed", 0.5f, 0.1f, 0.7f, 0.3f);
			RaycastResultSeen seen = null;
			_autoTap.Refuse = topmost =>
			{
				seen = new RaycastResultSeen { GameObject = topmost.gameObject };
				return topmost.gameObject == blocked.gameObject;
			};

			var refused = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);
			var accepted = _autoTap.Tap(0, TestScene.At(0.6f, 0.2f), 0f);

			Assert.That(refused, Is.Null);
			Assert.That(accepted, Is.Not.Null);
			Assert.That(seen.GameObject, Is.SameAs(allowed.gameObject));
		}

		sealed class RaycastResultSeen
		{
			public GameObject GameObject;
		}

		[Test]
		public void ShouldNotPressOnIsNotAskedWhereNothingIsHit()
		{
			var asked = 0;
			_autoTap.Refuse = _ =>
			{
				++asked;
				return true;
			};

			var item = _autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0f);

			Assert.That(asked, Is.Zero);
			Assert.That(item, Is.Not.Null, "a press is made even where nothing is hit");
			Assert.That(item.Target, Is.Null);
		}

		[Test]
		public void FireReturnsNullWhenNoFingerIsFree()
		{
			using (var other = VirtualInput.Acquire())
			{
				var held = new List<VirtualFinger>();
				for (var i = 0; i < 10; ++i)
				{
					var finger = other.NewFinger();
					finger.Press(new Vector2(10, 10));
					held.Add(finger);
				}

				Assert.That(_autoTap.Tap(0, TestScene.At(0.5f, 0.5f), 0f), Is.Null);
			}
		}
	}
}
#endif
