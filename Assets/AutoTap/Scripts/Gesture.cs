#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityAutoTap
{
	/// <summary>
	/// The plan of one pointer: a finger of a touchscreen, a mouse, a pen, or any other pointer device. Passed to
	/// <see cref="AutoTapBase.Simulate(int, Gesture)"/>, <see cref="AutoTapBase.Simulate{TDevice}(Gesture, int)"/> or
	/// <see cref="AutoTapBase.Simulate(string, Gesture, int)"/>.
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
	/// What differs is which controls exist, and that is checked when the gesture is simulated: a gesture that does not
	/// fit the pointer it is simulated on makes <c>Simulate</c> throw <see cref="ArgumentException"/>. In particular
	/// <see cref="HoverAt"/>, <see cref="Scroll"/> and buttons other than the primary one cannot be used with a
	/// finger.
	/// </para>
	/// <para>
	/// <c>Simulate</c> carries out the first step and every step after it that takes no time, up to the first step
	/// that takes time (a move or a change that takes seconds, a hold that has to wait). Each of them is an event of
	/// its own, queued in the order of the plan, so one input update processes them all. The other steps are carried out in the following
	/// frames, once the first step was processed. A button is never pressed and released by the same input update: a
	/// release that follows a press with no time in between is made in the next frame. A pen is in range from the first
	/// step until the end of the gesture.
	/// </para>
	/// <para>
	/// A gesture must not be modified after it was passed to a <c>Simulate</c> method.
	/// </para>
	/// </remarks>
	public sealed class Gesture
	{
		/// <summary>
		/// What a <see cref="Step"/> of a gesture does.
		/// </summary>
		public enum StepKind
		{
			/// <summary>
			/// Presses a button (<see cref="Gesture.PressAt"/>, <see cref="Gesture.Press"/>).
			/// </summary>
			Press,

			/// <summary>
			/// Releases a button (<see cref="Gesture.Release"/>).
			/// </summary>
			Release,

			/// <summary>
			/// Moves the pointer to a position without pressing anything (<see cref="Gesture.HoverAt"/>).
			/// </summary>
			Hover,

			/// <summary>
			/// Moves the pointer in a straight line (<see cref="Gesture.MoveTo(Vector2, float)"/>,
			/// <see cref="Gesture.MoveTo(Func{Vector2}, float)"/>).
			/// </summary>
			Move,

			/// <summary>
			/// Scrolls once (<see cref="Gesture.Scroll"/>).
			/// </summary>
			Scroll,

			/// <summary>
			/// Changes a control gradually (<see cref="Gesture.Set{T}"/>).
			/// </summary>
			Set,

			/// <summary>
			/// Keeps everything as it is for a time (<see cref="Gesture.Hold"/>).
			/// </summary>
			Hold,

			/// <summary>
			/// Keeps everything as it is until a condition is met (<see cref="Gesture.HoldUntil"/>).
			/// </summary>
			HoldUntil,
		}

		/// <summary>
		/// One step of the plan of a <see cref="Gesture"/>, as it was given. Which values a step has depends on its
		/// <see cref="Kind"/>; the others are <c>null</c>, zero or empty.
		/// </summary>
		public readonly struct Step
		{
			internal Step(
				StepKind kind, Vector2? position = null, Func<Vector2> positionFunc = null, string button = null,
				Vector2 delta = default, string path = null, object value = null, Type valueType = null,
				float seconds = 0f, Func<bool> condition = null)
			{
				Kind = kind;
				Position = position;
				PositionFunc = positionFunc;
				Button = button;
				Delta = delta;
				Path = path;
				Value = value;
				ValueType = valueType;
				Seconds = seconds;
				Condition = condition;
			}

			/// <summary>
			/// What the step does.
			/// </summary>
			public StepKind Kind { get; }

			/// <summary>
			/// The position in screen pixels, for the first step (<see cref="StepKind.Press"/> of <see cref="PressAt"/>,
			/// <see cref="StepKind.Hover"/>) and for a <see cref="StepKind.Move"/> whose destination was given as a
			/// value. <c>null</c> otherwise, and for a move whose destination is <see cref="PositionFunc"/>.
			/// </summary>
			public Vector2? Position { get; }

			/// <summary>
			/// The function that returns the destination of a <see cref="StepKind.Move"/>, which is called when the
			/// move starts. <c>null</c> for the other steps, and for a move whose destination is <see cref="Position"/>.
			/// </summary>
			public Func<Vector2> PositionFunc { get; }

			/// <summary>
			/// The button of a <see cref="StepKind.Press"/> or <see cref="StepKind.Release"/>, as it was given: the
			/// path of a button control relative to the device. <c>null</c> selects the primary button, and is also what
			/// the other steps have.
			/// </summary>
			public string Button { get; }

			/// <summary>
			/// The amount of a <see cref="StepKind.Scroll"/>. Zero for the other steps.
			/// </summary>
			public Vector2 Delta { get; }

			/// <summary>
			/// The path of the control that a <see cref="StepKind.Set"/> changes. <c>null</c> for the other steps.
			/// </summary>
			public string Path { get; }

			/// <summary>
			/// The value that a <see cref="StepKind.Set"/> has at the end of the step, boxed as <see cref="ValueType"/>.
			/// <c>null</c> for the other steps.
			/// </summary>
			public object Value { get; }

			/// <summary>
			/// The type argument of a <see cref="StepKind.Set"/>. <c>null</c> for the other steps.
			/// </summary>
			public Type ValueType { get; }

			/// <summary>
			/// The time that a <see cref="StepKind.Move"/>, a <see cref="StepKind.Set"/> or a <see cref="StepKind.Hold"/>
			/// takes, in seconds, and the timeout of a <see cref="StepKind.HoldUntil"/>. Zero for the other steps.
			/// </summary>
			public float Seconds { get; }

			/// <summary>
			/// The condition of a <see cref="StepKind.HoldUntil"/>. <c>null</c> for the other steps.
			/// </summary>
			public Func<bool> Condition { get; }
		}

		readonly List<Step> _steps = new List<Step>();
		readonly IReadOnlyList<Step> _readOnlySteps;

		Gesture()
		{
			_readOnlySteps = _steps.AsReadOnly();
		}

		/// <summary>
		/// The plan, step by step, in the order the steps run. The first step is the one that
		/// <see cref="PressAt"/> or <see cref="HoverAt"/> made.
		/// </summary>
		/// <remarks>
		/// <para>
		/// It is a read-only view of the gesture: steps that are added to the gesture later show up in it.
		/// </para>
		/// </remarks>
		public IReadOnlyList<Step> Steps => _readOnlySteps;

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
			gesture._steps.Add(new Step(StepKind.Press, position: position, button: button));
			return gesture;
		}

		/// <summary>
		/// Starts a gesture by moving the pointer to <paramref name="position"/> without pressing anything. The UI
		/// there is hovered. It cannot be used with a finger.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		public static Gesture HoverAt(Vector2 position)
		{
			var gesture = new Gesture();
			gesture._steps.Add(new Step(StepKind.Hover, position: position));
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
			_steps.Add(new Step(StepKind.Move, position: position, seconds: seconds));
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
			_steps.Add(new Step(StepKind.Move, positionFunc: position, seconds: seconds));
			return this;
		}

		/// <summary>
		/// Presses a button, in addition to what is already pressed.
		/// </summary>
		/// <param name="button">The button to press. See <see cref="PressAt"/>.</param>
		public Gesture Press(string button = null)
		{
			_steps.Add(new Step(StepKind.Press, button: button));
			return this;
		}

		/// <summary>
		/// Releases a button.
		/// </summary>
		/// <param name="button">The button to release. See <see cref="PressAt"/>.</param>
		public Gesture Release(string button = null)
		{
			_steps.Add(new Step(StepKind.Release, button: button));
			return this;
		}

		/// <summary>
		/// Scrolls once, at the current position. It cannot be used with a finger.
		/// </summary>
		/// <param name="delta">The amount, in the unit of the <c>scroll</c> control of the device.</param>
		public Gesture Scroll(Vector2 delta)
		{
			_steps.Add(new Step(StepKind.Scroll, delta: delta));
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
			_steps.Add(new Step(StepKind.Set, path: controlPath, value: value, valueType: typeof(T), seconds: seconds));
			return this;
		}

		/// <summary>
		/// Keeps everything as it is for <paramref name="seconds"/>.
		/// </summary>
		/// <param name="seconds">The time to wait.</param>
		public Gesture Hold(float seconds)
		{
			RequireSeconds(seconds);
			_steps.Add(new Step(StepKind.Hold, seconds: seconds));
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
			_steps.Add(new Step(StepKind.HoldUntil, seconds: timeoutSeconds, condition: condition));
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
			if (Steps.Count == 0 || Steps[0].Kind != StepKind.Press || !Steps[0].Position.HasValue)
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

			var points = new List<Vector2> { Steps[0].Position.Value };
			for (var i = 1; i < count; ++i)
			{
				var step = Steps[i];
				switch (step.Kind)
				{
					case StepKind.Move:
						if (step.Position.HasValue)
						{
							points.Add(step.Position.Value);
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
							$"A finger cannot do '{step.Kind}'. Hovering, scrolling and buttons need a pointer such as a mouse or a pen.",
							nameof(Steps));
				}
			}

			return new FingerPlan(Steps[0].Position.Value, points, count);
		}

		/// <summary>
		/// Checks that the gesture can be carried out by a pointer device, as far as that can be told without the
		/// device, and summarizes it.
		/// </summary>
		/// <exception cref="ArgumentException">The gesture does not fit a pointer.</exception>
		internal PointerPlan PlanForPointer()
		{
			if (Steps.Count == 0
				|| !Steps[0].Position.HasValue
				|| (Steps[0].Kind != StepKind.Press && Steps[0].Kind != StepKind.Hover))
			{
				throw new ArgumentException("A gesture for a pointer has to start with PressAt or HoverAt.", nameof(Steps));
			}

			var points = new List<Vector2> { Steps[0].Position.Value };
			for (var i = 1; i < Steps.Count; ++i)
			{
				var step = Steps[i];
				if (step.Kind == StepKind.Move && step.Position.HasValue)
				{
					points.Add(step.Position.Value);
				}
			}

			return new PointerPlan(
				Steps[0].Position.Value, Steps[0].Kind == StepKind.Press, Steps[0].Button, points, Steps.Count);
		}
	}

	/// <summary>
	/// What <see cref="Gesture.PlanForFinger"/> found out about a gesture.
	/// </summary>
	internal sealed class FingerPlan
	{
		public FingerPlan(Vector2 start, List<Vector2> points, int stepCount)
		{
			Start = start;
			Points = points;
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
			Vector2 start, bool startsWithPress, string firstButton, List<Vector2> points, int stepCount)
		{
			Start = start;
			StartsWithPress = startsWithPress;
			FirstButton = firstButton;
			Points = points;
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
		/// The number of steps to run, which is every step.
		/// </summary>
		public int StepCount { get; }
	}
}
#endif
