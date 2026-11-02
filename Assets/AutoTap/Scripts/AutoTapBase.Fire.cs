#if ENABLE_AUTOTAP
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UnityAutoTap
{
	/// <remarks>
	/// <para>
	/// The <c>Fire</c> methods queue the first step of an operation and return its <see cref="LogItem"/> at once.
	/// The operation is processed by the input update of the next frame (see <see cref="GetLatestLogs"/>), and
	/// <see cref="Update(float)"/> queues the following steps. A <see cref="LogItem"/> that was returned ends exactly
	/// once.
	/// </para>
	/// <para>
	/// The common rules of all <c>Fire</c> methods:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// A plan that cannot be carried out on the target (a control that the device does not have, a step that does not
	/// fit the kind of device) is a usage error, and the method throws <see cref="ArgumentException"/>.
	/// </description></item>
	/// <item><description>
	/// A method returns <c>null</c> and queues nothing if a position of the plan is outside the screen, if the
	/// previous operation of the same slot or the same device has not ended yet (its end has to be processed first),
	/// if the first step is a press and <see cref="ShouldNotPressOn"/> returns <c>true</c> for the topmost object at
	/// that point, or, for a finger, if all 10 fingers are in use.
	/// </description></item>
	/// <item><description>
	/// A press is made even at a point where no object is hit.
	/// </description></item>
	/// <item><description>
	/// An operation outside the slots has <see cref="LogItem.Index"/> -1; <see cref="LogItem.Device"/> tells which
	/// device it operates.
	/// </description></item>
	/// <item><description>
	/// An operation never presses and releases in the same input update, even if its plan has no time between the
	/// steps; the release is queued in the next frame.
	/// </description></item>
	/// </list>
	/// </remarks>
	public abstract partial class AutoTapBase
	{
		/// <summary>
		/// Taps: presses a finger at <paramref name="position"/>, holds it for <paramref name="duration"/> seconds and
		/// releases it. It is <c>Fire(index, Gesture.PressAt(position).Hold(duration))</c>.
		/// </summary>
		/// <param name="index">The slot, from 0 to the tap count minus 1.</param>
		/// <param name="position">The position in screen pixels.</param>
		/// <param name="duration">The time to hold, in seconds.</param>
		/// <returns>The record of the operation, or <c>null</c> if it was not started (see the class remarks).</returns>
		/// <exception cref="ArgumentException">The plan cannot be carried out.</exception>
		protected internal LogItem Fire(int index, Vector2 position, float duration)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Drags: presses a finger at <paramref name="from"/>, moves it in a straight line to <paramref name="to"/>
		/// in <paramref name="duration"/> seconds and releases it. It is
		/// <c>Fire(index, Gesture.PressAt(from).MoveTo(to, duration))</c>.
		/// </summary>
		/// <param name="index">The slot, from 0 to the tap count minus 1.</param>
		/// <param name="from">The position of the press in screen pixels.</param>
		/// <param name="to">The position of the release in screen pixels.</param>
		/// <param name="duration">The time the move takes, in seconds.</param>
		/// <returns>The record of the operation, or <c>null</c> if it was not started (see the class remarks).</returns>
		/// <exception cref="ArgumentException">The plan cannot be carried out.</exception>
		protected internal LogItem Fire(int index, Vector2 from, Vector2 to, float duration)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Operates a finger as planned by <paramref name="gesture"/>: a path, holding until a condition is met,
		/// a long press followed by a drag, pressure, and so on. Several fingers are moved together by firing several
		/// slots in the same frame.
		/// </summary>
		/// <param name="index">The slot, from 0 to the tap count minus 1.</param>
		/// <param name="gesture">The plan. It has to fit a finger.</param>
		/// <returns>The record of the operation, or <c>null</c> if it was not started (see the class remarks).</returns>
		/// <exception cref="ArgumentException">The plan cannot be carried out.</exception>
		protected internal LogItem Fire(int index, Gesture gesture)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Operates a pointer device other than a touchscreen as planned by <paramref name="gesture"/>: hover, the
		/// buttons of a mouse, a pen with pressure and tilt, scroll.
		/// </summary>
		/// <param name="layout">The layout of the device, derived from <see cref="Pointer"/>.</param>
		/// <param name="gesture">The plan.</param>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>The record of the operation, or <c>null</c> if it was not started (see the class remarks).</returns>
		/// <exception cref="ArgumentException">
		/// The plan cannot be carried out, or <paramref name="layout"/> is a touchscreen or not derived from
		/// <see cref="Pointer"/>.
		/// </exception>
		protected internal LogItem FirePointer(string layout, Gesture gesture, int number = 0)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Operates a device that has no position (a keyboard, a gamepad, a joystick, a sensor, a custom layout) as
		/// planned by <paramref name="sequence"/>.
		/// </summary>
		/// <param name="layout">The layout of the device, not derived from <see cref="Pointer"/>.</param>
		/// <param name="sequence">The plan.</param>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>
		/// The record of the operation, or <c>null</c> if it was not started (see the class remarks). Its
		/// <see cref="LogItem.Target"/> and the other fields about targets stay empty.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// The plan cannot be carried out, or <paramref name="layout"/> is derived from <see cref="Pointer"/>.
		/// </exception>
		protected internal LogItem FireDevice(string layout, ControlSequence sequence, int number = 0)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Operates a virtual mouse. It is <c>FirePointer("Mouse", gesture, number)</c>.
		/// </summary>
		/// <param name="gesture">The plan.</param>
		/// <param name="number">Tells apart several mice.</param>
		/// <returns>See <see cref="FirePointer"/>.</returns>
		protected internal LogItem FireMouse(Gesture gesture, int number = 0)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Operates a virtual pen. It is <c>FirePointer("Pen", gesture, number)</c>.
		/// </summary>
		/// <param name="gesture">The plan.</param>
		/// <param name="number">Tells apart several pens.</param>
		/// <returns>See <see cref="FirePointer"/>.</returns>
		protected internal LogItem FirePen(Gesture gesture, int number = 0)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Scrolls the virtual mouse at <paramref name="screenPosition"/>. It is
		/// <c>FireMouse(Gesture.HoverAt(screenPosition).Scroll(delta))</c>.
		/// </summary>
		/// <param name="screenPosition">The position in screen pixels. The UI there receives the scroll.</param>
		/// <param name="delta">The amount, in the unit of the <c>scroll</c> control of the mouse.</param>
		/// <returns>See <see cref="FirePointer"/>.</returns>
		protected internal LogItem FireScroll(Vector2 screenPosition, Vector2 delta)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Operates a virtual gamepad. It is <c>FireDevice("Gamepad", sequence, number)</c>.
		/// </summary>
		/// <param name="sequence">The plan.</param>
		/// <param name="number">Tells apart several gamepads.</param>
		/// <returns>See <see cref="FireDevice"/>.</returns>
		protected internal LogItem FireGamepad(ControlSequence sequence, int number = 0)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Presses a key of the virtual keyboard, holds it and releases it. The modifiers are pressed before the key
		/// and released after it. No character is entered.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="duration">The time to hold the key, in seconds.</param>
		/// <param name="modifiers">The modifier keys to hold while the key is held.</param>
		/// <returns>See <see cref="FireDevice"/>.</returns>
		protected internal LogItem FireKey(Key key, float duration = 0f, params Key[] modifiers)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Decides whether a press is made at a point. It is asked for each press of a finger or a pointer, with the
		/// topmost result of <see cref="EventSystem.RaycastAll"/> at that point: the object the input module treats
		/// as the first hit. It is not asked when nothing is hit, nor for hovering, nor for operations of
		/// <see cref="FireDevice"/>.
		/// </summary>
		/// <remarks>
		/// When it returns <c>true</c> the press is not made. For the first press of an operation <c>Fire</c> returns
		/// <c>null</c>; for a later press the operation releases what it holds and ends with
		/// <see cref="EndReason.Refused"/>. The objects behind the topmost one are not tried.
		/// </remarks>
		/// <param name="topmost">The topmost hit. Its <see cref="RaycastResult.module"/> tells which raycaster hit it.</param>
		/// <returns><c>true</c> not to press.</returns>
		protected virtual bool ShouldNotPressOn(RaycastResult topmost)
		{
			return false;
		}

		/// <summary>
		/// Creates the <see cref="LogItem"/> that <c>Fire</c> returns. Override it to return an instance of a class
		/// derived from <see cref="LogItem"/>.
		/// </summary>
		/// <remarks>
		/// It is called once for each operation that is accepted. The caller fills in every field of the returned
		/// item afterwards.
		/// </remarks>
		/// <param name="index">The slot, or -1 for an operation outside the slots.</param>
		/// <returns>A new item.</returns>
		protected virtual LogItem NewLogItem(int index)
		{
			return new LogItem();
		}
	}
}
#endif
