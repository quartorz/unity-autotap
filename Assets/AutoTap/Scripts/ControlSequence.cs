#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap
{
	/// <summary>
	/// The plan of one device that has no position: a <see cref="Keyboard"/>, a <see cref="Gamepad"/>, a joystick, a
	/// sensor or any custom layout. Passed to <see cref="AutoTapBase.Simulate{TDevice}(ControlSequence, int)"/> or
	/// <see cref="AutoTapBase.Simulate(string, ControlSequence, int)"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A sequence is built from steps, which run one after another. Every step takes time that is counted in seconds
	/// of the <c>deltaTime</c> passed to <see cref="AutoTapBase.Update(float)"/>. The sequence ends after its last
	/// step; at the end whatever is still pressed is released, and then the controls that are not marked
	/// <c>dontReset</c> are returned to their default values.
	/// </para>
	/// <para>
	/// Controls are named by their path relative to the device (for example <c>leftStick</c>, <c>buttonSouth</c> or
	/// <c>space</c>). A path that names no control of the device makes <c>Simulate</c> throw
	/// <see cref="ArgumentException"/>, and so does a type or a layout derived from <see cref="Pointer"/>, which is
	/// operated with a <see cref="Gesture"/>.
	/// </para>
	/// <para>
	/// Every step that writes is an event of its own, even when several steps fall into one frame, so the order of the
	/// steps is the order in which the Input System processes them (which a shortcut such as Ctrl+R depends on). The
	/// steps at the start that take no time fall into the frame of <c>Simulate</c>, which queues them. A control is never
	/// pressed and released by the same input update: a release that follows a press with no time in between is made
	/// in the next frame.
	/// </para>
	/// <para>
	/// A sequence must not be modified after it was passed to a <c>Simulate</c> method.
	/// </para>
	/// </remarks>
	public sealed class ControlSequence
	{
		/// <summary>
		/// What a <see cref="Step"/> of a sequence does.
		/// </summary>
		public enum StepKind
		{
			/// <summary>
			/// Presses a button-like control: writes 1 to it (<see cref="ControlSequence.Press(string)"/>,
			/// <see cref="ControlSequence.Press(Key)"/>, <see cref="ControlSequence.Press(GamepadButton)"/>).
			/// </summary>
			Press,

			/// <summary>
			/// Releases a button-like control: writes 0 to it (<see cref="ControlSequence.Release(string)"/>,
			/// <see cref="ControlSequence.Release(Key)"/>, <see cref="ControlSequence.Release(GamepadButton)"/>).
			/// </summary>
			Release,

			/// <summary>
			/// Changes a control gradually (<see cref="ControlSequence.Set{T}"/>).
			/// </summary>
			Set,

			/// <summary>
			/// Enters text (<see cref="ControlSequence.Text"/>).
			/// </summary>
			Text,

			/// <summary>
			/// Sets the text being composed by an input method editor (<see cref="ControlSequence.Composition"/>).
			/// </summary>
			Composition,

			/// <summary>
			/// Keeps everything as it is for a time (<see cref="ControlSequence.Hold"/>).
			/// </summary>
			Hold,

			/// <summary>
			/// Keeps everything as it is until a condition is met (<see cref="ControlSequence.HoldUntil"/>).
			/// </summary>
			HoldUntil,
		}

		/// <summary>
		/// One step of the plan of a <see cref="ControlSequence"/>, as it was given. Which values a step has depends on
		/// its <see cref="Kind"/>; the others are <c>null</c>, zero or empty.
		/// </summary>
		public readonly struct Step
		{
			internal Step(
				StepKind kind, object control = null, object value = null, Type valueType = null, string text = null,
				float seconds = 0f, Func<bool> condition = null)
			{
				Kind = kind;
				Control = control;
				Value = value;
				ValueType = valueType;
				Text = text;
				Seconds = seconds;
				Condition = condition;
			}

			/// <summary>
			/// What the step does.
			/// </summary>
			public StepKind Kind { get; }

			/// <summary>
			/// The control of a <see cref="StepKind.Press"/>, <see cref="StepKind.Release"/> or
			/// <see cref="StepKind.Set"/>: a path relative to the device (a <see cref="string"/>), or, for a press or a
			/// release, a <see cref="Key"/> or a <see cref="GamepadButton"/>. <c>null</c> for the other steps.
			/// </summary>
			public object Control { get; }

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
			/// The characters of a <see cref="StepKind.Text"/> or the text of a <see cref="StepKind.Composition"/>.
			/// <c>null</c> for the other steps.
			/// </summary>
			public string Text { get; }

			/// <summary>
			/// The time that a <see cref="StepKind.Set"/> or a <see cref="StepKind.Hold"/> takes, in seconds, and the
			/// timeout of a <see cref="StepKind.HoldUntil"/>. Zero for the other steps.
			/// </summary>
			public float Seconds { get; }

			/// <summary>
			/// The condition of a <see cref="StepKind.HoldUntil"/>. <c>null</c> for the other steps.
			/// </summary>
			public Func<bool> Condition { get; }
		}

		readonly List<Step> _steps = new List<Step>();
		readonly IReadOnlyList<Step> _readOnlySteps;

		/// <summary>
		/// Creates an empty sequence.
		/// </summary>
		public ControlSequence()
		{
			_readOnlySteps = _steps.AsReadOnly();
		}

		/// <summary>
		/// The plan, step by step, in the order the steps run.
		/// </summary>
		/// <remarks>
		/// <para>
		/// It is a read-only view of the sequence: steps that are added to the sequence later show up in it. The index
		/// of a step is what <see cref="AutoTapBase.LogItem.CurrentStep"/> refers to.
		/// </para>
		/// </remarks>
		public IReadOnlyList<Step> Steps => _readOnlySteps;

		/// <summary>
		/// Checks that the sequence writes something, which a sequence of waiting steps only does not.
		/// </summary>
		/// <exception cref="ArgumentException">The sequence has no step that writes.</exception>
		internal void RequireWrites()
		{
			for (var i = 0; i < _steps.Count; ++i)
			{
				switch (_steps[i].Kind)
				{
					case StepKind.Press:
					case StepKind.Release:
					case StepKind.Set:
					case StepKind.Text:
					case StepKind.Composition:
						return;
				}
			}

			throw new ArgumentException(
				"The sequence writes nothing: it needs a Press, Release, Set, Text or Composition step.", nameof(Steps));
		}

		static void RequireSeconds(float seconds)
		{
			if (float.IsNaN(seconds) || seconds < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(seconds), "The time must not be negative.");
			}
		}

		static string RequirePath(string controlPath)
		{
			if (string.IsNullOrEmpty(controlPath))
			{
				throw new ArgumentException("The control path is empty.", nameof(controlPath));
			}

			return controlPath;
		}

		/// <summary>
		/// Presses a button-like control: writes 1 to it.
		/// </summary>
		/// <param name="controlPath">The path of the control relative to the device.</param>
		public ControlSequence Press(string controlPath)
		{
			_steps.Add(new Step(StepKind.Press, control: RequirePath(controlPath)));
			return this;
		}

		/// <summary>
		/// Releases a button-like control: writes 0 to it.
		/// </summary>
		/// <param name="controlPath">The path of the control relative to the device.</param>
		public ControlSequence Release(string controlPath)
		{
			_steps.Add(new Step(StepKind.Release, control: RequirePath(controlPath)));
			return this;
		}

		/// <summary>
		/// Presses a key of a keyboard. No character is entered; use <see cref="Text"/> for that.
		/// </summary>
		/// <param name="key">The key to press.</param>
		public ControlSequence Press(Key key)
		{
			_steps.Add(new Step(StepKind.Press, control: key));
			return this;
		}

		/// <summary>
		/// Releases a key of a keyboard.
		/// </summary>
		/// <param name="key">The key to release.</param>
		public ControlSequence Release(Key key)
		{
			_steps.Add(new Step(StepKind.Release, control: key));
			return this;
		}

		/// <summary>
		/// Presses a button of a gamepad.
		/// </summary>
		/// <param name="button">The button to press.</param>
		public ControlSequence Press(GamepadButton button)
		{
			_steps.Add(new Step(StepKind.Press, control: button));
			return this;
		}

		/// <summary>
		/// Releases a button of a gamepad.
		/// </summary>
		/// <param name="button">The button to release.</param>
		public ControlSequence Release(GamepadButton button)
		{
			_steps.Add(new Step(StepKind.Release, control: button));
			return this;
		}

		/// <summary>
		/// Changes a control gradually, for example a stick or a trigger.
		/// </summary>
		/// <remarks>
		/// The value changes linearly (<see cref="UnityEngine.Quaternion"/> values spherically). To change it along a
		/// curve, chain several steps.
		/// </remarks>
		/// <typeparam name="T">The value type of the control, for example <see cref="float"/>.</typeparam>
		/// <param name="controlPath">The path of the control relative to the device.</param>
		/// <param name="value">The value at the end of the step.</param>
		/// <param name="seconds">The time the change takes. 0 sets the value at once.</param>
		public ControlSequence Set<T>(string controlPath, T value, float seconds = 0) where T : struct
		{
			RequireSeconds(seconds);
			_steps.Add(new Step(
				StepKind.Set, control: RequirePath(controlPath), value: value, valueType: typeof(T), seconds: seconds));
			return this;
		}

		/// <summary>
		/// Enters text. Only devices that receive text support this, which at present is a <see cref="Keyboard"/>.
		/// </summary>
		/// <param name="text">The characters to enter.</param>
		public ControlSequence Text(string text)
		{
			if (text == null)
			{
				throw new ArgumentNullException(nameof(text));
			}

			_steps.Add(new Step(StepKind.Text, text: text));
			return this;
		}

		/// <summary>
		/// Sets the text being composed by an input method editor. An empty string ends the composition. Only devices
		/// that receive text support this, which at present is a <see cref="Keyboard"/>.
		/// </summary>
		/// <param name="text">The text being composed.</param>
		public ControlSequence Composition(string text)
		{
			if (text == null)
			{
				throw new ArgumentNullException(nameof(text));
			}

			_steps.Add(new Step(StepKind.Composition, text: text));
			return this;
		}

		/// <summary>
		/// Keeps everything as it is for <paramref name="seconds"/>.
		/// </summary>
		/// <param name="seconds">The time to wait.</param>
		public ControlSequence Hold(float seconds)
		{
			RequireSeconds(seconds);
			_steps.Add(new Step(StepKind.Hold, seconds: seconds));
			return this;
		}

		/// <summary>
		/// Keeps everything as it is until <paramref name="condition"/> returns <c>true</c>. If that does not
		/// happen within <paramref name="timeoutSeconds"/>, the sequence releases what is pressed and ends with
		/// <see cref="AutoTapBase.EndReason.TimedOut"/>.
		/// </summary>
		/// <param name="condition">Evaluated once per <see cref="AutoTapBase.Update(float)"/>.</param>
		/// <param name="timeoutSeconds">The longest time to wait.</param>
		public ControlSequence HoldUntil(Func<bool> condition, float timeoutSeconds)
		{
			if (condition == null)
			{
				throw new ArgumentNullException(nameof(condition));
			}

			RequireSeconds(timeoutSeconds);
			_steps.Add(new Step(StepKind.HoldUntil, seconds: timeoutSeconds, condition: condition));
			return this;
		}
	}
}
#endif
