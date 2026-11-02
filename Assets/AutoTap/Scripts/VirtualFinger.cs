#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityAutoTap
{
	/// <summary>
	/// The progress of a <see cref="VirtualFinger"/>.
	/// </summary>
	public enum FingerState
	{
		/// <summary>
		/// <see cref="VirtualFinger.Press"/> was called, but the press has not been processed yet.
		/// </summary>
		Queued,

		/// <summary>
		/// The press was processed and the finger is down.
		/// </summary>
		Pressed,

		/// <summary>
		/// <see cref="VirtualFinger.Release"/> or <see cref="VirtualFinger.Cancel"/> was called, but it has not been
		/// processed yet.
		/// </summary>
		ReleaseQueued,

		/// <summary>
		/// The release was processed. The finger cannot be used any more.
		/// </summary>
		Released,
	}

	/// <summary>
	/// One touch of the virtual touchscreen. Obtained from <see cref="IVirtualInput.NewFinger"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each call that changes the touch queues one event. Events are processed in the order they were queued, at the
	/// next input update. A press and a release may be queued for the same input update.
	/// </para>
	/// <para>
	/// A finger is used for one press only: its <see cref="TouchId"/> is never reused for another finger.
	/// </para>
	/// </remarks>
	public sealed class VirtualFinger
	{
		internal VirtualFinger()
		{
		}

		/// <summary>
		/// The id of the touch on the virtual touchscreen.
		/// </summary>
		public int TouchId => throw new NotImplementedException();

		/// <summary>
		/// The position that was queued last, in screen pixels.
		/// </summary>
		public Vector2 Position => throw new NotImplementedException();

		/// <summary>
		/// The progress of the finger.
		/// </summary>
		public FingerState State => throw new NotImplementedException();

		/// <summary>
		/// <see cref="Time.frameCount"/> of the input update that processed the press. 0 until then.
		/// </summary>
		public int PressedFrame => throw new NotImplementedException();

		/// <summary>
		/// <see cref="Time.frameCount"/> of the input update that processed the release. 0 until then.
		/// </summary>
		public int ReleasedFrame => throw new NotImplementedException();

		/// <summary>
		/// The prediction for the press, move or release that was processed last. It is refreshed at the end of each
		/// input update that processed such an event of this finger, before
		/// <see cref="IVirtualInput.InputUpdated"/> is raised.
		/// </summary>
		public PointerPrediction Prediction => throw new NotImplementedException();

		/// <summary>
		/// Puts the finger down. It can be called only once, and has to be called first.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		public void Press(Vector2 position)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Moves the finger while it is down.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		public void MoveTo(Vector2 position)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Lifts the finger. The touch ends.
		/// </summary>
		public void Release()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Cancels the touch. For the UI this releases the pointer in the same way as <see cref="Release"/>.
		/// </summary>
		public void Cancel()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Writes a control of the touch (for example <c>pressure</c> or <c>radius</c>) while the finger is down.
		/// </summary>
		/// <remarks>
		/// <c>tapCount</c>, <c>isTap</c> and the primary touch are computed by the touchscreen and cannot be set.
		/// </remarks>
		/// <typeparam name="T">The value type of the control.</typeparam>
		/// <param name="controlPath">The path of the control relative to the touch, for example <c>pressure</c>.</param>
		/// <param name="value">The value to write.</param>
		public void Set<T>(string controlPath, T value) where T : struct
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// What the event system is going to do for a pointer, worked out right after the input update that processed
	/// a press, a move or a release, before the event system delivers anything.
	/// </summary>
	/// <remarks>
	/// The values are predictions that follow the rules of the input module. They are not what was actually
	/// delivered, and a handler that changes the UI while it is called can make them differ.
	/// </remarks>
	public sealed class PointerPrediction
	{
		internal PointerPrediction()
		{
		}

		/// <summary>
		/// The raycast results at the pointer, nearest first. Empty if nothing was hit.
		/// </summary>
		public IReadOnlyList<RaycastResult> PressHits { get; internal set; } = Array.Empty<RaycastResult>();

		/// <summary>
		/// The object that receives the pointer-down: the nearest handler of it, or, if there is none, the handler
		/// of the click. <c>null</c> if there is none.
		/// </summary>
		public GameObject PointerPress { get; internal set; }

		/// <summary>
		/// The object that receives the click if the pointer is released on it.
		/// <c>null</c> if there is none.
		/// </summary>
		public GameObject PointerClick { get; internal set; }

		/// <summary>
		/// The object that receives begin-drag, drag and end-drag. <c>null</c> if there is none.
		/// </summary>
		public GameObject PointerDrag { get; internal set; }

		/// <summary>
		/// <c>true</c> if the pointer has moved beyond the drag threshold, so that begin-drag is delivered.
		/// </summary>
		public bool Dragging { get; internal set; }

		/// <summary>
		/// The object that receives the click when the pointer is released. <c>null</c> if no click is delivered.
		/// </summary>
		public GameObject ClickTarget { get; internal set; }

		/// <summary>
		/// The object that receives the drop (<see cref="IDropHandler"/>) when the pointer is released at its current
		/// position. <c>null</c> if the pointer is not dragging or nothing accepts the drop.
		/// </summary>
		public GameObject DropTarget { get; internal set; }
	}
}
#endif
