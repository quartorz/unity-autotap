#if ENABLE_AUTOTAP
using System;
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
	/// A gesture must not be modified after it was passed to a <c>Fire</c> method.
	/// </para>
	/// </remarks>
	public sealed class Gesture
	{
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
			throw new NotImplementedException();
		}

		/// <summary>
		/// Starts a gesture by moving the pointer to <paramref name="position"/> without pressing anything. The UI
		/// there is hovered. It cannot be fired on a finger.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		public static Gesture HoverAt(Vector2 position)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Moves the pointer in a straight line to <paramref name="position"/>, keeping whatever is pressed.
		/// </summary>
		/// <param name="position">The destination in screen pixels.</param>
		/// <param name="seconds">The time the move takes. 0 moves at once.</param>
		public Gesture MoveTo(Vector2 position, float seconds)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Moves the pointer in a straight line to the position returned by <paramref name="position"/>, which is
		/// evaluated when the move starts.
		/// </summary>
		/// <param name="position">Returns the destination in screen pixels.</param>
		/// <param name="seconds">The time the move takes. 0 moves at once.</param>
		public Gesture MoveTo(Func<Vector2> position, float seconds)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Presses a button, in addition to what is already pressed.
		/// </summary>
		/// <param name="button">The button to press. See <see cref="PressAt"/>.</param>
		public Gesture Press(string button = null)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Releases a button.
		/// </summary>
		/// <param name="button">The button to release. See <see cref="PressAt"/>.</param>
		public Gesture Release(string button = null)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Scrolls once, at the current position. It cannot be fired on a finger.
		/// </summary>
		/// <param name="delta">The amount, in the unit of the <c>scroll</c> control of the device.</param>
		public Gesture Scroll(Vector2 delta)
		{
			throw new NotImplementedException();
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
			throw new NotImplementedException();
		}

		/// <summary>
		/// Keeps everything as it is for <paramref name="seconds"/>.
		/// </summary>
		/// <param name="seconds">The time to wait.</param>
		public Gesture Hold(float seconds)
		{
			throw new NotImplementedException();
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
			throw new NotImplementedException();
		}
	}
}
#endif
