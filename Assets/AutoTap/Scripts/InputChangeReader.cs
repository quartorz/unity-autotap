#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace UnityAutoTap
{
	/// <summary>
	/// Reports the changes of input device states as <see cref="InputChange"/>s, one event at a time, in the order the
	/// Input System processed them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// What is watched:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// Updates: Dynamic updates, and the before-render update for devices that update before render.
	/// </description></item>
	/// <item><description>
	/// Devices: every enabled device, real and virtual. Narrow them down with the filter passed to the constructor.
	/// </description></item>
	/// </list>
	/// <para>
	/// What is reported: <see cref="TouchBegan"/>, <see cref="TouchMoved"/>, <see cref="TouchEnded"/>,
	/// <see cref="TouchCanceled"/>, <see cref="ButtonDown"/>, <see cref="ButtonUp"/>, <see cref="ValueChanged"/>,
	/// <see cref="Scrolled"/>, <see cref="TextEntered"/>, <see cref="CompositionChanged"/> and
	/// <see cref="DeviceReset"/>.
	/// </para>
	/// <para>
	/// What is not reported: events the Input System dropped (disabled device, focus handling, stale time stamp,
	/// handled by <see cref="InputSystem.onEvent"/>), and changes that are not caused by an event (the zeroing of
	/// deltas at the start of an update). Events that the Input System merged into a previous one are reported once,
	/// as the merged result.
	/// </para>
	/// <para>
	/// How a device is read:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// A touchscreen is read through its touches, never through its primary touch. One event that changes several
	/// touches reports each of them.
	/// </description></item>
	/// <item><description>
	/// A button-like control is reported as <see cref="ButtonDown"/> or <see cref="ButtonUp"/> when it crosses its
	/// press point. If its value changes without crossing it (an analog trigger), the change is a
	/// <see cref="ValueChanged"/>.
	/// </description></item>
	/// <item><description>
	/// A control made of axes (a stick, a position, a tilt) is reported as one <see cref="ValueChanged"/> with its
	/// vector value. The delta controls are not reported, except the <c>scroll</c> control, which is reported as
	/// <see cref="Scrolled"/>. Synthetic controls (for example <c>up</c> of a stick) are not reported.
	/// </description></item>
	/// <item><description>
	/// A change that does not come from an event in the queue, such as the reset of a device, is reported as one
	/// <see cref="DeviceReset"/> per input update, instead of the changes of the controls. The reset that comes with
	/// disabling a device is reported although the device is disabled by then.
	/// </description></item>
	/// </list>
	/// <para>
	/// Raw events are neither recorded nor replayable through this class; use <see cref="InputEventTrace"/> for that.
	/// </para>
	/// <para>
	/// <see cref="Changed"/> is raised synchronously inside the input update, right after the state was written and
	/// before the actions are notified.
	/// </para>
	/// </remarks>
	public sealed class InputChangeReader : IDisposable
	{
		enum UnitKind
		{
			Button,
			Value,
			Scroll,
		}

		sealed class Unit
		{
			public InputControl Control;
			public string Path;
			public UnitKind Kind;
		}

		sealed class Tracker
		{
			public InputDevice Device;
			public byte[] Shadow;
			public byte[] Current;
			public bool CurrentFresh;
			public Unit[] Units;
			public Touchscreen Touchscreen;
			public bool LastWasReset;
			public uint LastResetUpdateCount;
		}

		readonly Func<InputDevice, bool> _deviceFilter;
		readonly Dictionary<InputDevice, Tracker> _trackers = new Dictionary<InputDevice, Tracker>();
		readonly List<InputChange> _pending = new List<InputChange>();
		readonly StringBuilder _stringBuilder = new StringBuilder();

		long _nextOrder;
		bool _disposed;
		Action<InputChange> _changed;
		Action _updateEnded;
		Action<InputDevice> _eventProcessed;

		/// <summary>
		/// Starts reading.
		/// </summary>
		/// <param name="deviceFilter">
		/// Selects the devices to report. <c>null</c> reports every enabled device. The filter is evaluated when the
		/// reader first sees a device, which includes the devices that are added after the reader was created, so it
		/// has to give the same answer for a device every time.
		/// </param>
		public InputChangeReader(Func<InputDevice, bool> deviceFilter = null)
		{
			_deviceFilter = deviceFilter;

			foreach (var device in InputSystem.devices)
			{
				if (IsWatched(device))
				{
					_trackers[device] = CreateTracker(device, useDefaultState: false);
				}
			}

			InputState.onChange += OnStateChange;
			InputSystem.onEvent += OnEvent;
			InputSystem.onAfterUpdate += OnAfterUpdate;
			InputSystem.onDeviceChange += OnDeviceChange;
		}

		/// <summary>
		/// Raised for each change, in the order of <see cref="InputChange.Order"/>.
		/// </summary>
		public event Action<InputChange> Changed
		{
			add => _changed += value;
			remove => _changed -= value;
		}

		/// <summary>
		/// Raised at the end of each Dynamic input update, after every change of that update was raised through
		/// <see cref="Changed"/>. Use it to handle the changes frame by frame.
		/// </summary>
		public event Action UpdateEnded
		{
			add => _updateEnded += value;
			remove => _updateEnded -= value;
		}

		/// <summary>
		/// Raised for every event that the Input System processed for a watched device, whether or not it changed
		/// anything that is reported: a state event that wrote the state it already had, a character, a composition.
		/// It is raised before the changes of the event. It is not raised for the reset of a device.
		/// </summary>
		internal event Action<InputDevice> EventProcessed
		{
			add => _eventProcessed += value;
			remove => _eventProcessed -= value;
		}

		/// <summary>
		/// Stops reading. No event is raised afterwards.
		/// </summary>
		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			InputState.onChange -= OnStateChange;
			InputSystem.onEvent -= OnEvent;
			InputSystem.onAfterUpdate -= OnAfterUpdate;
			InputSystem.onDeviceChange -= OnDeviceChange;
			_trackers.Clear();
			_changed = null;
			_updateEnded = null;
			_eventProcessed = null;
		}

		bool IsWatched(InputDevice device)
		{
			return _deviceFilter == null || _deviceFilter(device);
		}

		/// <param name="device">The device to watch.</param>
		/// <param name="useDefaultState">
		/// <c>true</c> to compare the first change with the default state, for a device that was seen for the first
		/// time when it already had changed.
		/// </param>
		static Tracker CreateTracker(InputDevice device, bool useDefaultState)
		{
			var tracker = new Tracker
			{
				Device = device,
				Touchscreen = device as Touchscreen,
			};
			if (!device.added)
			{
				tracker.Shadow = Array.Empty<byte>();
			}
			else if (useDefaultState)
			{
				ReadDefault(device, ref tracker.Shadow);
			}
			else
			{
				ReadCurrent(device, ref tracker.Shadow);
			}

			return tracker;
		}

		static unsafe void ReadDefault(InputDevice device, ref byte[] buffer)
		{
			StateEvent.FromDefaultStateFor(device, out var eventPtr, Allocator.Temp);
			var stateEvent = StateEvent.From(eventPtr);
			var size = (int)stateEvent->stateSizeInBytes;
			if (buffer == null || buffer.Length != size)
			{
				buffer = new byte[size];
			}

			fixed (byte* destination = buffer)
			{
				UnsafeUtility.MemCpy(destination, stateEvent->state, size);
			}
		}

		/// <summary>
		/// Copies the current state of the device, without the offset of the device in the state buffers.
		/// </summary>
		static unsafe void ReadCurrent(InputDevice device, ref byte[] buffer)
		{
			// The event is allocated with Allocator.Temp, which needs no disposing.
			StateEvent.From(device, out var eventPtr, Allocator.Temp);
			var stateEvent = StateEvent.From(eventPtr);
			var size = (int)stateEvent->stateSizeInBytes;
			if (buffer == null || buffer.Length != size)
			{
				buffer = new byte[size];
			}

			fixed (byte* destination = buffer)
			{
				UnsafeUtility.MemCpy(destination, stateEvent->state, size);
			}
		}

		/// <summary>
		/// Makes the current state the one that the next change is compared with.
		/// </summary>
		static void Advance(Tracker tracker)
		{
			if (!tracker.Device.added)
			{
				return;
			}

			EnsureCurrent(tracker);
			var shadow = tracker.Shadow;
			tracker.Shadow = tracker.Current;
			tracker.Current = shadow;
			tracker.CurrentFresh = false;
		}

		static void EnsureCurrent(Tracker tracker)
		{
			if (!tracker.CurrentFresh)
			{
				ReadCurrent(tracker.Device, ref tracker.Current);
				tracker.CurrentFresh = true;
			}
		}

		static unsafe bool SameBytes(byte[] a, byte[] b)
		{
			if (a.Length != b.Length)
			{
				return false;
			}

			if (a.Length == 0)
			{
				return true;
			}

			fixed (byte* pa = a)
			fixed (byte* pb = b)
			{
				return UnsafeUtility.MemCmp(pa, pb, a.Length) == 0;
			}
		}

		/// <summary>
		/// Tells whether the event was made by the Input System itself, which does for the reset of a device. Events
		/// that went through the queue have an id above 0; the event of a reset has the id of -1, which reads as
		/// <see cref="int.MaxValue"/> because the id uses 31 bits, or no id at all.
		/// </summary>
		static bool IsSyntheticEvent(InputEventPtr eventPtr)
		{
			var id = eventPtr.id;
			return id <= 0 || id == int.MaxValue;
		}

		static bool IsReportedUpdate(InputDevice device)
		{
			var updateType = InputState.currentUpdateType;
			return updateType == InputUpdateType.Dynamic
				|| (updateType == InputUpdateType.BeforeRender && device.updateBeforeRender);
		}

		void OnDeviceChange(InputDevice device, InputDeviceChange change)
		{
			if (_disposed)
			{
				return;
			}

			switch (change)
			{
				case InputDeviceChange.Added:
					if (IsWatched(device))
					{
						_trackers[device] = CreateTracker(device, useDefaultState: false);
					}

					break;
				case InputDeviceChange.Removed:
					_trackers.Remove(device);
					break;
			}
		}

		void OnAfterUpdate()
		{
			if (_disposed || InputState.currentUpdateType != InputUpdateType.Dynamic)
			{
				return;
			}

			_updateEnded?.Invoke();
		}

		unsafe void OnEvent(InputEventPtr eventPtr, InputDevice device)
		{
			if (_disposed || device == null || !eventPtr.valid)
			{
				return;
			}

			var isText = eventPtr.type == TextEvent.Type;
			var isComposition = eventPtr.type == IMECompositionEvent.Type;
			if (!isText && !isComposition)
			{
				return;
			}

			if (!device.added || !device.enabled || !IsReportedUpdate(device))
			{
				return;
			}

			if (_deviceFilter != null && !_deviceFilter(device))
			{
				return;
			}

			_eventProcessed?.Invoke(device);

			InputChange change;
			if (isText)
			{
				var utf32 = ((TextEvent*)eventPtr.data)->character;
				string text;
				try
				{
					text = char.ConvertFromUtf32(utf32);
				}
				catch (ArgumentOutOfRangeException)
				{
					return;
				}

				change = new TextEntered { Text = text };
			}
			else
			{
				var composition = ((IMECompositionEvent*)eventPtr.data)->compositionString;
				_stringBuilder.Clear();
				foreach (var c in composition)
				{
					_stringBuilder.Append(c);
				}

				change = new CompositionChanged { Composition = _stringBuilder.ToString() };
			}

			Raise(change, device, eventPtr);
		}

		void OnStateChange(InputDevice device, InputEventPtr eventPtr)
		{
			if (_disposed || device == null)
			{
				return;
			}

			if (!_trackers.TryGetValue(device, out var tracker))
			{
				// A device that is not watched costs nothing but this lookup.
				if (!device.added || !IsWatched(device))
				{
					return;
				}

				tracker = CreateTracker(device, useDefaultState: true);
				_trackers[device] = tracker;
			}

			try
			{
				if (!device.added)
				{
					return;
				}

				if (!eventPtr.valid || !IsReportedUpdate(device))
				{
					return;
				}

				// The Input System disables a device before it resets it, and that reset is reported.
				var isReset = IsSyntheticEvent(eventPtr);
				if (!isReset && !device.enabled)
				{
					return;
				}

				if (!isReset)
				{
					_eventProcessed?.Invoke(device);
				}

				_pending.Clear();
				tracker.CurrentFresh = false;
				if (isReset)
				{
					CollectReset(tracker);
				}
				else
				{
					tracker.LastWasReset = false;
					Collect(tracker);
				}

				// The changes are raised after the state was compared, so that a listener that reads the device
				// sees the state of the whole event.
				for (var i = 0; i < _pending.Count; ++i)
				{
					Raise(_pending[i], device, eventPtr);
				}

				_pending.Clear();
			}
			finally
			{
				if (!_disposed)
				{
					Advance(tracker);
				}
			}
		}

		void CollectReset(Tracker tracker)
		{
			var updateCount = InputState.updateCount;
			if (tracker.LastWasReset && tracker.LastResetUpdateCount == updateCount)
			{
				return;
			}

			EnsureCurrent(tracker);
			if (SameBytes(tracker.Shadow, tracker.Current))
			{
				return;
			}

			tracker.LastWasReset = true;
			tracker.LastResetUpdateCount = updateCount;
			_pending.Add(new DeviceReset());
		}

		unsafe void Collect(Tracker tracker)
		{
			var device = tracker.Device;
			EnsureCurrent(tracker);
			if (tracker.Shadow.Length == 0 || SameBytes(tracker.Shadow, tracker.Current))
			{
				return;
			}

			fixed (byte* shadow = tracker.Shadow)
			fixed (byte* current = tracker.Current)
			{
				if (tracker.Touchscreen != null)
				{
					CollectTouches(tracker.Touchscreen, shadow, current);
					return;
				}

				if (tracker.Units == null)
				{
					tracker.Units = BuildUnits(device);
				}

				var offset = (int)device.stateBlock.byteOffset;
				var currentBase = current - offset;
				var shadowBase = shadow - offset;
				foreach (var unit in tracker.Units)
				{
					var control = unit.Control;
					switch (unit.Kind)
					{
						case UnitKind.Button:
						{
							var button = (ButtonControl)control;
							var before = button.IsValueConsideredPressed(button.ReadValueFromState(shadowBase));
							var after = button.IsValueConsideredPressed(button.ReadValueFromState(currentBase));
							if (before != after)
							{
								ControlChange change = after ? new ButtonDown() : (ControlChange)new ButtonUp();
								change.Control = control;
								change.ControlPath = unit.Path;
								_pending.Add(change);
							}
							else if (control.CompareValue(currentBase, shadowBase))
							{
								_pending.Add(MakeValueChanged(unit, currentBase, shadowBase));
							}

							break;
						}
						case UnitKind.Scroll:
						{
							if (control.CompareValue(currentBase, shadowBase))
							{
								var before = (Vector2)control.ReadValueFromStateAsObject(shadowBase);
								var after = (Vector2)control.ReadValueFromStateAsObject(currentBase);
								_pending.Add(new Scrolled
								{
									Control = control,
									ControlPath = unit.Path,
									Delta = after - before,
								});
							}

							break;
						}
						default:
						{
							if (control.CompareValue(currentBase, shadowBase))
							{
								_pending.Add(MakeValueChanged(unit, currentBase, shadowBase));
							}

							break;
						}
					}
				}
			}
		}

		static unsafe ValueChanged MakeValueChanged(Unit unit, byte* currentBase, byte* shadowBase)
		{
			return new ValueChanged
			{
				Control = unit.Control,
				ControlPath = unit.Path,
				PreviousValue = unit.Control.ReadValueFromStateAsObject(shadowBase),
				Value = unit.Control.ReadValueFromStateAsObject(currentBase),
			};
		}

		unsafe void CollectTouches(Touchscreen touchscreen, byte* shadow, byte* current)
		{
			var touches = touchscreen.touches;
			var count = touches.Count;
			if (count == 0)
			{
				return;
			}

			var relativeOffset = (int)(touches[0].stateBlock.byteOffset - touchscreen.stateBlock.byteOffset);
			var currentStates = (TouchState*)(current + relativeOffset);
			var oldStates = (TouchState*)(shadow + relativeOffset);
			for (var i = 0; i < count; ++i)
			{
				var change = MakeTouchChange(ref oldStates[i], ref currentStates[i]);
				if (change == null)
				{
					continue;
				}

				change.TouchId = currentStates[i].touchId;
				change.Position = currentStates[i].position;
				change.StartPosition = currentStates[i].startPosition;
				_pending.Add(change);
			}
		}

		static TouchChange MakeTouchChange(ref TouchState before, ref TouchState after)
		{
			var sameTouch = before.touchId == after.touchId;
			switch (after.phase)
			{
				case TouchPhase.Began:
					if (!sameTouch || before.phase != TouchPhase.Began)
					{
						return new TouchBegan();
					}

					break;
				case TouchPhase.Ended:
					if (!sameTouch || before.phase != TouchPhase.Ended)
					{
						return new TouchEnded();
					}

					break;
				case TouchPhase.Canceled:
					if (!sameTouch || before.phase != TouchPhase.Canceled)
					{
						return new TouchCanceled();
					}

					break;
				case TouchPhase.Moved:
				case TouchPhase.Stationary:
					if (!sameTouch
						|| before.phase != after.phase
						|| before.position != after.position
						|| before.pressure != after.pressure
						|| before.radius != after.radius)
					{
						return new TouchMoved();
					}

					break;
			}

			return null;
		}

		static Unit[] BuildUnits(InputDevice device)
		{
			var units = new List<Unit>();
			Mouse mouse = device as Mouse;
			Collect(device, device, mouse, units);
			return units.ToArray();
		}

		static void Collect(InputDevice device, InputControl parent, Mouse mouse, List<Unit> units)
		{
			foreach (var child in parent.children)
			{
				if (child.synthetic)
				{
					continue;
				}

				if (mouse != null && child == mouse.scroll)
				{
					units.Add(new Unit { Control = child, Path = RelativePath(device, child), Kind = UnitKind.Scroll });
					continue;
				}

				if (child is DeltaControl)
				{
					continue;
				}

				if (child is ButtonControl)
				{
					units.Add(new Unit { Control = child, Path = RelativePath(device, child), Kind = UnitKind.Button });
				}
				else if (child.children.Count == 0 || IsMadeOfAxes(child))
				{
					units.Add(new Unit { Control = child, Path = RelativePath(device, child), Kind = UnitKind.Value });
				}
				else
				{
					Collect(device, child, mouse, units);
				}
			}
		}

		static bool IsMadeOfAxes(InputControl control)
		{
			var any = false;
			foreach (var child in control.children)
			{
				if (child.synthetic)
				{
					continue;
				}

				if (child is ButtonControl || !(child is AxisControl) || child.children.Count > 0)
				{
					return false;
				}

				any = true;
			}

			return any;
		}

		/// <summary>
		/// The path of a control relative to its device, in the form that <see cref="ControlChange.ControlPath"/> has.
		/// </summary>
		internal static string RelativePath(InputDevice device, InputControl control)
		{
			var devicePath = device.path;
			var path = control.path;
			return path.Length > devicePath.Length + 1 && path.StartsWith(devicePath, StringComparison.Ordinal)
				? path.Substring(devicePath.Length + 1)
				: path;
		}

		void Raise(InputChange change, InputDevice device, InputEventPtr eventPtr)
		{
			change.Order = _nextOrder++;
			change.UpdateCount = (int)InputState.updateCount;
			change.Frame = Time.frameCount;
			change.Time = eventPtr.valid ? eventPtr.time : 0;
			change.EventId = eventPtr.id;
			change.Device = device;
			change.IsVirtual = VirtualInput.IsVirtual(device);

			var handlers = _changed;
			if (handlers == null)
			{
				return;
			}

			foreach (var handler in handlers.GetInvocationList())
			{
				if (_disposed)
				{
					return;
				}

				try
				{
					((Action<InputChange>)handler)(change);
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}
	}
}
#endif
