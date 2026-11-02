#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UnityAutoTap
{
	/// <summary>
	/// What the prediction of one button of a pointer needs between two input updates.
	/// </summary>
	internal sealed class ButtonTrack
	{
		public ButtonTrack(string path)
		{
			Path = path;
		}

		/// <summary>
		/// The path of the button relative to the device.
		/// </summary>
		public string Path { get; }

		public PointerPredictor Predictor { get; } = new PointerPredictor();

		/// <summary>
		/// What happened to the button in the input update that is being processed.
		/// </summary>
		public FingerUpdate Update;

		/// <summary>
		/// The position of the pointer at the end of the previous input update that moved it.
		/// </summary>
		public Vector2 LastPosition;
	}

	/// <summary>
	/// A virtual <see cref="Pointer"/> device other than a touchscreen: a <see cref="Mouse"/>, a <see cref="Pen"/> or
	/// any other layout derived from <see cref="Pointer"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The position of the pointer stays where it was left, and the UI under it keeps being hovered.
	/// </para>
	/// <para>
	/// Each call queues exactly one event. A call that moves the pointer also writes the <c>delta</c> of the move, and
	/// <see cref="Scroll"/> writes the <c>scroll</c> of the event; both are zero in every other event.
	/// </para>
	/// </remarks>
	public sealed class VirtualPointer : VirtualDevice
	{
		static readonly PointerPrediction s_NoPrediction = new PointerPrediction();

		readonly Pointer _pointer;
		readonly ButtonControl _primary;
		readonly InputControl<Vector2> _scroll;
		readonly Dictionary<string, ButtonTrack> _tracks = new Dictionary<string, ButtonTrack>();

		internal VirtualPointer(VirtualInputCore core, Pointer pointer) : base(core, pointer)
		{
			_pointer = pointer;
			_primary = InputControlPath.TryFindChild(pointer, "{PrimaryAction}") as ButtonControl;
			_scroll = pointer is Mouse mouse ? mouse.scroll : pointer.TryGetChildControl("scroll") as InputControl<Vector2>;

			// The UI handles the primary button, and the right and middle buttons of a mouse. The other buttons never
			// reach it, so nothing is predicted for them.
			if (_primary != null)
			{
				AddTrack(_primary);
			}

			if (pointer is Mouse m)
			{
				AddTrack(m.rightButton);
				AddTrack(m.middleButton);
			}
		}

		void AddTrack(ButtonControl button)
		{
			var path = InputChangeReader.RelativePath(Device, button);
			_tracks[path] = new ButtonTrack(path);
		}

		/// <summary>
		/// Moves the pointer to <paramref name="position"/>. While a button is pressed this is a drag; otherwise it
		/// is a hover.
		/// </summary>
		/// <param name="position">The position in screen pixels.</param>
		public void MoveTo(Vector2 position)
		{
			QueueEvent(new InputControl[] { _pointer.position }, new object[] { position }, reset: false);
		}

		/// <summary>
		/// Presses a button.
		/// </summary>
		/// <param name="button">
		/// The path of a button control relative to the device, for example <c>rightButton</c> of a mouse or
		/// <c>eraser</c> of a pen. <c>null</c> selects the control whose usage is
		/// <see cref="CommonUsages.PrimaryAction"/> (the left button of a mouse, the tip of a pen).
		/// </param>
		/// <exception cref="ArgumentException">The device has no such button.</exception>
		public void Press(string button = null)
		{
			QueueEvent(new InputControl[] { ResolveButton(button) }, new object[] { 1f }, reset: false);
		}

		/// <summary>
		/// Releases a button.
		/// </summary>
		/// <param name="button">The button to release. See <see cref="Press"/>.</param>
		/// <exception cref="ArgumentException">The device has no such button.</exception>
		public void Release(string button = null)
		{
			QueueEvent(new InputControl[] { ResolveButton(button) }, new object[] { 0f }, reset: false);
		}

		/// <summary>
		/// Scrolls.
		/// </summary>
		/// <param name="delta">The amount of one event, in the unit of the <c>scroll</c> control of the device.</param>
		/// <exception cref="InvalidOperationException">The device has no <c>scroll</c> control.</exception>
		public void Scroll(Vector2 delta)
		{
			QueueEvent(new InputControl[] { RequireScroll() }, new object[] { delta }, reset: false);
		}

		/// <summary>
		/// The prediction for the primary button, for the press, move or release that was processed last. It is
		/// refreshed at the end of each input update that processed such an event of this pointer, before
		/// <see cref="IVirtualInput.InputUpdated"/> is raised.
		/// </summary>
		public PointerPrediction Prediction => GetPrediction();

		/// <summary>
		/// Gives the prediction for a button, for the press, move or release that was processed last. See
		/// <see cref="Prediction"/>.
		/// </summary>
		/// <param name="button">
		/// The button, as for <see cref="Press"/>. Predictions are kept for the primary button and for the right and
		/// middle buttons of a mouse, which are the ones that the UI handles.
		/// </param>
		/// <returns>
		/// The prediction, or an empty one for a button that the UI does not handle.
		/// </returns>
		/// <exception cref="ArgumentException">The device has no such button.</exception>
		public PointerPrediction GetPrediction(string button = null)
		{
			var track = TrackOf(ResolveButton(button));
			return track != null ? track.Predictor.Prediction : s_NoPrediction;
		}

		/// <summary>
		/// The position that was queued last, in screen pixels.
		/// </summary>
		public Vector2 Position => ReadControl(_pointer.position);

		internal IEnumerable<ButtonTrack> Tracks => _tracks.Values;

		/// <summary>
		/// <c>true</c> if the device has a scroll control.
		/// </summary>
		internal bool CanScroll => _scroll != null;

		internal InputControl RequireScroll()
		{
			if (_scroll == null)
			{
				throw new InvalidOperationException($"The device '{Device.name}' has no scroll control.");
			}

			return _scroll;
		}

		/// <summary>
		/// The control that <c>null</c> stands for as a button, or <c>null</c> if the device has none.
		/// </summary>
		internal ButtonControl PrimaryButton => _primary;

		/// <summary>
		/// The control of <c>inRange</c>, or <c>null</c> if the device has none (a pen has one).
		/// </summary>
		internal ButtonControl InRange => Device.TryGetChildControl("inRange") as ButtonControl;

		internal InputControl PositionControl => _pointer.position;

		/// <summary>
		/// Finds the button.
		/// </summary>
		/// <param name="button">The path of the button relative to the device, or <c>null</c> for the primary one.</param>
		/// <exception cref="ArgumentException">The device has no such button.</exception>
		internal ButtonControl ResolveButton(string button)
		{
			if (button == null)
			{
				if (_primary == null)
				{
					throw new ArgumentException($"The device '{Device.name}' has no control whose usage is PrimaryAction.");
				}

				return _primary;
			}

			var control = FindControl(button);
			if (!(control is ButtonControl buttonControl))
			{
				throw new ArgumentException($"The control '{button}' of '{Device.name}' is not a button.", nameof(button));
			}

			CheckWritable(buttonControl);
			return buttonControl;
		}

		/// <summary>
		/// Gives the track that predicts what the UI does for the button, or <c>null</c> if the UI does not handle it.
		/// </summary>
		internal ButtonTrack TrackOf(ButtonControl button)
		{
			return _tracks.TryGetValue(InputChangeReader.RelativePath(Device, button), out var track) ? track : null;
		}

		/// <summary>
		/// Checks that a gesture can be carried out by this pointer.
		/// </summary>
		/// <exception cref="ArgumentException">A control of the gesture does not exist or does not fit the step.</exception>
		internal void ValidateGesture(Gesture gesture)
		{
			foreach (var step in gesture.Steps)
			{
				switch (step.Kind)
				{
					case Gesture.StepKind.Press:
					case Gesture.StepKind.Release:
						ResolveButton(step.Button);
						break;
					case Gesture.StepKind.Scroll:
						if (_scroll == null)
						{
							throw new ArgumentException($"The device '{Device.name}' has no scroll control.");
						}

						break;
					case Gesture.StepKind.Set:
					{
						var control = ResolveForWrite(step.Path, step.ValueType);
						Interpolation.ConvertTo(step.Value, control.valueType);
						break;
					}
				}
			}
		}

		/// <summary>
		/// Follows a change of this pointer for the predictions. Called while the input update is processed.
		/// </summary>
		internal void OnChange(InputChange change, VirtualInputCore core)
		{
			switch (change)
			{
				case ValueChanged value when value.ControlPath == "position":
				{
					var position = CurrentPosition();
					foreach (var track in _tracks.Values)
					{
						core.Touch(track);
						track.Update.LastPosition = position;
						track.Update.LastOrder = change.Order;
						track.Update.LastFrame = change.Frame;
						if (track.Update.FirstBeyondOrder < 0 && track.Predictor.IsBeyondDragThreshold(position))
						{
							track.Update.FirstBeyondOrder = change.Order;
							track.Update.FirstBeyondFrame = change.Frame;
						}
					}

					break;
				}
				case ButtonDown down when _tracks.TryGetValue(down.ControlPath, out var track):
				{
					core.Touch(track);
					track.Update.Pressed = true;
					track.Update.LastPosition = CurrentPosition();
					track.Update.LastOrder = change.Order;
					track.Update.LastFrame = change.Frame;
					break;
				}
				case ButtonUp up when _tracks.TryGetValue(up.ControlPath, out var track):
				{
					core.Touch(track);
					track.Update.Released = true;
					track.Update.LastPosition = CurrentPosition();
					track.Update.LastOrder = change.Order;
					track.Update.LastFrame = change.Frame;
					break;
				}
				case DeviceReset reset:
				{
					foreach (var track in _tracks.Values)
					{
						if (!track.Predictor.Active)
						{
							continue;
						}

						core.Touch(track);
						track.Update.Released = true;
						track.Update.LastOrder = reset.Order;
						track.Update.LastFrame = reset.Frame;
					}

					break;
				}
			}
		}

		/// <summary>
		/// The position that the device has now, which is that of the event that is being processed.
		/// </summary>
		Vector2 CurrentPosition()
		{
			return _pointer.position.ReadUnprocessedValue();
		}
	}
}
#endif
