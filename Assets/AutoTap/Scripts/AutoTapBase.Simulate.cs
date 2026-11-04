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
	/// The <c>Simulate</c> methods queue the steps at the start of an operation that take no time, and return its
	/// <see cref="LogItem"/> at once. The operation is processed by the input update of the next frame (see
	/// <see cref="GetLatestLogs"/>), and <see cref="Update(float)"/> queues the steps that follow once those were
	/// processed. A <see cref="LogItem"/> that was returned ends exactly once.
	/// </para>
	/// <para>
	/// What is simulated is chosen by what is passed:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// <c>Simulate(slot, gesture)</c>: a finger of the virtual touchscreen, in a slot.
	/// </description></item>
	/// <item><description>
	/// <c>Simulate&lt;Mouse&gt;(gesture)</c>, <c>Simulate("Mouse", gesture)</c>: a pointer other than a touchscreen
	/// (a mouse, a pen), with a <see cref="Gesture"/>.
	/// </description></item>
	/// <item><description>
	/// <c>Simulate&lt;Gamepad&gt;(sequence)</c>, <c>Simulate("Gamepad", sequence)</c>: any device that is not a
	/// pointer (a keyboard, a gamepad, a joystick, a sensor, a custom layout), with a <see cref="ControlSequence"/>.
	/// </description></item>
	/// <item><description>
	/// <c>Simulate(key)</c>: a key of the keyboard.
	/// </description></item>
	/// </list>
	/// <para>
	/// A type and the name of its layout select the same virtual device: <c>Simulate&lt;Mouse&gt;</c> and
	/// <c>Simulate("Mouse")</c> operate the same mouse. A touchscreen is derived from <see cref="Pointer"/>, which a
	/// type constraint cannot refuse, but it is operated only through the slots: <c>Simulate&lt;Touchscreen&gt;</c> and
	/// <c>Simulate("Touchscreen", ...)</c> throw <see cref="ArgumentException"/>.
	/// </para>
	/// <para>
	/// The common rules of all <c>Simulate</c> methods:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// A plan that cannot be carried out on the target (a control that the device does not have, a step that does not
	/// fit the kind of device, a pointer type given where a <see cref="ControlSequence"/> is operated and the other
	/// way round) is a usage error, and the method throws <see cref="ArgumentException"/>.
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
	/// The steps at the start of the plan that take no time are queued by <c>Simulate</c> itself, in the order of the
	/// plan and each as an event of its own, by the same rule by which <see cref="Update(float)"/> queues the steps
	/// of a frame: a step that takes no time is queued in the frame of <c>Simulate</c> as long as no step before it
	/// took time, whether or not it is the first step.
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
		/// Operates a finger in a slot as planned by <paramref name="gesture"/>: a tap, a drag, a path, holding until a
		/// condition is met, a long press followed by a drag, pressure, and so on.
		/// </summary>
		/// <remarks>
		/// <para>
		/// A tap is <c>Gesture.PressAt(position).Hold(duration)</c>, and a drag is
		/// <c>Gesture.PressAt(from).MoveTo(to, duration)</c>. Several fingers are moved together by simulating several
		/// slots in the same frame.
		/// </para>
		/// <para>
		/// The first step and the steps after it that take no time are queued by this method, as events of their own
		/// (see <see cref="Gesture"/>).
		/// </para>
		/// </remarks>
		/// <param name="slot">The slot, from 0 to the slot count minus 1.</param>
		/// <param name="gesture">The plan. It has to fit a finger.</param>
		/// <returns>The record of the operation, or <c>null</c> if it was not started (see the class remarks).</returns>
		/// <exception cref="ArgumentException">The plan cannot be carried out.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="slot"/> is not a slot.</exception>
		protected internal LogItem Simulate(int slot, Gesture gesture)
		{
			ThrowIfDisposed();
			if (gesture == null)
			{
				throw new ArgumentNullException(nameof(gesture));
			}

			return SimulateFinger(slot, gesture);
		}

		/// <summary>
		/// Operates a pointer device other than a touchscreen as planned by <paramref name="gesture"/>: hover, the
		/// buttons of a mouse, a pen with pressure and tilt, scroll. The device is given by its type.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The device is added to the Input System by the first call that names it, even if the call returns
		/// <c>null</c> for another reason. It is the device that <see cref="Simulate(string, Gesture, int)"/> operates
		/// for the name of the layout of <typeparamref name="TDevice"/>. Its position stays where the gesture left it,
		/// so the UI under it stays hovered; the buttons and the other controls that are not marked <c>dontReset</c> go
		/// back to their defaults when the gesture ends.
		/// </para>
		/// <para>
		/// <see cref="Touchscreen"/> is derived from <see cref="Pointer"/>, so the constraint cannot refuse it. It is
		/// operated through the slots (<see cref="Simulate(int, Gesture)"/>); a touchscreen type makes this method throw
		/// <see cref="ArgumentException"/>.
		/// </para>
		/// <para>
		/// The first step and the steps after it that take no time are queued by this method, as events of their own
		/// (see <see cref="Gesture"/>), so one input update processes them: hovering, writing a control and pressing
		/// a button in a row reach the Input System together, in that order. Scrolling is a step of the gesture:
		/// <c>Simulate&lt;Mouse&gt;(Gesture.HoverAt(position).Scroll(delta))</c>. The hover and the scroll are two events
		/// of the same input update, which is enough for the input module to enter the UI under the mouse and scroll it.
		/// </para>
		/// <para>
		/// The <see cref="LogItem"/> follows the first button that the gesture presses if the UI handles that button
		/// (the primary button, and the right and middle buttons of a mouse); for other buttons and for gestures that
		/// press nothing, the fields about targets stay empty.
		/// </para>
		/// </remarks>
		/// <typeparam name="TDevice">
		/// The type of the device, for example <see cref="Mouse"/> or <see cref="Pen"/>. It has to be registered as a
		/// layout of the Input System.
		/// </typeparam>
		/// <param name="gesture">The plan.</param>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>The record of the operation, or <c>null</c> if it was not started (see the class remarks).</returns>
		/// <exception cref="ArgumentException">
		/// The plan cannot be carried out, or <typeparamref name="TDevice"/> is a touchscreen or is not registered as a
		/// layout.
		/// </exception>
		protected internal LogItem Simulate<TDevice>(Gesture gesture, int number = 0) where TDevice : Pointer
		{
			ThrowIfDisposed();
			if (gesture == null)
			{
				throw new ArgumentNullException(nameof(gesture));
			}

			var plan = gesture.PlanForPointer();
			if (!Enabled)
			{
				return null;
			}

			return SimulatePointer(_input.GetPointer<TDevice>(number), gesture, plan);
		}

		/// <summary>
		/// Operates a pointer device other than a touchscreen as planned by <paramref name="gesture"/>. The device is
		/// given by the name of its layout, for a device that has no C# type.
		/// </summary>
		/// <remarks>
		/// <para>
		/// It is <see cref="Simulate{TDevice}(Gesture, int)"/> with the layout as a string: <c>Simulate("Mouse", gesture)</c>
		/// and <c>Simulate&lt;Mouse&gt;(gesture)</c> operate the same virtual mouse. The rules are the same; see there.
		/// </para>
		/// <para>
		/// A touchscreen is operated through the slots (<see cref="Simulate(int, Gesture)"/>); the layout of a
		/// touchscreen makes this method throw <see cref="ArgumentException"/>.
		/// </para>
		/// </remarks>
		/// <param name="layout">The layout of the device, derived from <see cref="Pointer"/>.</param>
		/// <param name="gesture">The plan.</param>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>The record of the operation, or <c>null</c> if it was not started (see the class remarks).</returns>
		/// <exception cref="ArgumentException">
		/// The plan cannot be carried out, or <paramref name="layout"/> is a touchscreen, is not derived from
		/// <see cref="Pointer"/> or is not a layout.
		/// </exception>
		protected internal LogItem Simulate(string layout, Gesture gesture, int number = 0)
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

			return SimulatePointer(_input.GetPointer(layout, number), gesture, plan);
		}

		LogItem SimulatePointer(VirtualPointer pointer, Gesture gesture, PointerPlan plan)
		{
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

			var operation = new PointerOperation(item, ++_startSequence, pointer, gesture, plan.StepCount);
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
		/// planned by <paramref name="sequence"/>. The device is given by its type.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The device is added to the Input System by the first call that names it, even if the call returns
		/// <c>null</c> for another reason. It is the device that <see cref="Simulate(string, ControlSequence, int)"/>
		/// operates for the name of the layout of <typeparamref name="TDevice"/>.
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
		/// <para>
		/// The constraint is <see cref="InputDevice"/> because the devices that are not pointers have no common type
		/// below it. A pointer type (<see cref="Pointer"/> and everything derived from it, a touchscreen included) is
		/// refused when the method is called: those are operated with a <see cref="Gesture"/>.
		/// </para>
		/// </remarks>
		/// <typeparam name="TDevice">
		/// The type of the device, for example <see cref="Keyboard"/> or <see cref="Gamepad"/>. It has to be registered
		/// as a layout of the Input System.
		/// </typeparam>
		/// <param name="sequence">The plan.</param>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>
		/// The record of the operation, or <c>null</c> if it was not started (see the class remarks). Its
		/// <see cref="LogItem.Target"/> and the other fields about targets stay empty.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// The plan cannot be carried out, or <typeparamref name="TDevice"/> is derived from <see cref="Pointer"/> or is
		/// not registered as a layout.
		/// </exception>
		protected internal LogItem Simulate<TDevice>(ControlSequence sequence, int number = 0) where TDevice : InputDevice
		{
			ThrowIfDisposed();
			if (sequence == null)
			{
				throw new ArgumentNullException(nameof(sequence));
			}

			sequence.RequireWrites();
			if (!Enabled)
			{
				return null;
			}

			return SimulateDevice(_input.GetDevice<TDevice>(number), sequence);
		}

		/// <summary>
		/// Operates a device that has no position as planned by <paramref name="sequence"/>. The device is given by the
		/// name of its layout, for a device that has no C# type.
		/// </summary>
		/// <remarks>
		/// It is <see cref="Simulate{TDevice}(ControlSequence, int)"/> with the layout as a string:
		/// <c>Simulate("Gamepad", sequence)</c> and <c>Simulate&lt;Gamepad&gt;(sequence)</c> operate the same virtual
		/// gamepad. The rules are the same; see there.
		/// </remarks>
		/// <param name="layout">The layout of the device, not derived from <see cref="Pointer"/>.</param>
		/// <param name="sequence">The plan.</param>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>
		/// The record of the operation, or <c>null</c> if it was not started (see the class remarks). Its
		/// <see cref="LogItem.Target"/> and the other fields about targets stay empty.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// The plan cannot be carried out, or <paramref name="layout"/> is derived from <see cref="Pointer"/> or is not
		/// a layout.
		/// </exception>
		protected internal LogItem Simulate(string layout, ControlSequence sequence, int number = 0)
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

			return SimulateDevice(_input.GetDevice(layout, number), sequence);
		}

		/// <summary>
		/// Presses a key of the virtual keyboard, holds it and releases it. The modifiers are pressed before the key
		/// and released after it. No character is entered.
		/// </summary>
		/// <remarks>
		/// It is <see cref="Simulate{TDevice}(ControlSequence, int)"/> of <see cref="Keyboard"/> with a sequence that
		/// presses the modifiers and the key, holds, and releases them in the opposite order.
		/// </remarks>
		/// <param name="key">The key.</param>
		/// <param name="duration">The time to hold the key, in seconds.</param>
		/// <param name="modifiers">The modifier keys to hold while the key is held.</param>
		/// <returns>See <see cref="Simulate{TDevice}(ControlSequence, int)"/>.</returns>
		protected internal LogItem Simulate(Key key, float duration = 0f, params Key[] modifiers)
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

			return Simulate<Keyboard>(sequence);
		}

		LogItem SimulateDevice(VirtualDevice device, ControlSequence sequence)
		{
			var buttons = device.ResolveSequence(sequence);
			if (_deviceOperations.ContainsKey(device.Device))
			{
				return null;
			}

			var item = NewLogItem(-1) ?? new LogItem();
			FillItem(item, -1, LogItemKind.Device, device.Device, Vector2.zero, null, null, null, sequence);

			var operation = new DeviceOperation(item, ++_startSequence, device, sequence, buttons);
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

		LogItem SimulateFinger(int index, Gesture gesture)
		{
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

			var operation = new FingerOperation(index, item, ++_startSequence, finger, gesture, plan.StepCount);

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
		/// as the first hit. It is not asked when nothing is hit, nor for hovering, nor for operations of a device
		/// that has no position (<see cref="Simulate{TDevice}(ControlSequence, int)"/>).
		/// </summary>
		/// <remarks>
		/// When it returns <c>true</c> the press is not made. For the first press of an operation <c>Simulate</c> returns
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
		/// Creates the <see cref="LogItem"/> that <c>Simulate</c> returns. Override it to return an instance of a class
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
