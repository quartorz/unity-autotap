#if ENABLE_AUTOTAP
using System;
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
	/// A sequence must not be modified after it was passed to a <c>Fire</c> method.
	/// </para>
	/// </remarks>
	public sealed class ControlSequence
	{
		/// <summary>
		/// Presses a button-like control: writes 1 to it.
		/// </summary>
		/// <param name="controlPath">The path of the control relative to the device.</param>
		public ControlSequence Press(string controlPath)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Releases a button-like control: writes 0 to it.
		/// </summary>
		/// <param name="controlPath">The path of the control relative to the device.</param>
		public ControlSequence Release(string controlPath)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Presses a key of a keyboard. No character is entered; use <see cref="Text"/> for that.
		/// </summary>
		/// <param name="key">The key to press.</param>
		public ControlSequence Press(Key key)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Releases a key of a keyboard.
		/// </summary>
		/// <param name="key">The key to release.</param>
		public ControlSequence Release(Key key)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Presses a button of a gamepad.
		/// </summary>
		/// <param name="button">The button to press.</param>
		public ControlSequence Press(GamepadButton button)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Releases a button of a gamepad.
		/// </summary>
		/// <param name="button">The button to release.</param>
		public ControlSequence Release(GamepadButton button)
		{
			throw new NotImplementedException();
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
			throw new NotImplementedException();
		}

		/// <summary>
		/// Enters text. Only devices that receive text support this, which at present is a <see cref="Keyboard"/>.
		/// </summary>
		/// <param name="text">The characters to enter.</param>
		public ControlSequence Text(string text)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Sets the text being composed by an input method editor. An empty string ends the composition. Only devices
		/// that receive text support this, which at present is a <see cref="Keyboard"/>.
		/// </summary>
		/// <param name="text">The text being composed.</param>
		public ControlSequence Composition(string text)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Keeps everything as it is for <paramref name="seconds"/>.
		/// </summary>
		/// <param name="seconds">The time to wait.</param>
		public ControlSequence Hold(float seconds)
		{
			throw new NotImplementedException();
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
			throw new NotImplementedException();
		}
	}
}
#endif
