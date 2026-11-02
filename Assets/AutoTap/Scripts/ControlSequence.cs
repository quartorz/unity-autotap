#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap
{
	/// <summary>
	/// The plan of one device that has no position: a <see cref="Keyboard"/>, a <see cref="Gamepad"/>, a joystick, a
	/// sensor or any custom layout. Passed to <see cref="AutoTapBase.FireDevice"/>.
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
	/// <c>space</c>). A path that names no control of the device makes <c>FireDevice</c> throw
	/// <see cref="ArgumentException"/>, and so does a layout derived from <see cref="Pointer"/>, which is fired with
	/// <see cref="AutoTapBase.FirePointer"/>.
	/// </para>
	/// <para>
	/// Every step that writes is an event of its own, even when several steps fall into one frame, so the order of the
	/// steps is the order in which the Input System processes them (which a shortcut such as Ctrl+R depends on). A
	/// control is never pressed and released by the same input update: a release that follows a press with no time in
	/// between is made in the next frame.
	/// </para>
	/// <para>
	/// A sequence must not be modified after it was passed to a <c>Fire</c> method.
	/// </para>
	/// </remarks>
	public sealed class ControlSequence
	{
		internal enum StepKind
		{
			Press,
			Release,
			Set,
			Text,
			Composition,
			Hold,
			HoldUntil,
		}

		internal sealed class Step
		{
			public StepKind Kind;

			/// <summary>
			/// A control path, a <see cref="Key"/> or a <see cref="GamepadButton"/>.
			/// </summary>
			public object Control;
			public object Value;
			public Type ValueType;
			public string Text;
			public float Seconds;
			public Func<bool> Condition;
		}

		internal readonly List<Step> Steps = new List<Step>();

		/// <summary>
		/// Checks that the sequence writes something, which a sequence of waiting steps only does not.
		/// </summary>
		/// <exception cref="ArgumentException">The sequence has no step that writes.</exception>
		internal void RequireWrites()
		{
			foreach (var step in Steps)
			{
				switch (step.Kind)
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
			Steps.Add(new Step { Kind = StepKind.Press, Control = RequirePath(controlPath) });
			return this;
		}

		/// <summary>
		/// Releases a button-like control: writes 0 to it.
		/// </summary>
		/// <param name="controlPath">The path of the control relative to the device.</param>
		public ControlSequence Release(string controlPath)
		{
			Steps.Add(new Step { Kind = StepKind.Release, Control = RequirePath(controlPath) });
			return this;
		}

		/// <summary>
		/// Presses a key of a keyboard. No character is entered; use <see cref="Text"/> for that.
		/// </summary>
		/// <param name="key">The key to press.</param>
		public ControlSequence Press(Key key)
		{
			Steps.Add(new Step { Kind = StepKind.Press, Control = key });
			return this;
		}

		/// <summary>
		/// Releases a key of a keyboard.
		/// </summary>
		/// <param name="key">The key to release.</param>
		public ControlSequence Release(Key key)
		{
			Steps.Add(new Step { Kind = StepKind.Release, Control = key });
			return this;
		}

		/// <summary>
		/// Presses a button of a gamepad.
		/// </summary>
		/// <param name="button">The button to press.</param>
		public ControlSequence Press(GamepadButton button)
		{
			Steps.Add(new Step { Kind = StepKind.Press, Control = button });
			return this;
		}

		/// <summary>
		/// Releases a button of a gamepad.
		/// </summary>
		/// <param name="button">The button to release.</param>
		public ControlSequence Release(GamepadButton button)
		{
			Steps.Add(new Step { Kind = StepKind.Release, Control = button });
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
			Steps.Add(new Step
			{
				Kind = StepKind.Set,
				Control = RequirePath(controlPath),
				Value = value,
				ValueType = typeof(T),
				Seconds = seconds,
			});
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

			Steps.Add(new Step { Kind = StepKind.Text, Text = text });
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

			Steps.Add(new Step { Kind = StepKind.Composition, Text = text });
			return this;
		}

		/// <summary>
		/// Keeps everything as it is for <paramref name="seconds"/>.
		/// </summary>
		/// <param name="seconds">The time to wait.</param>
		public ControlSequence Hold(float seconds)
		{
			RequireSeconds(seconds);
			Steps.Add(new Step { Kind = StepKind.Hold, Seconds = seconds });
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
			Steps.Add(new Step { Kind = StepKind.HoldUntil, Condition = condition, Seconds = timeoutSeconds });
			return this;
		}
	}
}
#endif
