#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// A copy of an <see cref="InputChange"/>, made inside the callback, so that a test does not depend on whether the
	/// reader reuses its objects.
	/// </summary>
	public sealed class ChangeRecord
	{
		public Type Type;
		public long Order;
		public int UpdateCount;
		public int Frame;
		public double Time;
		public int EventId;
		public InputDevice Device;
		public bool IsVirtual;

		public int TouchId;
		public Vector2 Position;
		public Vector2 StartPosition;

		public string ControlPath;
		public object PreviousValue;
		public object Value;
		public Vector2 Delta;

		public string Text;
		public string Composition;

		public bool Is<T>() where T : InputChange
		{
			return Type == typeof(T);
		}

		public static ChangeRecord From(InputChange change)
		{
			var record = new ChangeRecord
			{
				Type = change.GetType(),
				Order = change.Order,
				UpdateCount = change.UpdateCount,
				Frame = change.Frame,
				Time = change.Time,
				EventId = change.EventId,
				Device = change.Device,
				IsVirtual = change.IsVirtual,
			};

			switch (change)
			{
				case TouchChange touch:
					record.TouchId = touch.TouchId;
					record.Position = touch.Position;
					record.StartPosition = touch.StartPosition;
					break;
				case ValueChanged valueChanged:
					record.ControlPath = valueChanged.ControlPath;
					record.PreviousValue = valueChanged.PreviousValue;
					record.Value = valueChanged.Value;
					break;
				case Scrolled scrolled:
					record.ControlPath = scrolled.ControlPath;
					record.Delta = scrolled.Delta;
					break;
				case ControlChange control:
					record.ControlPath = control.ControlPath;
					break;
				case TextEntered text:
					record.Text = text.Text;
					break;
				case CompositionChanged composition:
					record.Composition = composition.Composition;
					break;
			}

			return record;
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"#{Order} {Type.Name} {(Device != null ? Device.name : "(no device)")}" +
				$"{(ControlPath != null ? " " + ControlPath : "")} event {EventId} update {UpdateCount} " +
				$"frame {Frame}{(IsVirtual ? " virtual" : "")}";
		}
	}

	/// <summary>
	/// Collects the changes of an <see cref="InputChangeReader"/>.
	/// </summary>
	public sealed class ChangeLog : IDisposable
	{
		readonly InputChangeReader _reader;
		readonly bool _ownsReader;

		public readonly List<ChangeRecord> Items = new List<ChangeRecord>();

		/// <summary>
		/// Collects the changes of a reader owned by someone else, for example <see cref="IVirtualInput.Reader"/>.
		/// </summary>
		public ChangeLog(InputChangeReader reader)
		{
			_reader = reader;
			_reader.Changed += OnChanged;
		}

		/// <summary>
		/// Creates a reader of its own.
		/// </summary>
		/// <param name="deviceFilter">See <see cref="InputChangeReader"/>.</param>
		public ChangeLog(Func<InputDevice, bool> deviceFilter)
		{
			_ownsReader = true;
			_reader = new InputChangeReader(deviceFilter);
			_reader.Changed += OnChanged;
		}

		void OnChanged(InputChange change)
		{
			Items.Add(ChangeRecord.From(change));
		}

		public IEnumerable<ChangeRecord> Of<T>() where T : InputChange
		{
			return Items.Where(i => i.Type == typeof(T));
		}

		public IEnumerable<ChangeRecord> OfDevice(InputDevice device)
		{
			return Items.Where(i => i.Device == device);
		}

		/// <summary>
		/// The changes of <paramref name="device"/> at <paramref name="controlPath"/>, in order.
		/// </summary>
		public IEnumerable<ChangeRecord> At(InputDevice device, string controlPath)
		{
			return Items.Where(i => i.Device == device && i.ControlPath == controlPath);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			_reader.Changed -= OnChanged;
			if (_ownsReader)
			{
				_reader.Dispose();
			}
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			return string.Join("\n", Items.Select(i => i.ToString()));
		}
	}

	/// <summary>
	/// One call of <see cref="InputState.onChange"/>, which is what the reader is built on.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="EventId"/> is 0 when there was no event (the zeroing of deltas at the start of an update,
	/// InputManager.cs 2766-2783). The events that were queued have ids from 1 (InputTestRuntime.m_NextEventId).
	/// </para>
	/// <para>
	/// The event that a reset builds is not queued. The reset of an ordinary device has the id -1
	/// (InputManager.cs, 1549), which reads as <see cref="int.MaxValue"/> because an id keeps 31 bits
	/// (InputEvent.cs, 58 and 137-140), and the reset of the touches of a touchscreen has the id 0
	/// (Touchscreen.cs, 944-975). <see cref="IsQueued"/> tells the events that went through the queue from these.
	/// </para>
	/// </remarks>
	public struct RawChange
	{
		public InputDevice Device;
		public int EventId;
		public FourCC EventType;
		public int Frame;
		public uint UpdateCount;

		/// <summary>
		/// <c>true</c> if an event that went through the queue caused the change: its id is from 1, and is not the
		/// <see cref="int.MaxValue"/> that the id of a reset reads as.
		/// </summary>
		public bool IsQueued => EventId > 0 && EventId != int.MaxValue;
	}

	/// <summary>
	/// Collects the calls of <see cref="InputState.onChange"/>.
	/// </summary>
	public sealed class RawChangeLog : IDisposable
	{
		public readonly List<RawChange> Items = new List<RawChange>();

		public RawChangeLog()
		{
			InputState.onChange += OnChange;
		}

		void OnChange(InputDevice device, InputEventPtr eventPtr)
		{
			Items.Add(new RawChange
			{
				Device = device,
				EventId = eventPtr.valid ? eventPtr.id : 0,
				EventType = eventPtr.type,
				Frame = UnityEngine.Time.frameCount,
				UpdateCount = InputState.updateCount,
			});
		}

		/// <summary>
		/// The devices of the calls that a queued event caused, in order, with consecutive calls for the same device
		/// listed once. The calls without an event (the zeroing of deltas at the start of an update) and the calls
		/// caused by a reset are left out.
		/// </summary>
		public List<InputDevice> Devices()
		{
			var result = new List<InputDevice>();
			foreach (var item in Items.Where(i => i.IsQueued))
			{
				if (result.Count == 0 || result[result.Count - 1] != item.Device)
				{
					result.Add(item.Device);
				}
			}

			return result;
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			InputState.onChange -= OnChange;
		}
	}
}
#endif
