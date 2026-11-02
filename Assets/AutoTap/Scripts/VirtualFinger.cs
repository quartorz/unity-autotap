#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

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
		readonly VirtualInputCore _core;
		readonly Touchscreen _touchscreen;
		readonly int _touchId;
		TouchState _state;
		bool _pressCalled;
		bool _releaseCalled;

		internal VirtualFinger(VirtualInputCore core, Touchscreen touchscreen, int touchId)
		{
			_core = core;
			_touchscreen = touchscreen;
			_touchId = touchId;
			Predictor = new PointerPredictor();
		}

		internal PointerPredictor Predictor { get; }

		/// <summary>
		/// The virtual touchscreen that the finger belongs to.
		/// </summary>
		internal InputDevice Device => _touchscreen;

		/// <summary>
		/// <see cref="VirtualInputCore.EndedUpdates"/> when the press was queued, or -1 before.
		/// </summary>
		internal long PressStamp { get; private set; } = -1;

		internal long ReleaseStamp { get; private set; } = -1;

		/// <summary>
		/// <c>true</c> once the press was processed by an input update.
		/// </summary>
		internal bool BeganProcessed { get; set; }

		/// <summary>
		/// <c>true</c> once <see cref="Press"/> was called.
		/// </summary>
		internal bool PressCalled => _pressCalled;

		/// <summary>
		/// <c>true</c> if the finger was pressed and nothing was queued to release it yet.
		/// </summary>
		internal bool CanRelease => _pressCalled && !_releaseCalled && State != FingerState.Released;

		/// <summary>
		/// <c>true</c> if the press was queued before an input update that did not process it.
		/// </summary>
		internal bool Dropped { get; set; }

		/// <summary>
		/// <c>true</c> if the release was queued before an input update that did not process it.
		/// </summary>
		internal bool ReleaseLost { get; set; }

		internal FingerUpdate Update;

		/// <summary>
		/// The id of the touch on the virtual touchscreen.
		/// </summary>
		public int TouchId => _touchId;

		/// <summary>
		/// The position that was queued last, in screen pixels.
		/// </summary>
		public Vector2 Position => _state.position;

		/// <summary>
		/// The progress of the finger.
		/// </summary>
		public FingerState State { get; internal set; } = FingerState.Queued;

		/// <summary>
		/// <see cref="Time.frameCount"/> of the input update that processed the press. 0 until then.
		/// </summary>
		public int PressedFrame { get; internal set; }

		/// <summary>
		/// <see cref="Time.frameCount"/> of the input update that processed the release. 0 until then.
		/// </summary>
		public int ReleasedFrame { get; internal set; }

		/// <summary>
		/// The prediction for the press, move or release that was processed last. It is refreshed at the end of each
		/// input update that processed such an event of this finger, before
		/// <see cref="IVirtualInput.InputUpdated"/> is raised.
		/// </summary>
		public PointerPrediction Prediction => Predictor.Prediction;

		/// <summary>
		/// Puts the finger down. It can be called only once, and has to be called first.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		/// <exception cref="InvalidOperationException">The finger was already pressed.</exception>
		public void Press(Vector2 position)
		{
			if (_pressCalled)
			{
				throw new InvalidOperationException("The finger was already pressed.");
			}

			_pressCalled = true;
			_state = new TouchState
			{
				touchId = _touchId,
				phase = TouchPhase.Began,
				position = position,
				pressure = 1f,
			};
			PressStamp = Queue();
		}

		/// <summary>
		/// Moves the finger while it is down.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		/// <exception cref="InvalidOperationException">The finger is not down.</exception>
		public void MoveTo(Vector2 position)
		{
			RequireDown();
			_state.phase = TouchPhase.Moved;
			_state.position = position;
			_state.delta = Vector2.zero;
			Queue();
		}

		/// <summary>
		/// Lifts the finger. The touch ends.
		/// </summary>
		/// <exception cref="InvalidOperationException">The finger is not down.</exception>
		public void Release()
		{
			End(TouchPhase.Ended);
		}

		/// <summary>
		/// Cancels the touch. For the UI this releases the pointer in the same way as <see cref="Release"/>.
		/// </summary>
		/// <exception cref="InvalidOperationException">The finger is not down.</exception>
		public void Cancel()
		{
			End(TouchPhase.Canceled);
		}

		/// <summary>
		/// Writes a control of the touch (for example <c>pressure</c> or <c>radius</c>) while the finger is down.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Only <c>pressure</c> (a <see cref="float"/>) and <c>radius</c> (a <see cref="Vector2"/>, or a
		/// <see cref="float"/> for <c>radius/x</c> and <c>radius/y</c>) can be set. <c>tapCount</c>, <c>isTap</c> and
		/// the primary touch are computed by the touchscreen, and the position is moved with <see cref="MoveTo"/>.
		/// </para>
		/// </remarks>
		/// <typeparam name="T">The value type of the control.</typeparam>
		/// <param name="controlPath">The path of the control relative to the touch, for example <c>pressure</c>.</param>
		/// <param name="value">The value to write.</param>
		/// <exception cref="ArgumentException">The control cannot be set, or <typeparamref name="T"/> does not fit it.</exception>
		/// <exception cref="InvalidOperationException">The finger is not down.</exception>
		public void Set<T>(string controlPath, T value) where T : struct
		{
			SetControl(controlPath, value);
		}

		/// <summary>
		/// Writes a control of the touch. See <see cref="Set{T}"/>.
		/// </summary>
		internal void SetControl(string controlPath, object value)
		{
			ValidateControl(controlPath, value.GetType());
			RequireDown();
			switch (controlPath)
			{
				case "pressure":
					_state.pressure = Convert.ToSingle(value);
					break;
				case "radius":
					_state.radius = (Vector2)value;
					break;
				case "radius/x":
					_state.radius.x = Convert.ToSingle(value);
					break;
				case "radius/y":
					_state.radius.y = Convert.ToSingle(value);
					break;
			}

			_state.phase = TouchPhase.Moved;
			_state.delta = Vector2.zero;
			Queue();
		}

		/// <summary>
		/// Tells whether a control of a touch can be written, and whether a value of <paramref name="valueType"/>
		/// fits it.
		/// </summary>
		/// <param name="controlPath">The path relative to the touch.</param>
		/// <param name="valueType">The value type that is going to be written.</param>
		/// <exception cref="ArgumentException">The control cannot be set, or the type does not fit it.</exception>
		internal static void ValidateControl(string controlPath, Type valueType)
		{
			Type expected;
			switch (controlPath)
			{
				case "pressure":
				case "radius/x":
				case "radius/y":
					expected = typeof(float);
					break;
				case "radius":
					expected = typeof(Vector2);
					break;
				default:
					throw new ArgumentException(
						$"The control '{controlPath}' of a finger cannot be set. Only pressure and radius can.",
						nameof(controlPath));
			}

			if (expected == typeof(float))
			{
				if (valueType != typeof(float) && valueType != typeof(double) && valueType != typeof(int))
				{
					throw new ArgumentException(
						$"The control '{controlPath}' takes a float, not a {valueType.Name}.", nameof(valueType));
				}
			}
			else if (valueType != expected)
			{
				throw new ArgumentException(
					$"The control '{controlPath}' takes a {expected.Name}, not a {valueType.Name}.", nameof(valueType));
			}
		}

		/// <summary>
		/// Reads the value that the control has in the state that was queued last.
		/// </summary>
		internal object ReadControl(string controlPath)
		{
			switch (controlPath)
			{
				case "pressure":
					return _state.pressure;
				case "radius":
					return _state.radius;
				case "radius/x":
					return _state.radius.x;
				case "radius/y":
					return _state.radius.y;
				default:
					throw new ArgumentException($"The control '{controlPath}' of a finger cannot be read.", nameof(controlPath));
			}
		}

		/// <summary>
		/// Gives the finger up without queueing anything.
		/// </summary>
		internal void Abandon()
		{
			_releaseCalled = true;
			State = FingerState.Released;
		}

		void End(TouchPhase phase)
		{
			RequireDown();
			_releaseCalled = true;
			_state.phase = phase;
			_state.pressure = 0f;
			_state.radius = Vector2.zero;
			_state.delta = Vector2.zero;
			State = FingerState.ReleaseQueued;
			ReleaseStamp = Queue();
		}

		void RequireDown()
		{
			if (!_pressCalled)
			{
				throw new InvalidOperationException("The finger was not pressed yet.");
			}

			if (_releaseCalled)
			{
				throw new InvalidOperationException("The finger was already released.");
			}

			if (State == FingerState.Released)
			{
				throw new InvalidOperationException("The finger was released by a reset of the device.");
			}
		}

		long Queue()
		{
			InputSystem.QueueStateEvent(_touchscreen, _state);
			return _core.EndedUpdates;
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
		/// How many presses were evaluated. A finger is pressed once; a button of a pointer is pressed again and
		/// again, so an operation compares this with the value at the time it started to use the button.
		/// </summary>
		internal int PressCount { get; set; }

		/// <summary>
		/// How many releases were evaluated.
		/// </summary>
		internal int ReleaseCount { get; set; }

		/// <summary>
		/// The <see cref="InputChange.Order"/> of the change at which <see cref="Dragging"/> became <c>true</c>.
		/// </summary>
		internal long? DragStartedOrder { get; set; }

		/// <summary>
		/// <see cref="Time.frameCount"/> of the change at which <see cref="Dragging"/> became <c>true</c>.
		/// </summary>
		internal int DragStartedFrame { get; set; }

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
