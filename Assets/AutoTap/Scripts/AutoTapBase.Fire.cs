#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UnityAutoTap
{
	/// <remarks>
	/// <para>
	/// The <c>Fire</c> methods queue the steps at the start of an operation that take no time, and return its
	/// <see cref="LogItem"/> at once. The operation is processed by the input update of the next frame (see
	/// <see cref="GetLatestLogs"/>), and <see cref="Update(float)"/> queues the steps that follow once those were
	/// processed. A <see cref="LogItem"/> that was returned ends exactly once.
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
	/// The steps at the start of the plan that take no time are queued by <c>Fire</c> itself, in the order of the
	/// plan and each as an event of its own, by the same rule by which <see cref="Update(float)"/> queues the steps
	/// of a frame: a step that takes no time is queued in the frame of <c>Fire</c> as long as no step before it took
	/// time, whether or not it is the first step.
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
			var gesture = Gesture.PressAt(position).Hold(Mathf.Max(0f, duration));
			return FireFinger(index, gesture);
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
			var gesture = Gesture.PressAt(from).MoveTo(to, Mathf.Max(0f, duration));
			return FireFinger(index, gesture);
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
			if (gesture == null)
			{
				throw new ArgumentNullException(nameof(gesture));
			}

			return FireFinger(index, gesture);
		}

		/// <summary>
		/// Operates a pointer device other than a touchscreen as planned by <paramref name="gesture"/>: hover, the
		/// buttons of a mouse, a pen with pressure and tilt, scroll.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The device is added to the Input System by the first call that names it, even if the call returns
		/// <c>null</c> for another reason. Its position stays where the gesture left it, so the UI under it stays
		/// hovered; the buttons and the other controls that are not marked <c>dontReset</c> go back to their
		/// defaults when the gesture ends.
		/// </para>
		/// <para>
		/// The first step and the steps after it that take no time are queued by this method, as events of their own
		/// (see <see cref="Gesture"/>), so one input update processes them: hovering, writing a control and pressing
		/// a button in a row reach the Input System together, in that order.
		/// </para>
		/// <para>
		/// The <see cref="LogItem"/> follows the first button that the gesture presses if the UI handles that button
		/// (the primary button, and the right and middle buttons of a mouse); for other buttons and for gestures that
		/// press nothing, the fields about targets stay empty.
		/// </para>
		/// </remarks>
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
			ThrowIfDisposed();

			if (gesture == null)
			{
				throw new ArgumentNullException(nameof(gesture));
			}

			if (string.IsNullOrEmpty(layout))
			{
				throw new ArgumentException("The layout is empty.", nameof(layout));
			}

			var plan = gesture.PlanForPointer();
			if (!Enabled)
			{
				return null;
			}

			var pointer = _input.GetPointer(layout, number);
			pointer.ValidateGesture(gesture);
			if (_deviceOperations.ContainsKey(pointer.Device))
			{
				return null;
			}

			foreach (var point in plan.Points)
			{
				if (IsOutsideScreen(point))
				{
					return null;
				}
			}

			GameObject target = null;
			GameObject dragTarget = null;
			if (plan.StartsWithPress)
			{
				var probe = PointerPredictor.Probe(plan.Start);
				if (probe.Topmost.gameObject != null && ShouldNotPressOn(probe.Topmost))
				{
					return null;
				}

				// Only the buttons that the UI handles are predicted.
				if (pointer.TrackOf(pointer.ResolveButton(plan.FirstButton)) != null)
				{
					target = probe.Press;
					dragTarget = probe.Drag;
				}
			}

			var item = NewLogItem(-1) ?? new LogItem();
			FillItem(item, -1, LogItemKind.Pointer, pointer.Device, plan.Start, target, dragTarget, gesture, null);

			var operation = new PointerOperation(item, ++_fireSequence, pointer, gesture, plan.StepCount);
			operation.Refuses = position =>
			{
				var topmost = PointerPredictor.Probe(position).Topmost;
				return topmost.gameObject != null && ShouldNotPressOn(topmost);
			};

			_operations.Add(operation);
			_deviceOperations.Add(pointer.Device, operation);
			operation.QueueInitialSteps(plan);
			ShowPointerMarker(pointer, operation.Position);
			return item;
		}

		/// <summary>
		/// Operates a device that has no position (a keyboard, a gamepad, a joystick, a sensor, a custom layout) as
		/// planned by <paramref name="sequence"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The device is added to the Input System by the first call that names it, even if the call returns
		/// <c>null</c> for another reason.
		/// </para>
		/// <para>
		/// This method queues the steps at the start of the sequence that take no time (a press, a release, text, a
		/// composition, a value that is set at once, a hold of no time), each as an event of its own and in the order
		/// of the sequence; the others follow in the next frames, once those were processed. A sequence that begins
		/// with a step that waits, or that changes a value gradually, queues nothing yet; it is carried on once that
		/// step is over. A release that follows a press of the same frame waits for the next one. When the sequence
		/// ends, whatever it still holds pressed is released, in the opposite order, and the controls that are not
		/// marked <c>dontReset</c> go back to their defaults. The controls of a sensor are noisy, which makes them
		/// <c>dontReset</c>, so a sensor keeps the value that was set.
		/// </para>
		/// </remarks>
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
			return FireDeviceSequence(layout, sequence, number);
		}

		/// <summary>
		/// Operates a virtual mouse. It is <c>FirePointer("Mouse", gesture, number)</c>.
		/// </summary>
		/// <param name="gesture">The plan.</param>
		/// <param name="number">Tells apart several mice.</param>
		/// <returns>See <see cref="FirePointer"/>.</returns>
		protected internal LogItem FireMouse(Gesture gesture, int number = 0)
		{
			return FirePointer("Mouse", gesture, number);
		}

		/// <summary>
		/// Operates a virtual pen. It is <c>FirePointer("Pen", gesture, number)</c>.
		/// </summary>
		/// <param name="gesture">The plan.</param>
		/// <param name="number">Tells apart several pens.</param>
		/// <returns>See <see cref="FirePointer"/>.</returns>
		protected internal LogItem FirePen(Gesture gesture, int number = 0)
		{
			return FirePointer("Pen", gesture, number);
		}

		/// <summary>
		/// Scrolls the virtual mouse at <paramref name="screenPosition"/>. It is
		/// <c>FireMouse(Gesture.HoverAt(screenPosition).Scroll(delta))</c>: the hover and the scroll are two events of
		/// the same input update, which is enough for the input module to enter the UI under the mouse and scroll it.
		/// </summary>
		/// <param name="screenPosition">The position in screen pixels. The UI there receives the scroll.</param>
		/// <param name="delta">The amount, in the unit of the <c>scroll</c> control of the mouse.</param>
		/// <returns>See <see cref="FirePointer"/>.</returns>
		protected internal LogItem FireScroll(Vector2 screenPosition, Vector2 delta)
		{
			return FireMouse(Gesture.HoverAt(screenPosition).Scroll(delta));
		}

		/// <summary>
		/// Operates a virtual gamepad. It is <c>FireDevice("Gamepad", sequence, number)</c>.
		/// </summary>
		/// <param name="sequence">The plan.</param>
		/// <param name="number">Tells apart several gamepads.</param>
		/// <returns>See <see cref="FireDevice"/>.</returns>
		protected internal LogItem FireGamepad(ControlSequence sequence, int number = 0)
		{
			return FireDevice("Gamepad", sequence, number);
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
			var sequence = new ControlSequence();
			if (modifiers != null)
			{
				foreach (var modifier in modifiers)
				{
					sequence.Press(modifier);
				}
			}

			sequence.Press(key).Hold(Mathf.Max(0f, duration)).Release(key);
			if (modifiers != null)
			{
				for (var i = modifiers.Length - 1; i >= 0; --i)
				{
					sequence.Release(modifiers[i]);
				}
			}

			return FireDeviceSequence("Keyboard", sequence, 0);
		}

		LogItem FireDeviceSequence(string layout, ControlSequence sequence, int number)
		{
			ThrowIfDisposed();

			if (sequence == null)
			{
				throw new ArgumentNullException(nameof(sequence));
			}

			if (string.IsNullOrEmpty(layout))
			{
				throw new ArgumentException("The layout is empty.", nameof(layout));
			}

			sequence.RequireWrites();
			if (!Enabled)
			{
				return null;
			}

			var device = _input.GetDevice(layout, number);
			var buttons = device.ResolveSequence(sequence);
			if (_deviceOperations.ContainsKey(device.Device))
			{
				return null;
			}

			var item = NewLogItem(-1) ?? new LogItem();
			FillItem(item, -1, LogItemKind.Device, device.Device, Vector2.zero, null, null, null, sequence);

			var operation = new DeviceOperation(item, ++_fireSequence, device, sequence, buttons);
			_operations.Add(operation);
			_deviceOperations.Add(device.Device, operation);
			operation.QueueInitialSteps();
			return item;
		}

		static bool IsOutsideScreen(Vector2 point)
		{
			return point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height;
		}

		/// <summary>
		/// Sets every field of a new <see cref="LogItem"/>, so that nothing of what the override of
		/// <see cref="NewLogItem"/> did is left except its type and its own fields.
		/// </summary>
		void FillItem(
			LogItem item, int index, LogItemKind kind, InputDevice device, Vector2 start, GameObject target,
			GameObject dragTarget, Gesture gesture, ControlSequence sequence)
		{
			item.Index = index;
			item.Kind = kind;
			item.Device = device;
			item.DateTime = DateTime.Now;
			item.Target = target;
			item.ScreenPoint = start;
			item.FrameCount = Time.frameCount;
			item.Order = null;
			item.StartedFrameCount = 0;
			item.Dragged = false;
			item.DragStartedOrder = null;
			item.DragStartedFrameCount = 0;
			item.DragTarget = dragTarget;
			item.DropTarget = null;
			item.Completed = false;
			item.EndReason = EndReason.Finished;
			item.CompletedOrder = null;
			item.CompletedFrameCount = 0;
			item.ClickExecuted = false;
			item.ClickTarget = null;
			item.Gesture = gesture;
			item.Sequence = sequence;
		}

		LogItem FireFinger(int index, Gesture gesture)
		{
			ThrowIfDisposed();

			if (index < 0 || index >= _slots.Length)
			{
				throw new ArgumentOutOfRangeException(nameof(index), $"The slot has to be between 0 and {_slots.Length - 1}.");
			}

			var plan = gesture.PlanForFinger();
			if (!Enabled || _slots[index] != null)
			{
				return null;
			}

			foreach (var point in plan.Points)
			{
				if (IsOutsideScreen(point))
				{
					return null;
				}
			}

			var probe = PointerPredictor.Probe(plan.Start);
			if (probe.Topmost.gameObject != null && ShouldNotPressOn(probe.Topmost))
			{
				return null;
			}

			var finger = _input.NewFinger();
			if (finger == null)
			{
				return null;
			}

			LogItem item;
			try
			{
				item = NewLogItem(index) ?? new LogItem();
			}
			catch
			{
				finger.Abandon();
				throw;
			}

			FillItem(item, index, LogItemKind.Finger, finger.Device, plan.Start, probe.Press, probe.Drag, gesture, null);

			var operation = new FingerOperation(index, item, ++_fireSequence, finger, gesture, plan.StepCount);

			finger.Press(plan.Start);
			_slots[index] = operation;
			_operations.Add(operation);
			_fingerOperations.Add(finger.TouchId, operation);
			operation.Runner.AdvanceAfterFirstStep(true);
			ShowMarker(index, finger.Position);
			return item;
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
