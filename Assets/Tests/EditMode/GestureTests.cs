#if ENABLE_AUTOTAP
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// What <see cref="Gesture"/> and <see cref="ControlSequence"/> accept when they are built.
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
		public void TextMustNotBeNull()
		{
			Assert.Throws<ArgumentNullException>(() => new ControlSequence().Text(null));
			Assert.Throws<ArgumentNullException>(() => new ControlSequence().Composition(null));
		}
	}
}
#endif
