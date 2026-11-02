#if ENABLE_AUTOTAP
using System;
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
			/// A finger that is pressed and released at one position (<c>Fire(index, position, duration)</c>).
			/// </summary>
			Tap,

			/// <summary>
			/// A finger that is moved in a straight line (<c>Fire(index, from, to, duration)</c>).
			/// </summary>
			Drag,

			/// <summary>
			/// A finger that follows a <see cref="UnityAutoTap.Gesture"/> (<c>Fire(index, gesture)</c>).
			/// </summary>
			Gesture,

			/// <summary>
			/// A pointer other than a finger that follows a <see cref="UnityAutoTap.Gesture"/>
			/// (<see cref="FirePointer"/>).
			/// </summary>
			Pointer,

			/// <summary>
			/// A device that follows a <see cref="ControlSequence"/> (<see cref="FireDevice"/>).
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
			/// <see cref="Stop"/> ended the operation, or the Input System reset the device while the operation was
			/// running.
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
		/// The record of one operation. It is returned by <c>Fire</c> as soon as the operation was accepted, and is
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
			/// When <c>Fire</c> was called.
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
			/// The destination of the move of <see cref="LogItemKind.Drag"/>; for <see cref="LogItemKind.Gesture"/>
			/// and <see cref="LogItemKind.Pointer"/> the destination of the last move. <c>null</c> if the operation
			/// does not move.
			/// </summary>
			public Vector2? DragTo;

			/// <summary>
			/// <see cref="Time.frameCount"/> when <c>Fire</c> was called.
			/// </summary>
			public int FrameCount;

			/// <summary>
			/// The time in seconds given to <c>Fire</c>. <see cref="float.NaN"/> when the operation was given as a
			/// <see cref="UnityAutoTap.Gesture"/> or a <see cref="ControlSequence"/>.
			/// </summary>
			public float Duration;

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
			/// The object that receives begin-drag, drag and end-drag (<see cref="PointerPrediction.PointerDrag"/>).
			/// <c>null</c> if the operation does not drag.
			/// </summary>
			public GameObject DragTarget;

			/// <summary>
			/// The object that receives the drop (<see cref="PointerPrediction.DropTarget"/>) where the pointer was
			/// released. <c>null</c> if the operation did not drag, or nothing accepts the drop.
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
			/// operation ended without such a change (<see cref="EndReason.Dropped"/>, or ended by <see cref="Stop"/>).
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
			/// The plan of the operation, for <see cref="LogItemKind.Gesture"/> and <see cref="LogItemKind.Pointer"/>.
			/// <c>null</c> otherwise.
			/// </summary>
			public Gesture Gesture;

			/// <summary>
			/// The plan of the operation, for <see cref="LogItemKind.Device"/>. <c>null</c> otherwise.
			/// </summary>
			public ControlSequence Sequence;

			/// <inheritdoc/>
			public override string ToString()
			{
				var head = $"{Kind} Index: {Index} FrameCount: {FrameCount}, Duration: {Duration}, DateTime: {DateTime}";
				if (Kind == LogItemKind.Device)
				{
					return $"{head}\nDevice: {(Device != null ? Device.name : "(none)")}";
				}

				return
					$"{head}\nScreenPoint: {ScreenPoint}{(DragTo.HasValue ? $" → {DragTo}" : "")} GameObject: {(Target != null ? Target.name : "(none)")}";
			}
		}
	}
}
#endif
