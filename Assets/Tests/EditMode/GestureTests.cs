#if ENABLE_AUTOTAP
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// What <see cref="Gesture"/> and <see cref="ControlSequence"/> accept when they are built, and the steps that they
	/// show afterwards.
	/// </summary>
	public class GestureTests
	{
		[Test]
		public void ATimeMustNotBeNegativeOrNaN()
		{
			var gesture = Gesture.PressAt(Vector2.zero);

			Assert.Throws<ArgumentOutOfRangeException>(() => gesture.Hold(-1f));
			Assert.Throws<ArgumentOutOfRangeException>(() => gesture.Hold(float.NaN));
			Assert.Throws<ArgumentOutOfRangeException>(() => gesture.MoveTo(Vector2.one, -0.1f));
			Assert.Throws<ArgumentOutOfRangeException>(() => gesture.MoveTo(() => Vector2.one, -0.1f));
			Assert.Throws<ArgumentOutOfRangeException>(() => gesture.Set("pressure", 1f, -1f));
			Assert.Throws<ArgumentOutOfRangeException>(() => gesture.HoldUntil(() => true, -1f));
		}

		[Test]
		public void ConditionsAndFunctionsMustNotBeNull()
		{
			var gesture = Gesture.PressAt(Vector2.zero);

			Assert.Throws<ArgumentNullException>(() => gesture.MoveTo((Func<Vector2>)null, 1f));
			Assert.Throws<ArgumentNullException>(() => gesture.HoldUntil(null, 1f));
		}

		[Test]
		public void AControlPathMustNotBeEmpty()
		{
			Assert.Throws<ArgumentException>(() => Gesture.PressAt(Vector2.zero).Set("", 1f));
			Assert.Throws<ArgumentException>(() => Gesture.PressAt(Vector2.zero).Set(null, 1f));
			Assert.Throws<ArgumentException>(() => new ControlSequence().Press(""));
			Assert.Throws<ArgumentException>(() => new ControlSequence().Set("", 1f));
		}

		[Test]
		public void TheBuildersChain()
		{
			var gesture = Gesture.PressAt(new Vector2(1, 2), "rightButton")
				.Hold(0.1f)
				.MoveTo(new Vector2(3, 4), 0.2f)
				.Press("leftButton")
				.Release("rightButton")
				.Scroll(Vector2.up)
				.Set("pressure", 0.5f, 0.1f)
				.HoldUntil(() => true, 1f);
			var sequence = new ControlSequence()
				.Press(Key.A)
				.Press(GamepadButton.South)
				.Press("space")
				.Hold(0.1f)
				.Release("space")
				.Release(GamepadButton.South)
				.Release(Key.A)
				.Set("leftStick", Vector2.one, 0.1f)
				.Text("a")
				.Composition("b")
				.HoldUntil(() => true, 1f);

			Assert.That(gesture, Is.Not.Null);
			Assert.That(sequence, Is.Not.Null);
		}

		[Test]
		public void TheStepsOfAGestureReadAsTheyWereGiven()
		{
			Func<Vector2> destination = () => Vector2.one;
			Func<bool> condition = () => true;
			var gesture = Gesture.PressAt(new Vector2(1, 2), "rightButton")
				.Hold(0.1f)
				.MoveTo(new Vector2(3, 4), 0.2f)
				.MoveTo(destination, 0.3f)
				.Press("leftButton")
				.Release("rightButton")
				.Scroll(new Vector2(0, 5))
				.Set("pressure", 0.5f, 0.4f)
				.HoldUntil(condition, 1.5f);

			var steps = gesture.Steps;

			Assert.That(steps.Select(s => s.Kind), Is.EqualTo(new[]
			{
				Gesture.StepKind.Press,
				Gesture.StepKind.Hold,
				Gesture.StepKind.Move,
				Gesture.StepKind.Move,
				Gesture.StepKind.Press,
				Gesture.StepKind.Release,
				Gesture.StepKind.Scroll,
				Gesture.StepKind.Set,
				Gesture.StepKind.HoldUntil,
			}));
			Assert.That(steps[0].Position, Is.EqualTo(new Vector2(1, 2)));
			Assert.That(steps[0].Button, Is.EqualTo("rightButton"));
			Assert.That(steps[1].Seconds, Is.EqualTo(0.1f));
			Assert.That(steps[1].Position, Is.Null);
			Assert.That(steps[2].Position, Is.EqualTo(new Vector2(3, 4)));
			Assert.That(steps[2].PositionFunc, Is.Null);
			Assert.That(steps[2].Seconds, Is.EqualTo(0.2f));
			Assert.That(steps[3].Position, Is.Null);
			Assert.That(steps[3].PositionFunc, Is.SameAs(destination));
			Assert.That(steps[3].Seconds, Is.EqualTo(0.3f));
			Assert.That(steps[4].Button, Is.EqualTo("leftButton"));
			Assert.That(steps[5].Button, Is.EqualTo("rightButton"));
			Assert.That(steps[6].Delta, Is.EqualTo(new Vector2(0, 5)));
			Assert.That(steps[7].Path, Is.EqualTo("pressure"));
			Assert.That(steps[7].Value, Is.EqualTo(0.5f));
			Assert.That(steps[7].ValueType, Is.EqualTo(typeof(float)));
			Assert.That(steps[7].Seconds, Is.EqualTo(0.4f));
			Assert.That(steps[8].Condition, Is.SameAs(condition));
			Assert.That(steps[8].Seconds, Is.EqualTo(1.5f), "the timeout");
		}

		[Test]
		public void AHoverStartsAGestureWithAPositionAndNoButton()
		{
			var steps = Gesture.HoverAt(new Vector2(5, 6)).Steps;

			Assert.That(steps, Has.Count.EqualTo(1));
			Assert.That(steps[0].Kind, Is.EqualTo(Gesture.StepKind.Hover));
			Assert.That(steps[0].Position, Is.EqualTo(new Vector2(5, 6)));
			Assert.That(steps[0].Button, Is.Null);
		}

		[Test]
		public void ThePrimaryButtonOfAStepIsNull()
		{
			var steps = Gesture.PressAt(Vector2.zero).Release().Press().Steps;

			Assert.That(steps.Select(s => s.Button), Is.All.Null);
		}

		[Test]
		public void TheStepsOfAGestureAreAReadOnlyViewThatFollowsTheGesture()
		{
			var gesture = Gesture.PressAt(Vector2.zero);
			var steps = gesture.Steps;
			Assert.That(steps, Has.Count.EqualTo(1));

			gesture.Hold(0.1f);

			Assert.That(steps, Has.Count.EqualTo(2), "the same view");
			Assert.That(gesture.Steps, Is.SameAs(steps));
			Assert.That(steps, Is.Not.InstanceOf<System.Collections.Generic.List<Gesture.Step>>());
			Assert.That(steps, Is.Not.InstanceOf<Gesture.Step[]>());
		}

		[Test]
		public void TheStepsOfASequenceReadAsTheyWereGiven()
		{
			Func<bool> condition = () => true;
			var sequence = new ControlSequence()
				.Press(Key.A)
				.Press(GamepadButton.South)
				.Press("space")
				.Hold(0.1f)
				.Release("space")
				.Release(GamepadButton.South)
				.Release(Key.A)
				.Set("leftStick", Vector2.one, 0.2f)
				.Text("a")
				.Composition("b")
				.HoldUntil(condition, 1.5f);

			var steps = sequence.Steps;

			Assert.That(steps.Select(s => s.Kind), Is.EqualTo(new[]
			{
				ControlSequence.StepKind.Press,
				ControlSequence.StepKind.Press,
				ControlSequence.StepKind.Press,
				ControlSequence.StepKind.Hold,
				ControlSequence.StepKind.Release,
				ControlSequence.StepKind.Release,
				ControlSequence.StepKind.Release,
				ControlSequence.StepKind.Set,
				ControlSequence.StepKind.Text,
				ControlSequence.StepKind.Composition,
				ControlSequence.StepKind.HoldUntil,
			}));
			Assert.That(steps[0].Control, Is.EqualTo(Key.A));
			Assert.That(steps[1].Control, Is.EqualTo(GamepadButton.South));
			Assert.That(steps[2].Control, Is.EqualTo("space"));
			Assert.That(steps[3].Seconds, Is.EqualTo(0.1f));
			Assert.That(steps[3].Control, Is.Null);
			Assert.That(steps[4].Control, Is.EqualTo("space"));
			Assert.That(steps[5].Control, Is.EqualTo(GamepadButton.South));
			Assert.That(steps[6].Control, Is.EqualTo(Key.A));
			Assert.That(steps[7].Control, Is.EqualTo("leftStick"));
			Assert.That(steps[7].Value, Is.EqualTo(Vector2.one));
			Assert.That(steps[7].ValueType, Is.EqualTo(typeof(Vector2)));
			Assert.That(steps[7].Seconds, Is.EqualTo(0.2f));
			Assert.That(steps[8].Text, Is.EqualTo("a"));
			Assert.That(steps[9].Text, Is.EqualTo("b"));
			Assert.That(steps[10].Condition, Is.SameAs(condition));
			Assert.That(steps[10].Seconds, Is.EqualTo(1.5f), "the timeout");
		}

		[Test]
		public void TheStepsOfASequenceAreAReadOnlyViewThatFollowsTheSequence()
		{
			var sequence = new ControlSequence();
			var steps = sequence.Steps;
			Assert.That(steps, Is.Empty);

			sequence.Press(Key.A);

			Assert.That(steps, Has.Count.EqualTo(1));
			Assert.That(sequence.Steps, Is.SameAs(steps));
			Assert.That(steps, Is.Not.InstanceOf<System.Collections.Generic.List<ControlSequence.Step>>());
		}

		[Test]
		public void TheStepsOfTheGestureAndTheSequenceOfAnItemAreThoseThatWereGiven()
		{
			var gesture = Gesture.PressAt(Vector2.zero).Hold(0.1f);
			var sequence = new ControlSequence().Press(Key.A);
			var item = new AutoTapBase.LogItem { Gesture = gesture, Sequence = sequence };

			Assert.That(item.Gesture.Steps, Has.Count.EqualTo(2));
			Assert.That(item.Sequence.Steps, Has.Count.EqualTo(1));
		}

		[Test]
		public void TextMustNotBeNull()
		{
			Assert.Throws<ArgumentNullException>(() => new ControlSequence().Text(null));
			Assert.Throws<ArgumentNullException>(() => new ControlSequence().Composition(null));
		}
	}
}
#endif
