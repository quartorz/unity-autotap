#if ENABLE_AUTOTAP
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap
{
	/// <summary>
	/// One change of an input device's state, as reported by <see cref="InputChangeReader"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Changes are numbered by <see cref="Order"/> in the order the Input System processed them. The order is
	/// strict across devices, so a key press, a touch and a scroll that were queued in this order are reported in
	/// this order.
	/// </para>
	/// <para>
	/// Several changes can come from one event (for example a touch that moves while another one ends). They share
	/// <see cref="EventId"/> and are to be treated as having happened at the same time; they are reported in the
	/// order of the controls of the device.
	/// </para>
	/// <para>
	/// The set of derived types is closed. Use pattern matching on the derived type to tell the changes apart.
	/// </para>
	/// </remarks>
	public abstract class InputChange
	{
		internal InputChange()
		{
		}

		/// <summary>
		/// The sequence number of this change within its <see cref="InputChangeReader"/>, starting at 0 and growing
		/// by 1 per reported change. Orders of different readers are not comparable.
		/// </summary>
		public long Order { get; internal set; }

		/// <summary>
		/// <see cref="InputState.updateCount"/> of the input update in which the change was processed.
		/// </summary>
		public int UpdateCount { get; internal set; }

		/// <summary>
		/// <see cref="UnityEngine.Time.frameCount"/> when the change was processed.
		/// </summary>
		public int Frame { get; internal set; }

		/// <summary>
		/// The time of the event that carried the change, in the time base of <see cref="InputEvent.time"/>.
		/// </summary>
		public double Time { get; internal set; }

		/// <summary>
		/// The <see cref="InputEvent.eventId"/> of the event that carried the change.
		/// </summary>
		public int EventId { get; internal set; }

		/// <summary>
		/// The device whose state changed.
		/// </summary>
		public InputDevice Device { get; internal set; }

		/// <summary>
		/// <c>true</c> if <see cref="Device"/> is a device added by <see cref="VirtualInput"/>
		/// (see <see cref="VirtualInput.IsVirtual"/>).
		/// </summary>
		public bool IsVirtual { get; internal set; }
	}

	/// <summary>
	/// A change of a touch of a <see cref="Touchscreen"/>. Reported from the touches of the touchscreen, never from
	/// its primary touch.
	/// </summary>
	public abstract class TouchChange : InputChange
	{
		internal TouchChange()
		{
		}

		/// <summary>
		/// The id of the touch, unique among the touches of the device.
		/// </summary>
		public int TouchId { get; internal set; }

		/// <summary>
		/// The position of the touch in screen pixels after the change.
		/// </summary>
		public Vector2 Position { get; internal set; }

		/// <summary>
		/// The position at which the touch began.
		/// </summary>
		public Vector2 StartPosition { get; internal set; }
	}

	/// <summary>
	/// A touch began.
	/// </summary>
	public sealed class TouchBegan : TouchChange
	{
	}

	/// <summary>
	/// A touch moved, or one of its other controls (pressure, radius) changed, while it stayed in progress.
	/// </summary>
	public sealed class TouchMoved : TouchChange
	{
	}

	/// <summary>
	/// A touch ended. A touch that began and ended in the same input update yields both <see cref="TouchBegan"/> and
	/// <see cref="TouchEnded"/>.
	/// </summary>
	public sealed class TouchEnded : TouchChange
	{
	}

	/// <summary>
	/// A touch was canceled.
	/// </summary>
	public sealed class TouchCanceled : TouchChange
	{
	}

	/// <summary>
	/// A change of a single control of a device other than a touch.
	/// </summary>
	public abstract class ControlChange : InputChange
	{
		internal ControlChange()
		{
		}

		/// <summary>
		/// The control that changed.
		/// </summary>
		public InputControl Control { get; internal set; }

		/// <summary>
		/// The path of <see cref="Control"/> relative to <see cref="InputChange.Device"/>, in the form accepted by
		/// <see cref="VirtualDevice.Set{T}"/> (for example <c>leftStick/x</c> or <c>buttonSouth</c>).
		/// </summary>
		public string ControlPath { get; internal set; }
	}

	/// <summary>
	/// A button-like control (a key, a mouse button, a pen tip, a gamepad button or trigger) went from released to
	/// pressed. A synthetic control, such as the <c>inRange</c> of a pen, is never reported.
	/// </summary>
	public sealed class ButtonDown : ControlChange
	{
	}

	/// <summary>
	/// A button-like control went from pressed to released.
	/// </summary>
	public sealed class ButtonUp : ControlChange
	{
	}

	/// <summary>
	/// A control that is not reported as one of the more specific changes (a stick, a position, a pressure, any
	/// control of a joystick, a sensor or a custom layout) changed its value.
	/// </summary>
	public sealed class ValueChanged : ControlChange
	{
		/// <summary>
		/// The value before the change, boxed as the value type of <see cref="ControlChange.Control"/>.
		/// </summary>
		public object PreviousValue { get; internal set; }

		/// <summary>
		/// The value after the change, boxed as the value type of <see cref="ControlChange.Control"/>.
		/// </summary>
		public object Value { get; internal set; }
	}

	/// <summary>
	/// A scroll control (for example <c>scroll</c> of a mouse) received a scroll.
	/// </summary>
	public sealed class Scrolled : ControlChange
	{
		/// <summary>
		/// The amount scrolled by the event that carried the change, that is, the value after the change minus the
		/// value before it.
		/// </summary>
		public Vector2 Delta { get; internal set; }
	}

	/// <summary>
	/// A character was entered into a device that receives text (for example a keyboard).
	/// </summary>
	public sealed class TextEntered : InputChange
	{
		/// <summary>
		/// The entered character as a string holding one Unicode code point.
		/// </summary>
		public string Text { get; internal set; }
	}

	/// <summary>
	/// The text being composed by an input method editor changed.
	/// </summary>
	public sealed class CompositionChanged : InputChange
	{
		/// <summary>
		/// The text being composed. Empty when the composition ended.
		/// </summary>
		public string Composition { get; internal set; }
	}

	/// <summary>
	/// The Input System reset the state of a device (for example when the device is disabled or the application
	/// loses focus). The controls that changed by the reset are not reported separately.
	/// </summary>
	public sealed class DeviceReset : InputChange
	{
	}
}
#endif
