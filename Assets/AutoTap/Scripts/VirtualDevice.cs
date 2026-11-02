#if ENABLE_AUTOTAP
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityAutoTap
{
	/// <summary>
	/// A value to write into a control of a virtual device. Used as the argument of
	/// <see cref="VirtualDevice.Queue"/> to change several controls with one event.
	/// </summary>
	public readonly struct ControlValue
	{
		ControlValue(string controlPath, object value)
		{
			ControlPath = controlPath;
			Value = value;
		}

		/// <summary>
		/// Creates a value for the control at <paramref name="controlPath"/>.
		/// </summary>
		/// <typeparam name="T">The value type of the control, for example <see cref="float"/> or <see cref="Vector2"/>.</typeparam>
		/// <param name="controlPath">The path of the control relative to the device, for example <c>leftStick</c>.</param>
		/// <param name="value">The value to write.</param>
		public static ControlValue Of<T>(string controlPath, T value) where T : struct
		{
			return new ControlValue(controlPath, value);
		}

		/// <summary>
		/// The path of the control relative to the device.
		/// </summary>
		public string ControlPath { get; }

		/// <summary>
		/// The value to write, boxed. Its type has to match the value type of the control.
		/// </summary>
		public object Value { get; }
	}

	/// <summary>
	/// A device added by <see cref="VirtualInput"/>, to which input is queued.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="Queue"/>, <see cref="Set{T}"/> and <see cref="ResetToDefault"/> queue exactly one event each.
	/// Events are processed in the order they were queued, at the next input update.
	/// </para>
	/// <para>
	/// The events are built from the state the device will have after all events queued so far are processed, not
	/// from its current state. Controls that are not written keep that state, except the delta-like controls (for
	/// example <c>delta</c> and <c>scroll</c>), which are written from zero for every event.
	/// </para>
	/// <para>
	/// A path that does not name a control of the device, or a value whose type does not match the control, makes the
	/// call throw <see cref="ArgumentException"/>.
	/// </para>
	/// </remarks>
	public class VirtualDevice
	{
		internal VirtualDevice()
		{
		}

		/// <summary>
		/// The underlying Input System device.
		/// </summary>
		public InputDevice Device => throw new NotImplementedException();

		/// <summary>
		/// Writes several controls with a single event, so that the changes are processed as simultaneous.
		/// </summary>
		/// <param name="values">The controls and their values.</param>
		public void Queue(params ControlValue[] values)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Writes one control.
		/// </summary>
		/// <typeparam name="T">The value type of the control.</typeparam>
		/// <param name="controlPath">The path of the control relative to the device, for example <c>buttonSouth</c>.</param>
		/// <param name="value">The value to write.</param>
		public void Set<T>(string controlPath, T value) where T : struct
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Enters text, one text event per character (Unicode code point).
		/// </summary>
		/// <remarks>
		/// Only devices that implement <see cref="UnityEngine.InputSystem.LowLevel.ITextInputReceiver"/> support this
		/// (at present, only <see cref="Keyboard"/>). Calling it on another device throws
		/// <see cref="InvalidOperationException"/>.
		/// </remarks>
		/// <param name="text">The characters to enter.</param>
		public void Text(string text)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Sets the text being composed by an input method editor, with one event. An empty string ends the
		/// composition.
		/// </summary>
		/// <remarks>
		/// Only devices that implement <see cref="UnityEngine.InputSystem.LowLevel.ITextInputReceiver"/> support this
		/// (at present, only <see cref="Keyboard"/>). Calling it on another device throws
		/// <see cref="InvalidOperationException"/>.
		/// </remarks>
		/// <param name="text">The text being composed.</param>
		public void Composition(string text)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Writes the default value into every control that is not marked <c>dontReset</c>, with one event. This is
		/// the same range as the Input System's soft reset.
		/// </summary>
		public void ResetToDefault()
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// A virtual <see cref="Pointer"/> device other than a touchscreen: a <see cref="Mouse"/>, a <see cref="Pen"/> or
	/// any other layout derived from <see cref="Pointer"/>.
	/// </summary>
	/// <remarks>
	/// The position of the pointer stays where it was left, and the UI under it keeps being hovered.
	/// </remarks>
	public sealed class VirtualPointer : VirtualDevice
	{
		internal VirtualPointer()
		{
		}

		/// <summary>
		/// Moves the pointer to <paramref name="position"/>. While a button is pressed this is a drag; otherwise it
		/// is a hover.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		public void MoveTo(Vector2 position)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Presses a button.
		/// </summary>
		/// <param name="button">
		/// The path of a button control relative to the device, for example <c>rightButton</c> of a mouse or
		/// <c>eraser</c> of a pen. <c>null</c> selects the control whose usage is
		/// <see cref="CommonUsages.PrimaryAction"/> (the left button of a mouse, the tip of a pen).
		/// </param>
		public void Press(string button = null)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Releases a button.
		/// </summary>
		/// <param name="button">The button to release. See <see cref="Press"/>.</param>
		public void Release(string button = null)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Scrolls.
		/// </summary>
		/// <param name="delta">The amount of one event, in the unit of the <c>scroll</c> control of the device.</param>
		public void Scroll(Vector2 delta)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// The prediction for the press, move or release that was processed last. It is refreshed at the end of each
		/// input update that processed such an event of this pointer, before
		/// <see cref="IVirtualInput.InputUpdated"/> is raised.
		/// </summary>
		public PointerPrediction Prediction => throw new NotImplementedException();
	}
}
#endif
