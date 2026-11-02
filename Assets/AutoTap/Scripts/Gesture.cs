#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityAutoTap
{
	/// <summary>
	/// The plan of one pointer: a finger of a touchscreen, a mouse, a pen, or any other pointer device. Passed to
	/// <see cref="AutoTapBase.Fire(int, Gesture)"/> or <see cref="AutoTapBase.FirePointer"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A gesture is built from steps, which run one after another. Every step takes time that is counted in seconds
	/// of the <c>deltaTime</c> passed to <see cref="AutoTapBase.Update(float)"/>. The gesture ends after its last
	/// step; at the end whatever is still pressed is released, and then the controls that are not marked
	/// <c>dontReset</c> are returned to their default values. The position of a mouse or a pen stays where it is.
	/// </para>
	/// <para>
	/// The same plan is used for fingers, mice and pens because the input module handles the three in the same way.
	/// What differs is which controls exist, and that is checked when the gesture is fired: a gesture that does not
	/// fit the pointer it is fired on makes <c>Fire</c> throw <see cref="ArgumentException"/>. In particular
	/// <see cref="HoverAt"/>, <see cref="Scroll"/> and buttons other than the primary one cannot be used with a
	/// finger.
	/// </para>
	/// <para>
	/// <c>Fire</c> carries out the first step and every step after it that takes no time, up to the first step that
	/// takes time (a move or a change that takes seconds, a hold that has to wait). Each of them is an event of its own, queued in the
	/// order of the plan, so one input update processes them all. The other steps are carried out in the following
	/// frames, once the first step was processed. A button is never pressed and released by the same input update: a
	/// release that follows a press with no time in between is made in the next frame. A pen is in range from the first
	/// step until the end of the gesture.
	/// </para>
	/// <para>
	/// A gesture must not be modified after it was passed to a <c>Fire</c> method.
	/// </para>
	/// </remarks>
	public sealed class Gesture
	{
		internal enum StepKind
		{
			Press,
			Release,
			Hover,
			Move,
			Scroll,
			Set,
			Hold,
			HoldUntil,
		}

		internal sealed class Step
		{
			public StepKind Kind;
			public bool HasPosition;
			public Vector2 Position;
			public Func<Vector2> PositionFunc;
			public string Button;
			public Vector2 Delta;
			public string Path;
			public object Value;
			public Type ValueType;
			public float Seconds;
			public Func<bool> Condition;
		}

		internal readonly List<Step> Steps = new List<Step>();

		Gesture()
		{
		}

		/// <summary>
		/// Starts a gesture with a press.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		/// <param name="button">
		/// The button to press, as the path of a button control relative to the device (for example
		/// <c>rightButton</c> of a mouse). <c>null</c> selects the control whose usage is
		/// <see cref="UnityEngine.InputSystem.CommonUsages.PrimaryAction"/>: the touch of a finger, the left button
		/// of a mouse, the tip of a pen.
		/// </param>
		public static Gesture PressAt(Vector2 position, string button = null)
		{
			var gesture = new Gesture();
			gesture.Steps.Add(new Step { Kind = StepKind.Press, HasPosition = true, Position = position, Button = button });
			return gesture;
		}

		/// <summary>
		/// Starts a gesture by moving the pointer to <paramref name="position"/> without pressing anything. The UI
		/// there is hovered. It cannot be fired on a finger.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		public static Gesture HoverAt(Vector2 position)
		{
			var gesture = new Gesture();
			gesture.Steps.Add(new Step { Kind = StepKind.Hover, HasPosition = true, Position = position });
			return gesture;
		}

		/// <summary>
		/// Moves the pointer in a straight line to <paramref name="position"/>, keeping whatever is pressed.
		/// </summary>
		/// <param name="position">The destination in screen pixels.</param>
		/// <param name="seconds">The time the move takes. 0 moves at once.</param>
		public Gesture MoveTo(Vector2 position, float seconds)
		{
			RequireSeconds(seconds);
			Steps.Add(new Step { Kind = StepKind.Move, HasPosition = true, Position = position, Seconds = seconds });
			return this;
		}

		/// <summary>
		/// Moves the pointer in a straight line to the position returned by <paramref name="position"/>, which is
		/// evaluated when the move starts.
		/// </summary>
		/// <param name="position">Returns the destination in screen pixels.</param>
		/// <param name="seconds">The time the move takes. 0 moves at once.</param>
		public Gesture MoveTo(Func<Vector2> position, float seconds)
		{
			if (position == null)
			{
				throw new ArgumentNullException(nameof(position));
			}

			RequireSeconds(seconds);
			Steps.Add(new Step { Kind = StepKind.Move, PositionFunc = position, Seconds = seconds });
			return this;
		}

		/// <summary>
		/// Presses a button, in addition to what is already pressed.
		/// </summary>
		/// <param name="button">The button to press. See <see cref="PressAt"/>.</param>
		public Gesture Press(string button = null)
		{
			Steps.Add(new Step { Kind = StepKind.Press, Button = button });
			return this;
		}

		/// <summary>
		/// Releases a button.
		/// </summary>
		/// <param name="button">The button to release. See <see cref="PressAt"/>.</param>
		public Gesture Release(string button = null)
		{
			Steps.Add(new Step { Kind = StepKind.Release, Button = button });
			return this;
		}

		/// <summary>
		/// Scrolls once, at the current position. It cannot be fired on a finger.
		/// </summary>
		/// <param name="delta">The amount, in the unit of the <c>scroll</c> control of the device.</param>
		public Gesture Scroll(Vector2 delta)
		{
			Steps.Add(new Step { Kind = StepKind.Scroll, Delta = delta });
			return this;
		}

		/// <summary>
		/// Changes a control gradually, for example <c>pressure</c> of a finger or a pen, or <c>tilt</c> of a pen.
		/// </summary>
		/// <remarks>
		/// The value changes linearly (<see cref="Quaternion"/> values spherically). To change it along a curve,
		/// chain several steps.
		/// </remarks>
		/// <typeparam name="T">The value type of the control.</typeparam>
		/// <param name="controlPath">
		/// The path of the control. For a finger it is relative to the touch (for example <c>pressure</c>); for other
		/// pointers it is relative to the device.
		/// </param>
		/// <param name="value">The value at the end of the step.</param>
		/// <param name="seconds">The time the change takes. 0 sets the value at once.</param>
		public Gesture Set<T>(string controlPath, T value, float seconds = 0) where T : struct
		{
			if (string.IsNullOrEmpty(controlPath))
			{
				throw new ArgumentException("The control path is empty.", nameof(controlPath));
			}

			RequireSeconds(seconds);
			Steps.Add(new Step
			{
				Kind = StepKind.Set,
				Path = controlPath,
				Value = value,
				ValueType = typeof(T),
				Seconds = seconds,
			});
			return this;
		}

		/// <summary>
		/// Keeps everything as it is for <paramref name="seconds"/>.
		/// </summary>
		/// <param name="seconds">The time to wait.</param>
		public Gesture Hold(float seconds)
		{
			RequireSeconds(seconds);
			Steps.Add(new Step { Kind = StepKind.Hold, Seconds = seconds });
			return this;
		}

		/// <summary>
		/// Keeps everything as it is until <paramref name="condition"/> returns <c>true</c>. If that does not
		/// happen within <paramref name="timeoutSeconds"/>, the gesture releases what is pressed and ends with
		/// <see cref="AutoTapBase.EndReason.TimedOut"/>.
		/// </summary>
		/// <param name="condition">Evaluated once per <see cref="AutoTapBase.Update(float)"/>.</param>
		/// <param name="timeoutSeconds">The longest time to wait.</param>
		public Gesture HoldUntil(Func<bool> condition, float timeoutSeconds)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			RequireSeconds(timeoutSeconds);
			Steps.Add(new Step { Kind = StepKind.HoldUntil, Condition = condition, Seconds = timeoutSeconds });
			return this;
		}

		static void RequireSeconds(float seconds)
		{
			if (float.IsNaN(seconds) || seconds < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(seconds), "The time must not be negative.");
			}
		}

		/// <summary>
		/// Checks that the gesture can be carried out by a finger, and summarizes it.
		/// </summary>
		/// <exception cref="ArgumentException">The gesture does not fit a finger.</exception>
		internal FingerPlan PlanForFinger()
		{
			if (Steps.Count == 0 || Steps[0].Kind != StepKind.Press || !Steps[0].HasPosition)
			{
				throw new ArgumentException("A gesture for a finger has to start with PressAt.", nameof(Steps));
			}

			if (Steps[0].Button != null)
			{
				throw new ArgumentException(
					"A finger has only the touch, so PressAt takes no button.", nameof(Steps));
			}

			var count = Steps.Count;
			if (count > 1 && Steps[count - 1].Kind == StepKind.Release && Steps[count - 1].Button == null)
			{
				--count;
			}

			var points = new List<Vector2> { Steps[0].Position };
			Vector2? lastMove = null;
			for (var i = 1; i < count; ++i)
			{
				var step = Steps[i];
				switch (step.Kind)
				{
					case StepKind.Move:
						if (step.PositionFunc == null)
						{
							points.Add(step.Position);
							lastMove = step.Position;
						}

						break;
					case StepKind.Set:
						VirtualFinger.ValidateControl(step.Path, step.ValueType);
						break;
					case StepKind.Hold:
					case StepKind.HoldUntil:
						break;
					default:
						throw new ArgumentException(
							$"A finger cannot do '{step.Kind}'. Use FirePointer for hovering, scrolling and buttons.",
							nameof(Steps));
				}
			}

			return new FingerPlan(Steps[0].Position, points, lastMove, count);
		}

		/// <summary>
		/// Checks that the gesture can be carried out by a pointer device, as far as that can be told without the
		/// device, and summarizes it.
		/// </summary>
		/// <exception cref="ArgumentException">The gesture does not fit a pointer.</exception>
		internal PointerPlan PlanForPointer()
		{
			if (Steps.Count == 0
				|| !Steps[0].HasPosition
				|| (Steps[0].Kind != StepKind.Press && Steps[0].Kind != StepKind.Hover))
			{
				throw new ArgumentException("A gesture for a pointer has to start with PressAt or HoverAt.", nameof(Steps));
			}

			var points = new List<Vector2> { Steps[0].Position };
			Vector2? lastMove = null;
			for (var i = 1; i < Steps.Count; ++i)
			{
				var step = Steps[i];
				if (step.Kind == StepKind.Move && step.PositionFunc == null)
				{
					points.Add(step.Position);
					lastMove = step.Position;
				}
			}

			return new PointerPlan(
				Steps[0].Position, Steps[0].Kind == StepKind.Press, Steps[0].Button, points, lastMove, Steps.Count);
		}
	}

	/// <summary>
	/// What <see cref="Gesture.PlanForFinger"/> found out about a gesture.
	/// </summary>
	internal sealed class FingerPlan
	{
		public FingerPlan(Vector2 start, List<Vector2> points, Vector2? lastMove, int stepCount)
		{
			Start = start;
			Points = points;
			LastMove = lastMove;
			StepCount = stepCount;
		}

		/// <summary>
		/// The position of the press.
		/// </summary>
		public Vector2 Start { get; }

		/// <summary>
		/// The press position and every destination that is known before the gesture runs.
		/// </summary>
		public List<Vector2> Points { get; }

		/// <summary>
		/// The destination of the last move whose destination is known before the gesture runs.
		/// </summary>
		public Vector2? LastMove { get; }

		/// <summary>
		/// The number of steps to run: all steps except a release at the very end, which the end of the gesture does
		/// anyway.
		/// </summary>
		public int StepCount { get; }
	}

	/// <summary>
	/// What <see cref="Gesture.PlanForPointer"/> found out about a gesture.
	/// </summary>
	internal sealed class PointerPlan
	{
		public PointerPlan(
			Vector2 start, bool startsWithPress, string firstButton, List<Vector2> points, Vector2? lastMove,
			int stepCount)
		{
			Start = start;
			StartsWithPress = startsWithPress;
			FirstButton = firstButton;
			Points = points;
			LastMove = lastMove;
			StepCount = stepCount;
		}

		/// <summary>
		/// The position of the first step.
		/// </summary>
		public Vector2 Start { get; }

		/// <summary>
		/// <c>true</c> if the first step presses a button, <c>false</c> if it only hovers.
		/// </summary>
		public bool StartsWithPress { get; }

		/// <summary>
		/// The button that the first step presses, as given to the gesture. <c>null</c> for the primary button.
		/// </summary>
		public string FirstButton { get; }

		/// <summary>
		/// The position of the first step and every destination that is known before the gesture runs.
		/// </summary>
		public List<Vector2> Points { get; }

		/// <summary>
		/// The destination of the last move whose destination is known before the gesture runs.
		/// </summary>
		public Vector2? LastMove { get; }

		/// <summary>
		/// The number of steps to run, which is every step.
		/// </summary>
		public int StepCount { get; }
	}
}
#endif
