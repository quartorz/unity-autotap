#if ENABLE_AUTOTAP
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityAutoTap
{
	public abstract partial class AutoTapBase
	{
		/// <summary>
		/// The kind of operation a <see cref="LogItem"/> records.
		/// </summary>
		public enum LogItemKind
		{
			/// <summary>
			/// A finger of the virtual touchscreen that follows a <see cref="UnityAutoTap.Gesture"/>
			/// (<see cref="Simulate(int, UnityAutoTap.Gesture)"/>).
			/// </summary>
			Finger,

			/// <summary>
			/// A pointer other than a finger that follows a <see cref="UnityAutoTap.Gesture"/>
			/// (<see cref="Simulate{TDevice}(UnityAutoTap.Gesture, int)"/>,
			/// <see cref="Simulate(string, UnityAutoTap.Gesture, int)"/>).
			/// </summary>
			Pointer,

			/// <summary>
			/// A device that has no position and follows a <see cref="ControlSequence"/>
			/// (<see cref="Simulate{TDevice}(ControlSequence, int)"/>, <see cref="Simulate(string, ControlSequence, int)"/>,
			/// <see cref="Simulate(Key, float, Key[])"/>).
			/// </summary>
			Device,
		}

		/// <summary>
		/// Why an operation ended.
		/// </summary>
		public enum EndReason
		{
			/// <summary>
			/// The plan was carried out to its end.
			/// </summary>
			Finished,

			/// <summary>
			/// <see cref="Stop"/> ended the operation, the Input System reset the device while the operation was
			/// running, or the Input System discarded an event that the operation queued after it had started (a
			/// release, for example).
			/// </summary>
			Interrupted,

			/// <summary>
			/// A <c>HoldUntil</c> step reached its timeout. The operation released what it held and ended.
			/// </summary>
			TimedOut,

			/// <summary>
			/// The event that started the operation was dropped by the Input System, so the operation never started.
			/// </summary>
			Dropped,

			/// <summary>
			/// <see cref="ShouldNotPressOn"/> returned <c>true</c> for a press in the middle of the operation. The
			/// operation released what it held and ended.
			/// </summary>
			Refused,
		}

		/// <summary>
		/// What happened to a <see cref="LogItem"/>.
		/// </summary>
		public enum LogEventKind
		{
			/// <summary>
			/// The first change of the operation was processed (<see cref="LogItem.Order"/>).
			/// </summary>
			Started,

			/// <summary>
			/// The pointer moved beyond the drag threshold, so that the input module delivers begin-drag
			/// (<see cref="LogItem.DragStartedOrder"/>).
			/// </summary>
			DragStarted,

			/// <summary>
			/// The operation ended (<see cref="LogItem.CompletedOrder"/>).
			/// </summary>
			Completed,
		}

		/// <summary>
		/// One thing that happened to a <see cref="LogItem"/>, as returned by <see cref="GetLatestLogs"/>.
		/// </summary>
		public readonly struct LogEvent
		{
			/// <summary>
			/// Creates an element of the log.
			/// </summary>
			/// <param name="item">The item it happened to.</param>
			/// <param name="kind">What happened.</param>
			/// <param name="order">The sequence number of the processed change that caused it, if any.</param>
			public LogEvent(LogItem item, LogEventKind kind, long? order)
			{
				Item = item;
				Kind = kind;
				Order = order;
			}

			/// <summary>
			/// The item it happened to.
			/// </summary>
			public LogItem Item { get; }

			/// <summary>
			/// What happened.
			/// </summary>
			public LogEventKind Kind { get; }

			/// <summary>
			/// The sequence number of the processed change that caused it (<see cref="InputChange.Order"/>), the same
			/// as <see cref="LogItem.Order"/>, <see cref="LogItem.DragStartedOrder"/> or
			/// <see cref="LogItem.CompletedOrder"/> according to <see cref="Kind"/>. <c>null</c> for
			/// <see cref="LogEventKind.Completed"/> of an item that ended without a processed change, which is
			/// the case for <see cref="EndReason.Dropped"/>.
			/// </summary>
			public long? Order { get; }

			/// <inheritdoc/>
			public override string ToString()
			{
				return $"{Kind}: {Item}";
			}
		}

		/// <summary>
		/// What happened in one Dynamic input update, as returned by <see cref="GetLatestLogs"/>: a read-only list of
		/// <see cref="LogEvent"/>s in the order the Input System processed them.
		/// </summary>
		/// <remarks>
		/// <para>
		/// It is a view of the log of this auto tap, not a copy, and it is valid only until the next Dynamic input
		/// update starts, which builds the log again. After that, enumerating it, reading <see cref="Count"/> or reading
		/// an element throws <see cref="InvalidOperationException"/>. Copy what is needed later (<c>ToArray()</c> of
		/// LINQ, for example).
		/// </para>
		/// <para>
		/// Enumerating it with <c>foreach</c> allocates nothing, the way a <see cref="List{T}"/> does not. Enumerating
		/// it as an <see cref="IEnumerable{T}"/>, which LINQ does, boxes the enumerator and allocates.
		/// </para>
		/// <para>
		/// A <c>default</c> value is an empty sequence.
		/// </para>
		/// </remarks>
		public readonly struct LogEventSequence : IReadOnlyList<LogEvent>
		{
			readonly AutoTapBase _owner;
			readonly int _version;

			internal LogEventSequence(AutoTapBase owner, int version)
			{
				_owner = owner;
				_version = version;
			}

			/// <summary>
			/// The number of elements.
			/// </summary>
			/// <exception cref="InvalidOperationException">The log was built again since this sequence was returned.</exception>
			public int Count
			{
				get
				{
					ThrowIfOutOfDate();
					return _owner != null ? _owner._log.Count : 0;
				}
			}

			/// <summary>
			/// Gives the element at an index.
			/// </summary>
			/// <param name="index">The index, from 0 to <see cref="Count"/> - 1.</param>
			/// <exception cref="InvalidOperationException">The log was built again since this sequence was returned.</exception>
			/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an index of this sequence.</exception>
			public LogEvent this[int index]
			{
				get
				{
					ThrowIfOutOfDate();
					if (_owner == null || (uint)index >= (uint)_owner._log.Count)
					{
						throw new ArgumentOutOfRangeException(nameof(index));
					}

					return _owner._log[index];
				}
			}

			/// <summary>
			/// Gives an enumerator that allocates nothing, which <c>foreach</c> uses.
			/// </summary>
			/// <exception cref="InvalidOperationException">The log was built again since this sequence was returned.</exception>
			public Enumerator GetEnumerator()
			{
				ThrowIfOutOfDate();
				return new Enumerator(this);
			}

			IEnumerator<LogEvent> IEnumerable<LogEvent>.GetEnumerator()
			{
				return GetEnumerator();
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			void ThrowIfOutOfDate()
			{
				if (_owner != null && _owner._logVersion != _version)
				{
					throw new InvalidOperationException(
						"The log was built again by an input update after this sequence was returned. "
						+ "Copy the elements that are needed later.");
				}
			}

			/// <summary>
			/// Enumerates a <see cref="LogEventSequence"/>.
			/// </summary>
			public struct Enumerator : IEnumerator<LogEvent>
			{
				readonly LogEventSequence _sequence;
				int _index;
				LogEvent _current;

				internal Enumerator(LogEventSequence sequence)
				{
					_sequence = sequence;
					_index = 0;
					_current = default;
				}

				/// <summary>
				/// The element that <see cref="MoveNext"/> moved to.
				/// </summary>
				public LogEvent Current => _current;

				object IEnumerator.Current => _current;

				/// <summary>
				/// Moves to the next element.
				/// </summary>
				/// <returns><c>false</c> if there is none.</returns>
				/// <exception cref="InvalidOperationException">The log was built again since the sequence was returned.</exception>
				public bool MoveNext()
				{
					_sequence.ThrowIfOutOfDate();
					if (_sequence._owner != null && _index < _sequence._owner._log.Count)
					{
						_current = _sequence._owner._log[_index];
						++_index;
						return true;
					}

					_current = default;
					return false;
				}

				/// <summary>
				/// Starts again before the first element.
				/// </summary>
				public void Reset()
				{
					_index = 0;
					_current = default;
				}

				/// <summary>
				/// Does nothing.
				/// </summary>
				public void Dispose()
				{
				}
			}
		}

		/// <summary>
		/// The record of one operation. It is returned by <c>Simulate</c> as soon as the operation was accepted, and is
		/// filled in as the operation proceeds. Every returned item ends exactly once.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Fields about the targets (<see cref="Target"/>, <see cref="ClickTarget"/>, <see cref="ClickExecuted"/>,
		/// <see cref="Dragged"/>, <see cref="DragTarget"/>, <see cref="DropTarget"/>) are predictions of what the
		/// input module delivers (see <see cref="PointerPrediction"/>). They are set only for fingers and pointers;
		/// for operations of <see cref="LogItemKind.Device"/> they stay empty.
		/// </para>
		/// <para>
		/// The sequence numbers (<see cref="Order"/>, <see cref="DragStartedOrder"/>, <see cref="CompletedOrder"/>)
		/// are <see cref="InputChange.Order"/>s of the reader of the virtual input, so they can be compared with each
		/// other. A number is <c>null</c> until the change that defines it was processed, and stays <c>null</c> if
		/// that never happens.
		/// </para>
		/// </remarks>
		public class LogItem
		{
			/// <summary>
			/// The slot, or -1 for an operation outside the slots.
			/// </summary>
			public int Index;

			/// <summary>
			/// The kind of operation.
			/// </summary>
			public LogItemKind Kind;

			/// <summary>
			/// The virtual device that was operated: the virtual touchscreen for a finger.
			/// </summary>
			public InputDevice Device;

			/// <summary>
			/// When <c>Simulate</c> was called.
			/// </summary>
			public DateTime DateTime;

			/// <summary>
			/// The object that receives the pointer-down, predicted for the first press. <c>null</c> if there is
			/// none.
			/// </summary>
			public GameObject Target;

			/// <summary>
			/// The position of the first press or hover, in screen pixels. Zero for <see cref="LogItemKind.Device"/>.
			/// </summary>
			public Vector2 ScreenPoint;

			/// <summary>
			/// <see cref="Time.frameCount"/> when <c>Simulate</c> was called.
			/// </summary>
			public int FrameCount;

			/// <summary>
			/// The sequence number of the change that started the operation. <c>null</c> until it was processed.
			/// </summary>
			public long? Order;

			/// <summary>
			/// <see cref="Time.frameCount"/> of the input update that processed the change that started the
			/// operation. 0 until then.
			/// </summary>
			public int StartedFrameCount;

			/// <summary>
			/// <c>true</c> if the pointer moved beyond the drag threshold, so that begin-drag, drag and end-drag are
			/// delivered. It is a prediction, set when <see cref="DragStartedOrder"/> is set.
			/// </summary>
			public bool Dragged;

			/// <summary>
			/// The sequence number of the change that moved the pointer beyond the drag threshold, which is when the
			/// input module delivers begin-drag. <c>null</c> if the threshold was not exceeded, in which case
			/// <see cref="Dragged"/> is <c>false</c>.
			/// </summary>
			public long? DragStartedOrder;

			/// <summary>
			/// <see cref="Time.frameCount"/> of the input update that processed the change of
			/// <see cref="DragStartedOrder"/>. 0 if the threshold was not exceeded.
			/// </summary>
			public int DragStartedFrameCount;

			/// <summary>
			/// The object that receives begin-drag, drag and end-drag if the pointer is dragged
			/// (<see cref="PointerPrediction.PointerDrag"/>). <c>null</c> if no object under the press handles drags.
			/// </summary>
			/// <remarks>
			/// <para>
			/// It is a prospect, not a record of a drag: the input module decides it when the pointer is pressed (it
			/// is the object that receives <c>initializePotentialDrag</c> then), so it is worked out for the press
			/// when <c>Simulate</c> is called, again when the press is processed, and it stays for an operation that did
			/// not drag. <see cref="Dragged"/> tells whether the pointer was dragged.
			/// </para>
			/// </remarks>
			public GameObject DragTarget;

			/// <summary>
			/// The object that receives the drop (<see cref="PointerPrediction.DropTarget"/>) where the pointer was
			/// released. <c>null</c> if the operation did not drag, because no drop is delivered then, or if nothing
			/// accepts the drop.
			/// </summary>
			public GameObject DropTarget;

			/// <summary>
			/// <c>true</c> once the operation has ended. <see cref="EndReason"/> is valid only then.
			/// </summary>
			public bool Completed;

			/// <summary>
			/// Why the operation ended.
			/// </summary>
			public EndReason EndReason;

			/// <summary>
			/// The sequence number of the change that ended the operation, which is the release. <c>null</c> if the
			/// operation ended without such a change: <see cref="EndReason.Dropped"/>, ended by <see cref="Stop"/>, or
			/// an operation whose end changed no control (a hover that presses nothing, a <c>Set</c> of the value that
			/// a control already has).
			/// </summary>
			/// <remarks>
			/// When <see cref="Dragged"/> is <c>true</c>, this is the end of the drag (end-drag and drop) as well,
			/// because the drag ends when the pointer is released.
			/// </remarks>
			public long? CompletedOrder;

			/// <summary>
			/// <see cref="Time.frameCount"/> of the input update that processed the change of
			/// <see cref="CompletedOrder"/>, or when the operation was ended by <see cref="Stop"/>. 0 until the
			/// operation ended.
			/// </summary>
			/// <remarks>
			/// When <see cref="Dragged"/> is <c>true</c>, this is the frame of the end of the drag as well.
			/// </remarks>
			public int CompletedFrameCount;

			/// <summary>
			/// <c>true</c> if a click is predicted to be delivered when the pointer is released.
			/// </summary>
			public bool ClickExecuted;

			/// <summary>
			/// The object that receives the click, if <see cref="ClickExecuted"/> is <c>true</c>.
			/// </summary>
			public GameObject ClickTarget;

			/// <summary>
			/// The plan of the operation, for <see cref="LogItemKind.Finger"/> and <see cref="LogItemKind.Pointer"/>.
			/// What a tap, a drag and a hold last is in its steps. <c>null</c> otherwise.
			/// </summary>
			public Gesture Gesture;

			/// <summary>
			/// The plan of the operation, for <see cref="LogItemKind.Device"/>. <c>null</c> otherwise.
			/// </summary>
			public ControlSequence Sequence;

			/// <inheritdoc/>
			public override string ToString()
			{
				var head = $"{Kind} Index: {Index} FrameCount: {FrameCount}, DateTime: {DateTime}";
				if (Kind == LogItemKind.Device)
				{
					return $"{head}\nDevice: {(Device != null ? Device.name : "(none)")}";
				}

				return
					$"{head}\nScreenPoint: {ScreenPoint} GameObject: {(Target != null ? Target.name : "(none)")}";
			}
		}
	}
}
#endif
