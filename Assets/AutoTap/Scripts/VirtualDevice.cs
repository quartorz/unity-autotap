#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using BindingFlags = System.Reflection.BindingFlags;
using PropertyInfo = System.Reflection.PropertyInfo;

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
		/// Creates a value from a boxed value of the value type of the control.
		/// </summary>
		internal static ControlValue OfObject(string controlPath, object value)
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
		static readonly PropertyInfo s_DontReset =
			typeof(InputControl).GetProperty("dontReset", BindingFlags.Instance | BindingFlags.NonPublic);

		readonly VirtualInputCore _core;
		readonly InputDevice _device;
		readonly int _offset;
		readonly int _size;
		readonly byte[] _shadow;
		readonly byte[] _scratch;
		readonly byte[] _defaultState;
		readonly byte[] _keepMask;
		readonly InputControl[] _zeroed;
		readonly Vector2Control _position;
		readonly InputControl _delta;
		readonly Dictionary<string, InputControl> _controls = new Dictionary<string, InputControl>();
		readonly HashSet<InputControl> _writable = new HashSet<InputControl>();
		long _lastQueuedStamp = -1;

		internal VirtualDevice(VirtualInputCore core, InputDevice device)
		{
			_core = core;
			_device = device;
			_offset = (int)device.stateBlock.byteOffset;
			_size = (int)((device.stateBlock.sizeInBits + 7) >> 3);
			_shadow = new byte[_size];
			_scratch = new byte[_size];
			_defaultState = new byte[_size];
			ReadState(device, _shadow, useDefaultState: false);
			ReadState(device, _defaultState, useDefaultState: true);
			_keepMask = BuildKeepMask(device, _offset, _size);

			var zeroed = new List<InputControl>();
			if (device is Pointer pointer)
			{
				_position = pointer.position;
				_delta = pointer.delta;
				zeroed.Add(pointer.delta);
			}

			if (device is Mouse mouse)
			{
				zeroed.Add(mouse.scroll);
			}

			_zeroed = zeroed.ToArray();
			ZeroDeltas(_shadow);
		}

		/// <summary>
		/// The underlying Input System device.
		/// </summary>
		public InputDevice Device => _device;

		/// <summary>
		/// Writes several controls with a single event, so that the changes are processed as simultaneous.
		/// </summary>
		/// <param name="values">The controls and their values.</param>
		/// <exception cref="ArgumentException">
		/// A path names no control of the device, or a value does not fit the control. Nothing is queued then.
		/// </exception>
		public void Queue(params ControlValue[] values)
		{
			if (values == null)
			{
				throw new ArgumentNullException(nameof(values));
			}

			var controls = new InputControl[values.Length];
			var converted = new object[values.Length];
			for (var i = 0; i < values.Length; ++i)
			{
				controls[i] = ResolveForWrite(values[i].ControlPath, values[i].Value, out converted[i]);
			}

			QueueEvent(controls, converted, reset: false);
		}

		/// <summary>
		/// Writes one control.
		/// </summary>
		/// <typeparam name="T">The value type of the control.</typeparam>
		/// <param name="controlPath">The path of the control relative to the device, for example <c>buttonSouth</c>.</param>
		/// <param name="value">The value to write.</param>
		/// <exception cref="ArgumentException">The path names no control of the device, or the value does not fit it.</exception>
		public void Set<T>(string controlPath, T value) where T : struct
		{
			Queue(ControlValue.Of(controlPath, value));
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
		/// <exception cref="InvalidOperationException">The device does not receive text.</exception>
		public void Text(string text)
		{
			if (text == null)
			{
				throw new ArgumentNullException(nameof(text));
			}

			QueueText(text);
		}

		/// <summary>
		/// Sets the text being composed by an input method editor, with one event. An empty string ends the
		/// composition.
		/// </summary>
		/// <remarks>
		/// Only devices that implement <see cref="UnityEngine.InputSystem.LowLevel.ITextInputReceiver"/> support this
		/// (at present, only <see cref="Keyboard"/>). Calling it on another device throws
		/// <see cref="InvalidOperationException"/>. A composition has at most 63 characters, which is what an event
		/// holds.
		/// </remarks>
		/// <param name="text">The text being composed.</param>
		/// <exception cref="InvalidOperationException">The device does not receive text.</exception>
		/// <exception cref="ArgumentException"><paramref name="text"/> is longer than 63 characters.</exception>
		public void Composition(string text)
		{
			if (text == null)
			{
				throw new ArgumentNullException(nameof(text));
			}

			QueueComposition(text);
		}

		/// <summary>
		/// Writes the default value into every control that is not marked <c>dontReset</c>, with one event. This is
		/// the same range as the Input System's soft reset.
		/// </summary>
		public void ResetToDefault()
		{
			QueueEvent(Array.Empty<InputControl>(), Array.Empty<object>(), reset: true);
		}

		/// <summary>
		/// <see cref="VirtualInputCore.EndedUpdates"/> when the last event was queued, or -1 before.
		/// </summary>
		internal long LastQueuedStamp => _lastQueuedStamp;

		/// <summary>
		/// The number of Dynamic input updates that ended.
		/// </summary>
		internal long UpdatesEnded => _core.EndedUpdates;

		/// <summary>
		/// <c>true</c> if an event was queued that no input update has consumed yet.
		/// </summary>
		internal bool HasPending => _lastQueuedStamp >= _core.EndedUpdates;

		/// <summary>
		/// <c>true</c> if an event of this device was processed since the end of the previous Dynamic input update.
		/// </summary>
		internal bool ProcessedSinceLastUpdate { get; set; }

		/// <summary>
		/// Finds a control by its path relative to the device.
		/// </summary>
		/// <exception cref="ArgumentException">The device has no such control.</exception>
		internal InputControl FindControl(string controlPath)
		{
			if (string.IsNullOrEmpty(controlPath))
			{
				throw new ArgumentException("The control path is empty.", nameof(controlPath));
			}

			if (!_controls.TryGetValue(controlPath, out var control))
			{
				control = _device.TryGetChildControl(controlPath);
				if (control == null)
				{
					throw new ArgumentException(
						$"The device '{_device.name}' (layout '{_device.layout}') has no control '{controlPath}'.",
						nameof(controlPath));
				}

				_controls[controlPath] = control;
			}

			return control;
		}

		/// <summary>
		/// Finds the control to write and converts the value to its type.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// There is no such control, it cannot be written, or the value does not fit it.
		/// </exception>
		internal InputControl ResolveForWrite(string controlPath, object value, out object converted)
		{
			var control = FindControl(controlPath);
			if (value == null)
			{
				throw new ArgumentException($"The value for '{controlPath}' is null.", nameof(value));
			}

			converted = ConvertForControl(control, value);
			CheckWritable(control);
			return control;
		}

		/// <summary>
		/// Checks that a value of <paramref name="valueType"/> can be written to the control, without having a value.
		/// </summary>
		/// <exception cref="ArgumentException">
		/// There is no such control, it cannot be written, or a value of the type does not fit it.
		/// </exception>
		internal InputControl ResolveForWrite(string controlPath, Type valueType)
		{
			var control = FindControl(controlPath);
			if (!TypesFit(valueType, control.valueType))
			{
				throw new ArgumentException(
					$"The control '{controlPath}' takes a {control.valueType.Name}, not a {valueType.Name}.",
					nameof(valueType));
			}

			CheckWritable(control);
			return control;
		}

		static bool TypesFit(Type valueType, Type controlType)
		{
			return valueType == controlType || (valueType.IsPrimitive && controlType.IsPrimitive);
		}

		static object ConvertForControl(InputControl control, object value)
		{
			var valueType = value.GetType();
			if (!TypesFit(valueType, control.valueType))
			{
				throw new ArgumentException(
					$"The control '{control.name}' takes a {control.valueType.Name}, not a {valueType.Name}.");
			}

			return Interpolation.ConvertTo(value, control.valueType);
		}

		internal unsafe void CheckWritable(InputControl control)
		{
			if (_writable.Contains(control))
			{
				return;
			}

			fixed (byte* scratch = _scratch)
			{
				try
				{
					control.WriteValueFromObjectIntoState(Activator.CreateInstance(control.valueType), scratch - _offset);
				}
				catch (NotSupportedException)
				{
					throw new ArgumentException(
						$"The control '{control.name}' of '{_device.name}' computes its value from other controls and cannot be written. Write the controls it is made of.");
				}
			}

			_writable.Add(control);
		}

		/// <summary>
		/// Reads the value that a control has in the state that was queued last, before the processors of the control.
		/// </summary>
		internal unsafe object ReadControl(InputControl control)
		{
			fixed (byte* shadow = _shadow)
			{
				return ReadUnprocessed(control, shadow - _offset);
			}
		}

		/// <summary>
		/// Reads a control of a given value type without boxing.
		/// </summary>
		internal unsafe T ReadControl<T>(InputControl<T> control) where T : struct
		{
			fixed (byte* shadow = _shadow)
			{
				return control.ReadUnprocessedValueFromState(shadow - _offset);
			}
		}

		static unsafe object ReadUnprocessed(InputControl control, byte* statePtr)
		{
			switch (control)
			{
				case InputControl<float> c:
					return c.ReadUnprocessedValueFromState(statePtr);
				case InputControl<Vector2> c:
					return c.ReadUnprocessedValueFromState(statePtr);
				case InputControl<Vector3> c:
					return c.ReadUnprocessedValueFromState(statePtr);
				case InputControl<Quaternion> c:
					return c.ReadUnprocessedValueFromState(statePtr);
				case InputControl<int> c:
					return c.ReadUnprocessedValueFromState(statePtr);
				case InputControl<double> c:
					return c.ReadUnprocessedValueFromState(statePtr);
				default:
					return control.ReadValueFromStateAsObject(statePtr);
			}
		}

		/// <summary>
		/// Queues one event that writes the values, or, with <paramref name="reset"/>, one event that also returns
		/// the controls that a reset changes to their defaults first.
		/// </summary>
		/// <returns>The stamp of the event (<see cref="VirtualInputCore.EndedUpdates"/> when it was queued).</returns>
		internal unsafe long QueueEvent(InputControl[] controls, object[] values, bool reset)
		{
			if (!_device.added)
			{
				throw new InvalidOperationException($"The device '{_device.name}' is not part of the Input System any more.");
			}

			// The event is allocated with Allocator.Temp, which needs no disposing.
			StateEvent.From(_device, out var eventPtr, Allocator.Temp);
			var stateEvent = StateEvent.From(eventPtr);
			var state = (byte*)stateEvent->state;

			fixed (byte* shadow = _shadow)
			{
				UnsafeUtility.MemCpy(state, shadow, _size);
			}

			if (reset)
			{
				ApplyReset(state);
			}

			var previousPosition = _position != null ? ReadControl(_position) : default;
			for (var i = 0; i < controls.Length; ++i)
			{
				var control = controls[i];
				var statePtr = control.GetStatePtrFromStateEvent(eventPtr);
				if (statePtr == null)
				{
					throw new ArgumentException($"The control '{control.name}' is not part of the state of '{_device.name}'.");
				}

				control.WriteValueFromObjectIntoState(values[i], statePtr);
			}

			if (_position != null)
			{
				var newPosition = _position.ReadUnprocessedValueFromState(state - _offset);
				if (newPosition != previousPosition)
				{
					_delta.WriteValueFromObjectIntoState(newPosition - previousPosition, state - _offset);
				}
			}

			fixed (byte* shadow = _shadow)
			{
				UnsafeUtility.MemCpy(shadow, state, _size);
				ZeroDeltas(shadow);
			}

			InputSystem.QueueEvent(eventPtr);
			_lastQueuedStamp = _core.EndedUpdates;
			return _lastQueuedStamp;
		}

		/// <summary>
		/// <c>true</c> if the device receives text.
		/// </summary>
		internal bool ReceivesText => _device is ITextInputReceiver;

		/// <summary>
		/// The longest composition that an event holds.
		/// </summary>
		internal const int MaxCompositionLength = 63;

		/// <summary>
		/// Queues one text event per character.
		/// </summary>
		/// <returns>The stamp of the last event, or -1 if the text is empty.</returns>
		/// <exception cref="InvalidOperationException">The device does not receive text.</exception>
		internal long QueueText(string text)
		{
			RequireTextReceiver();
			var stamp = -1L;
			var i = 0;
			while (i < text.Length)
			{
				int codePoint;
				if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
				{
					codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
					i += 2;
				}
				else
				{
					codePoint = text[i];
					++i;
				}

				var textEvent = TextEvent.Create(_device.deviceId, codePoint);
				InputSystem.QueueEvent(ref textEvent);
				_lastQueuedStamp = stamp = _core.EndedUpdates;
			}

			return stamp;
		}

		/// <summary>
		/// Queues the event that sets the text being composed.
		/// </summary>
		/// <returns>The stamp of the event.</returns>
		/// <exception cref="InvalidOperationException">The device does not receive text.</exception>
		/// <exception cref="ArgumentException">The text is too long.</exception>
		internal long QueueComposition(string text)
		{
			RequireTextReceiver();
			if (text.Length > MaxCompositionLength)
			{
				throw new ArgumentException(
					$"A composition has at most {MaxCompositionLength} characters, not {text.Length}.", nameof(text));
			}

			var compositionEvent = IMECompositionEvent.Create(_device.deviceId, text, -1);
			InputSystem.QueueEvent(ref compositionEvent);
			_lastQueuedStamp = _core.EndedUpdates;
			return _lastQueuedStamp;
		}

		void RequireTextReceiver()
		{
			if (!(_device is ITextInputReceiver))
			{
				throw new InvalidOperationException(
					$"The device '{_device.name}' (layout '{_device.layout}') does not receive text. Only a keyboard does.");
			}

			if (!_device.added)
			{
				throw new InvalidOperationException($"The device '{_device.name}' is not part of the Input System any more.");
			}
		}

		/// <summary>
		/// Finds the control of every step that presses or releases, and checks the other steps against the device.
		/// </summary>
		/// <returns>
		/// The control of each press and release step, at the index of the step. The other elements are <c>null</c>.
		/// </returns>
		/// <exception cref="ArgumentException">A step does not fit the device.</exception>
		internal ButtonControl[] ResolveSequence(ControlSequence sequence)
		{
			var buttons = new ButtonControl[sequence.Steps.Count];
			for (var i = 0; i < buttons.Length; ++i)
			{
				var step = sequence.Steps[i];
				switch (step.Kind)
				{
					case ControlSequence.StepKind.Press:
					case ControlSequence.StepKind.Release:
						buttons[i] = ResolveStepButton(step.Control);
						break;
					case ControlSequence.StepKind.Set:
					{
						var control = ResolveForWrite((string)step.Control, step.ValueType);
						Interpolation.ConvertTo(step.Value, control.valueType);
						break;
					}
					case ControlSequence.StepKind.Text:
					case ControlSequence.StepKind.Composition:
						if (!ReceivesText)
						{
							throw new ArgumentException(
								$"The device '{_device.name}' (layout '{_device.layout}') does not receive text. Only a keyboard does.");
						}

						if (step.Kind == ControlSequence.StepKind.Composition && step.Text.Length > MaxCompositionLength)
						{
							throw new ArgumentException(
								$"A composition has at most {MaxCompositionLength} characters, not {step.Text.Length}.");
						}

						break;
				}
			}

			return buttons;
		}

		ButtonControl ResolveStepButton(object control)
		{
			switch (control)
			{
				case string path:
				{
					var found = FindControl(path);
					if (!(found is ButtonControl button))
					{
						throw new ArgumentException($"The control '{path}' of '{_device.name}' is not a button.");
					}

					CheckWritable(button);
					return button;
				}
				case Key key:
				{
					if (!(_device is Keyboard keyboard))
					{
						throw new ArgumentException(
							$"The key {key} needs a keyboard, but '{_device.name}' has the layout '{_device.layout}'.");
					}

					try
					{
						return keyboard[key];
					}
					catch (ArgumentOutOfRangeException)
					{
						throw new ArgumentException($"{key} is not a key that a keyboard has.");
					}
				}
				case GamepadButton gamepadButton:
				{
					if (!(_device is Gamepad gamepad))
					{
						throw new ArgumentException(
							$"The button {gamepadButton} needs a gamepad, but '{_device.name}' has the layout '{_device.layout}'.");
					}

					try
					{
						return gamepad[gamepadButton];
					}
					catch (System.ComponentModel.InvalidEnumArgumentException)
					{
						throw new ArgumentException($"{gamepadButton} is not a button that a gamepad has.");
					}
				}
				default:
					throw new ArgumentException($"A control of type {control?.GetType().Name} cannot be pressed.");
			}
		}

		/// <summary>
		/// Queues one event that returns the controls that a reset changes to their defaults, unless that changes
		/// nothing.
		/// </summary>
		/// <returns>The stamp of the event, or -1 if nothing was queued.</returns>
		internal long QueueResetIfNeeded()
		{
			return ResetWouldChange() ? QueueEvent(Array.Empty<InputControl>(), Array.Empty<object>(), reset: true) : -1;
		}

		/// <summary>
		/// Tells whether a reset would change the state that was queued last.
		/// </summary>
		internal unsafe bool ResetWouldChange()
		{
			for (var i = 0; i < _size; ++i)
			{
				var keep = _keepMask[i];
				var reset = (byte)((_shadow[i] & keep) | (_defaultState[i] & ~keep));
				if (reset != _shadow[i])
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Takes the state of the device as it is after the Input System reset it.
		/// </summary>
		internal unsafe void ResyncAfterReset()
		{
			if (!_device.added)
			{
				return;
			}

			ReadState(_device, _shadow, useDefaultState: false);
			fixed (byte* shadow = _shadow)
			{
				ZeroDeltas(shadow);
			}
		}

		unsafe void ApplyReset(byte* state)
		{
			for (var i = 0; i < _size; ++i)
			{
				var keep = _keepMask[i];
				state[i] = (byte)((state[i] & keep) | (_defaultState[i] & ~keep));
			}
		}

		unsafe void ZeroDeltas(byte[] buffer)
		{
			fixed (byte* pointer = buffer)
			{
				ZeroDeltas(pointer);
			}
		}

		unsafe void ZeroDeltas(byte* state)
		{
			foreach (var control in _zeroed)
			{
				control.WriteValueFromObjectIntoState(Vector2.zero, state - _offset);
			}
		}

		static unsafe void ReadState(InputDevice device, byte[] destination, bool useDefaultState)
		{
			// The event is allocated with Allocator.Temp, which needs no disposing.
			InputEventPtr eventPtr;
			if (useDefaultState)
			{
				StateEvent.FromDefaultStateFor(device, out eventPtr, Allocator.Temp);
			}
			else
			{
				StateEvent.From(device, out eventPtr, Allocator.Temp);
			}

			var stateEvent = StateEvent.From(eventPtr);
			fixed (byte* pointer = destination)
			{
				UnsafeUtility.MemCpy(pointer, stateEvent->state, destination.Length);
			}
		}

		/// <summary>
		/// Finds the bits that a reset leaves alone: the controls that are marked <c>dontReset</c>, which includes the
		/// noisy ones.
		/// </summary>
		static byte[] BuildKeepMask(InputDevice device, int deviceOffset, int size)
		{
			if (s_DontReset == null)
			{
				throw new InvalidOperationException(
					"This version of the Input System has no InputControl.dontReset, which the reset of a virtual device needs.");
			}

			var mask = new byte[size];
			foreach (var control in device.allControls)
			{
				if (!(bool)s_DontReset.GetValue(control))
				{
					continue;
				}

				var block = control.stateBlock;
				var first = (int)(block.byteOffset - deviceOffset) * 8 + (int)block.bitOffset;
				var end = first + (int)block.sizeInBits;
				for (var bit = Math.Max(0, first); bit < Math.Min(end, size * 8); ++bit)
				{
					mask[bit >> 3] |= (byte)(1 << (bit & 7));
				}
			}

			return mask;
		}
	}
}
#endif
