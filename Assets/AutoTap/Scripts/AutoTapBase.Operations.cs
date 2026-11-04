#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace UnityAutoTap
{
	public abstract partial class AutoTapBase
	{
		/// <summary>
		/// What every operation has: a <see cref="LogItem"/> that it fills in, and the way it ends.
		/// </summary>
		abstract class Operation
		{
			protected Operation(int index, LogItem item, long startSequence)
			{
				Index = index;
				Item = item;
				StartSequence = startSequence;
			}

			/// <summary>
			/// The slot, or -1 for an operation outside the slots.
			/// </summary>
			public int Index { get; }

			public LogItem Item { get; }
			public long StartSequence { get; }

			/// <summary>
			/// Why the operation ends, once it was decided to end it. <c>null</c> before that.
			/// </summary>
			public EndReason? PlannedReason { get; set; }

			/// <summary>
			/// <c>true</c> once what ends the operation (the release of the finger, the release of the buttons and
			/// the reset of the device) was queued.
			/// </summary>
			public bool EndQueued { get; set; }

			public bool Ended { get; set; }
			public bool Notified { get; set; }
			public bool PressRefreshed { get; set; }
			public bool DragReported { get; set; }
			public bool ReleaseReported { get; set; }

			/// <summary>
			/// <see cref="PointerPrediction.PressCount"/> when the operation began to use the prediction.
			/// </summary>
			public int PressBase { get; set; }

			/// <summary>
			/// <see cref="PointerPrediction.ReleaseCount"/> when the operation began to use the prediction.
			/// </summary>
			public int ReleaseBase { get; set; }

			/// <summary>
			/// <c>true</c> once the change that started the operation was processed.
			/// </summary>
			public bool Started => Item.Order.HasValue;

			/// <summary>
			/// What the input module is predicted to do for the pointer of the operation, or <c>null</c> if the
			/// operation has no pointer.
			/// </summary>
			public abstract PointerPrediction Prediction { get; }

			/// <summary>
			/// <c>true</c> once <see cref="Update(float)"/> carries out the plan: the steps after the ones that
			/// <c>Simulate</c> queued are carried out only then.
			/// </summary>
			public abstract bool PlanRuns { get; }

			/// <summary>
			/// The index of the step that the plan is at. See <see cref="LogItem.CurrentStep"/>.
			/// </summary>
			public abstract int CurrentStep { get; }

			/// <summary>
			/// The sum of the times that <see cref="Update(float)"/> passed to the plan, in seconds.
			/// </summary>
			public abstract float Elapsed { get; }

			/// <summary>
			/// The time that the plan takes, in seconds, or <c>null</c> while it is not known.
			/// </summary>
			public abstract float? PlannedDuration { get; }

			/// <summary>
			/// Where the plan puts the pointer last, or <c>null</c> while that is not known, and for an operation that
			/// has no position.
			/// </summary>
			public abstract Vector2? PlannedEndPoint { get; }

			/// <summary>
			/// Where the pointer was put last, or <c>null</c> for an operation that has no position.
			/// </summary>
			public abstract Vector2? LastPosition { get; }

			/// <summary>
			/// Queues what ends the operation, if that was not queued yet.
			/// </summary>
			public abstract void Interrupt();

			/// <summary>
			/// Starts to use the prediction: what was predicted before is not the prediction for this operation.
			/// </summary>
			protected void BindPrediction(PointerPrediction prediction)
			{
				PressBase = prediction.PressCount;
				ReleaseBase = prediction.ReleaseCount;
			}
		}

		/// <summary>
		/// One operation of a finger in a slot: what <c>Simulate(index, gesture)</c> starts.
		/// </summary>
		sealed class FingerOperation : Operation, IGestureTarget
		{
			public FingerOperation(int index, LogItem item, long startSequence, VirtualFinger finger, Gesture gesture, int stepCount)
				: base(index, item, startSequence)
			{
				Finger = finger;
				Runner = new GestureRunner(gesture, stepCount, this);
			}

			public VirtualFinger Finger { get; }
			public GestureRunner Runner { get; }

			public override PointerPrediction Prediction => Finger.Prediction;

			public override bool PlanRuns => Started;
			public override int CurrentStep => Runner.CurrentStep;
			public override float Elapsed => Runner.Elapsed;
			public override float? PlannedDuration => Runner.PlannedDuration;
			public override Vector2? PlannedEndPoint => Runner.PlannedEndPoint;
			public override Vector2? LastPosition => Finger.Position;

			public Vector2 Position => Finger.Position;

			public void MoveTo(Vector2 position)
			{
				Finger.MoveTo(position);
			}

			public object ReadControl(string path)
			{
				return Finger.ReadControl(path);
			}

			public void SetControl(string path, object value)
			{
				Finger.SetControl(path, value);
			}

			public void Press(string button)
			{
				throw new NotSupportedException();
			}

			public void Release(string button)
			{
				throw new NotSupportedException();
			}

			public void Scroll(Vector2 delta)
			{
				throw new NotSupportedException();
			}

			/// <summary>
			/// Queues the release of the finger, if it is still down or about to be and no release was queued yet.
			/// </summary>
			public override void Interrupt()
			{
				if (Finger.CanRelease)
				{
					Finger.Release();
					EndQueued = true;
				}
			}
		}

		/// <summary>
		/// An operation outside the slots: it operates one virtual device, and ends when everything it queued was
		/// processed.
		/// </summary>
		abstract class DeviceBoundOperation : Operation
		{
			protected DeviceBoundOperation(LogItem item, long startSequence, VirtualDevice device)
				: base(-1, item, startSequence)
			{
				Device = device;
			}

			public VirtualDevice Device { get; }

			/// <summary>
			/// <see cref="VirtualDevice.LastQueuedStamp"/> of the last event the operation queued, until an input
			/// update consumed it. -1 when no event is waiting.
			/// </summary>
			public long PendingStamp { get; set; } = -1;

			/// <summary>
			/// <c>true</c> once the operation queued an event.
			/// </summary>
			public bool HasQueued { get; set; }

			/// <summary>
			/// <c>true</c> once an input update processed events of the operation.
			/// </summary>
			public bool EventsProcessed { get; set; }

			/// <summary>
			/// <c>true</c> if an input update consumed events of the operation without processing them.
			/// </summary>
			public bool EventsLost { get; set; }

			/// <summary>
			/// The <see cref="InputChange.Order"/> of the last change of the operation in the input update that is being
			/// processed, or <c>null</c> if there was none.
			/// </summary>
			public long? UpdateLastOrder { get; set; }

			/// <summary>
			/// The operation advances once its first events were processed, or at once if it queued none.
			/// </summary>
			public bool CanAdvance => !HasQueued || EventsProcessed;

			public override bool PlanRuns => Started || CanAdvance;

			public abstract bool RunnerDone { get; }
			public abstract EndReason RunnerReason { get; }
			public abstract bool PressedInLastAdvance { get; }

			public abstract void AdvanceRunner(float deltaTime);

			/// <summary>
			/// Queues the release of what the operation holds, and the reset of the device.
			/// </summary>
			public abstract void QueueEnd();

			public override void Interrupt()
			{
				if (!EndQueued)
				{
					EndQueued = true;
					QueueEnd();
				}
			}

			/// <summary>
			/// Records that the operation queued an event.
			/// </summary>
			protected void NoteQueued()
			{
				Queued(Device.LastQueuedStamp);
			}

			protected void Queued(long stamp)
			{
				if (stamp >= 0)
				{
					PendingStamp = stamp;
					HasQueued = true;
				}
			}
		}

		/// <summary>
		/// One operation of a pointer device other than a touchscreen: what <c>Simulate</c> of a pointer starts.
		/// </summary>
		sealed class PointerOperation : DeviceBoundOperation, IGestureTarget
		{
			readonly List<ButtonControl> _pressed = new List<ButtonControl>();

			public PointerOperation(LogItem item, long startSequence, VirtualPointer pointer, Gesture gesture, int stepCount)
				: base(item, startSequence, pointer)
			{
				Pointer = pointer;
				Runner = new GestureRunner(gesture, stepCount, this);
			}

			public VirtualPointer Pointer { get; }
			public GestureRunner Runner { get; }

			/// <summary>
			/// Tells whether a press at a position is refused. See <see cref="ShouldNotPressOn"/>.
			/// </summary>
			public Func<Vector2, bool> Refuses { get; set; }

			/// <summary>
			/// The button whose prediction the <see cref="LogItem"/> follows: the first one that the operation pressed,
			/// if the UI handles it.
			/// </summary>
			public ButtonTrack Track { get; private set; }

			public override PointerPrediction Prediction => Track?.Predictor.Prediction;

			public override int CurrentStep => Runner.CurrentStep;
			public override float Elapsed => Runner.Elapsed;
			public override float? PlannedDuration => Runner.PlannedDuration;
			public override Vector2? PlannedEndPoint => Runner.PlannedEndPoint;
			public override Vector2? LastPosition => Pointer.Position;

			public override bool RunnerDone => Runner.Done;
			public override EndReason RunnerReason => Runner.Reason;
			public override bool PressedInLastAdvance => Runner.PressedInLastAdvance;

			public Vector2 Position => Pointer.Position;

			public override void AdvanceRunner(float deltaTime)
			{
				Runner.Advance(deltaTime);
			}

			/// <summary>
			/// Queues the event of the first step of the gesture (the pointer goes to the position, and the button
			/// is pressed if the step presses one; a pen is in range from here on), then the steps that follow and take
			/// no time, each as an event of its own.
			/// </summary>
			public void QueueInitialSteps(PointerPlan plan)
			{
				var controls = new List<InputControl> { Pointer.PositionControl };
				var values = new List<object> { plan.Start };
				var inRange = Pointer.InRange;
				if (inRange != null)
				{
					controls.Add(inRange);
					values.Add(1f);
				}

				if (plan.StartsWithPress)
				{
					var button = Pointer.ResolveButton(plan.FirstButton);
					controls.Add(button);
					values.Add(1f);
					NotePressed(button);
				}

				Queued(Pointer.QueueEvent(controls.ToArray(), values.ToArray(), reset: false));
				Runner.AdvanceAfterFirstStep(plan.StartsWithPress);
			}

			public void MoveTo(Vector2 position)
			{
				Pointer.MoveTo(position);
				NoteQueued();
			}

			public object ReadControl(string path)
			{
				return Pointer.ReadControl(Pointer.FindControl(path));
			}

			public void SetControl(string path, object value)
			{
				Pointer.Queue(ControlValue.OfObject(path, value));
				NoteQueued();
			}

			public void Press(string button)
			{
				if (Refuses != null && Refuses(Position))
				{
					Runner.Refuse();
					return;
				}

				var control = Pointer.ResolveButton(button);
				Pointer.Press(button);
				NoteQueued();
				NotePressed(control);
			}

			public void Release(string button)
			{
				var control = Pointer.ResolveButton(button);
				Pointer.Release(button);
				NoteQueued();
				_pressed.Remove(control);
			}

			public void Scroll(Vector2 delta)
			{
				Pointer.Scroll(delta);
				NoteQueued();
			}

			public override void QueueEnd()
			{
				for (var i = _pressed.Count - 1; i >= 0; --i)
				{
					Pointer.QueueEvent(new InputControl[] { _pressed[i] }, new object[] { 0f }, reset: false);
					NoteQueued();
				}

				_pressed.Clear();
				Queued(Device.QueueResetIfNeeded());
			}

			void NotePressed(ButtonControl button)
			{
				if (!_pressed.Contains(button))
				{
					_pressed.Add(button);
				}

				if (Track == null)
				{
					Track = Pointer.TrackOf(button);
					if (Track != null)
					{
						BindPrediction(Track.Predictor.Prediction);
					}
				}
			}
		}

		/// <summary>
		/// One operation of a device that has no position: what <c>Simulate</c> of a device that has no position starts.
		/// </summary>
		sealed class DeviceOperation : DeviceBoundOperation, ISequenceTarget
		{
			readonly List<ButtonControl> _pressed = new List<ButtonControl>();
			readonly ButtonControl[] _buttons;

			public DeviceOperation(
				LogItem item, long startSequence, VirtualDevice device, ControlSequence sequence,
				ButtonControl[] buttons)
				: base(item, startSequence, device)
			{
				_buttons = buttons;
				Runner = new SequenceRunner(sequence, this);
			}

			public SequenceRunner Runner { get; }

			public override PointerPrediction Prediction => null;

			public override int CurrentStep => Runner.CurrentStep;
			public override float Elapsed => Runner.Elapsed;
			public override float? PlannedDuration => Runner.PlannedDuration;
			public override Vector2? PlannedEndPoint => null;
			public override Vector2? LastPosition => null;

			public override bool RunnerDone => Runner.Done;
			public override EndReason RunnerReason => Runner.Reason;
			public override bool PressedInLastAdvance => Runner.PressedInLastAdvance;

			public override void AdvanceRunner(float deltaTime)
			{
				Runner.Advance(deltaTime);
			}

			/// <summary>
			/// Queues the steps at the start of the sequence that take no time, each as an event of its own.
			/// </summary>
			public void QueueInitialSteps()
			{
				Runner.Advance(0f);
			}

			public object ReadControl(string path)
			{
				return Device.ReadControl(Device.FindControl(path));
			}

			public void SetControl(string path, object value)
			{
				Device.Queue(ControlValue.OfObject(path, value));
				NoteQueued();
			}

			public void Press(int stepIndex)
			{
				var button = _buttons[stepIndex];
				Device.QueueEvent(new InputControl[] { button }, new object[] { 1f }, reset: false);
				NoteQueued();
				if (!_pressed.Contains(button))
				{
					_pressed.Add(button);
				}
			}

			public void Release(int stepIndex)
			{
				var button = _buttons[stepIndex];
				Device.QueueEvent(new InputControl[] { button }, new object[] { 0f }, reset: false);
				NoteQueued();
				_pressed.Remove(button);
			}

			public void Text(string text)
			{
				Queued(Device.QueueText(text));
			}

			public void Composition(string text)
			{
				Queued(Device.QueueComposition(text));
			}

			public override void QueueEnd()
			{
				for (var i = _pressed.Count - 1; i >= 0; --i)
				{
					Device.QueueEvent(new InputControl[] { _pressed[i] }, new object[] { 0f }, reset: false);
					NoteQueued();
				}

				_pressed.Clear();
				Queued(Device.QueueResetIfNeeded());
			}
		}

		void Subscribe()
		{
			if (_subscribedTo == _input)
			{
				return;
			}

			Unsubscribe();
			_input.Reader.Changed += OnReaderChanged;
			_input.InputUpdated += OnInputUpdated;
			_subscribedTo = _input;
		}

		void Unsubscribe()
		{
			if (_subscribedTo == null)
			{
				return;
			}

			_subscribedTo.Reader.Changed -= OnReaderChanged;
			_subscribedTo.InputUpdated -= OnInputUpdated;
			_subscribedTo = null;
		}

		void OnReaderChanged(InputChange change)
		{
			switch (change)
			{
				case TouchBegan began:
				{
					if (_fingerOperations.TryGetValue(began.TouchId, out var operation)
						&& !operation.Ended
						&& !operation.Started)
					{
						operation.Item.Order = began.Order;
						operation.Item.StartedFrameCount = began.Frame;
						RefreshItem(operation);
						AppendLog(operation.Item, LogEventKind.Started, began.Order);
					}

					break;
				}
				case TouchEnded _:
				case TouchCanceled _:
				{
					var touch = (TouchChange)change;
					if (_fingerOperations.TryGetValue(touch.TouchId, out var operation) && !operation.Ended)
					{
						var reason = operation.PlannedReason ?? EndReason.Interrupted;
						Complete(operation, reason, change.Order, change.Frame);
						AppendLog(operation.Item, LogEventKind.Completed, change.Order);
					}

					break;
				}
				case DeviceReset reset:
				{
					if (reset.Device is Touchscreen)
					{
						foreach (var operation in _operations)
						{
							if (operation is FingerOperation && !operation.Ended && operation.Started)
							{
								Complete(operation, EndReason.Interrupted, reset.Order, reset.Frame);
								AppendLog(operation.Item, LogEventKind.Completed, reset.Order);
							}
						}
					}
					else if (reset.Device != null
						&& _deviceOperations.TryGetValue(reset.Device, out var bound)
						&& !bound.Ended
						&& bound.HasQueued)
					{
						Complete(bound, EndReason.Interrupted, reset.Order, reset.Frame);
						AppendLog(bound.Item, LogEventKind.Completed, reset.Order);
					}

					break;
				}
				default:
				{
					if (change.Device != null
						&& _deviceOperations.TryGetValue(change.Device, out var bound)
						&& !bound.Ended)
					{
						if (!bound.Started)
						{
							bound.Item.Order = change.Order;
							bound.Item.StartedFrameCount = change.Frame;
							RefreshItem(bound);
							AppendLog(bound.Item, LogEventKind.Started, change.Order);
						}

						bound.UpdateLastOrder = change.Order;
					}

					break;
				}
			}
		}

		/// <summary>
		/// Runs at the end of each Dynamic input update, after the predictions were refreshed.
		/// </summary>
		void OnInputUpdated()
		{
			var frame = Time.frameCount;
			var anyEnded = false;
			foreach (var operation in _operations)
			{
				if (operation is FingerOperation fingerOperation)
				{
					var finger = fingerOperation.Finger;
					if (!operation.Ended)
					{
						if (finger.Dropped)
						{
							Complete(operation, EndReason.Dropped, null, frame);
							AppendEnd(operation);
							continue;
						}

						if (finger.ReleaseLost)
						{
							Complete(operation, EndReason.Interrupted, null, frame);
							AppendEnd(operation);
							continue;
						}
					}
				}
				else if (operation is DeviceBoundOperation bound && !bound.Ended)
				{
					ConsumeEvents(bound);
					if (bound.EventsLost)
					{
						Complete(bound, bound.EventsProcessed ? EndReason.Interrupted : EndReason.Dropped, null, frame);
						AppendEnd(operation);
						anyEnded = true;
						continue;
					}
				}

				var item = operation.Item;
				var prediction = operation.Prediction;
				if (prediction != null)
				{
					if (!operation.PressRefreshed && prediction.PressCount > operation.PressBase)
					{
						operation.PressRefreshed = true;
						item.Target = prediction.PointerPress;
						item.DragTarget = prediction.PointerDrag;
					}

					if (!operation.DragReported
						&& prediction.PressCount > operation.PressBase
						&& prediction.Dragging
						&& prediction.DragStartedOrder.HasValue)
					{
						operation.DragReported = true;
						item.Dragged = true;
						item.DragStartedOrder = prediction.DragStartedOrder;
						item.DragStartedFrameCount = prediction.DragStartedFrame;
						item.DragTarget = prediction.PointerDrag;
						AppendLog(item, LogEventKind.DragStarted, prediction.DragStartedOrder.Value);
					}

					if (!operation.ReleaseReported && prediction.ReleaseCount > operation.ReleaseBase)
					{
						operation.ReleaseReported = true;
						item.ClickTarget = prediction.ClickTarget;
						item.ClickExecuted = prediction.ClickTarget != null;
						item.DropTarget = prediction.DropTarget;
					}
				}

				if (operation is DeviceBoundOperation ending
					&& !ending.Ended
					&& ending.EndQueued
					&& ending.PendingStamp < 0)
				{
					Complete(operation, operation.PlannedReason ?? EndReason.Interrupted, ending.UpdateLastOrder, frame);
					if (ending.UpdateLastOrder.HasValue)
					{
						AppendLog(operation.Item, LogEventKind.Completed, ending.UpdateLastOrder.Value);
					}
					else
					{
						AppendEnd(operation);
					}

					anyEnded = true;
				}
				else if (operation.Index < 0 && operation.Ended)
				{
					anyEnded = true;
				}
			}

			foreach (var operation in _operations)
			{
				if (operation is DeviceBoundOperation bound)
				{
					bound.UpdateLastOrder = null;
				}

				if (!operation.Ended)
				{
					RefreshItem(operation);
				}
			}

			if (anyEnded)
			{
				_operations.RemoveAll(o => o.Index < 0 && o.Ended);
			}
		}

		/// <summary>
		/// Finds out what the input update that just ended did with the events the operation queued before it.
		/// </summary>
		void ConsumeEvents(DeviceBoundOperation operation)
		{
			var device = operation.Device;
			if (operation.PendingStamp < 0 || operation.PendingStamp >= device.UpdatesEnded)
			{
				return;
			}

			operation.PendingStamp = -1;
			if (device.ProcessedSinceLastUpdate)
			{
				operation.EventsProcessed = true;
			}
			else
			{
				operation.EventsLost = true;
			}
		}

		/// <summary>
		/// Adds what happened to an item that has a sequence number.
		/// </summary>
		void AppendLog(LogItem item, LogEventKind kind, long order)
		{
			Append(new LogEntry(new LogEvent(item, kind, order), 0));
		}

		/// <summary>
		/// Adds the end of an operation that has no sequence number: it ended without a processed change.
		/// </summary>
		void AppendEnd(Operation operation)
		{
			Append(new LogEntry(new LogEvent(operation.Item, LogEventKind.Completed, null), operation.StartSequence));
		}

		/// <summary>
		/// Puts an element into the log if <see cref="Update(float)"/> is running, and otherwise keeps it for the log
		/// that the next <see cref="Update(float)"/> builds. Either way it takes its place by the sequence number.
		/// </summary>
		void Append(LogEntry entry)
		{
			var list = _inUpdate ? _log : _pendingLog;

			// A stable insertion; the list is as long as the number of operations that ended in one frame, and an
			// element that belongs at the end, which is the usual one, is not moved.
			var index = list.Count;
			while (index > 0 && entry.ComesBefore(list[index - 1]))
			{
				--index;
			}

			list.Insert(index, entry);
		}

		/// <summary>
		/// Writes what is known of an operation that has not ended into its <see cref="LogItem"/>: the step it is at,
		/// where the plan puts the pointer last, and the time the plan takes.
		/// </summary>
		void RefreshItem(Operation operation)
		{
			var item = operation.Item;
			item.CurrentStep = operation.PlanRuns ? operation.CurrentStep : -1;
			item.EndScreenPoint = operation.PlannedEndPoint;
			item.Duration = operation.PlannedDuration;
		}

		/// <summary>
		/// Ends an operation: fills in the end of its <see cref="LogItem"/>.
		/// </summary>
		void Complete(Operation operation, EndReason reason, long? order, int frame)
		{
			operation.Ended = true;
			var item = operation.Item;
			item.Completed = true;
			item.EndReason = reason;
			item.CompletedOrder = order;
			item.CompletedFrameCount = frame;
			item.EndScreenPoint = operation.LastPosition;
			item.Duration = operation.Elapsed;
			item.CurrentStep = reason == EndReason.Finished || operation.PlanRuns ? operation.CurrentStep : -1;
			if (operation is FingerOperation fingerOperation)
			{
				_fingerOperations.Remove(fingerOperation.Finger.TouchId);
				HideMarker(operation.Index);
			}
			else if (operation is DeviceBoundOperation bound)
			{
				// Nothing reports the end of an operation outside the slots, so it is over for good.
				bound.Notified = true;
				_deviceOperations.Remove(bound.Device.Device);
				if (bound is PointerOperation pointerOperation)
				{
					HidePointerMarker(pointerOperation.Pointer);
				}
			}
		}

		/// <summary>
		/// Reports the operations of the slots that ended, in the order the ends were processed, and empties
		/// their slots.
		/// </summary>
		void NotifyCompleted(bool inStop = false)
		{
			_scratch.Clear();
			foreach (var operation in _operations)
			{
				if (operation.Index >= 0 && operation.Ended && !operation.Notified)
				{
					_scratch.Add(operation);
				}
			}

			if (_scratch.Count == 0)
			{
				return;
			}

			_scratch.Sort((a, b) =>
			{
				var order = a.Item.CompletedOrder.GetValueOrDefault(long.MaxValue)
					.CompareTo(b.Item.CompletedOrder.GetValueOrDefault(long.MaxValue));
				return order != 0 ? order : a.StartSequence.CompareTo(b.StartSequence);
			});

			var ended = _scratch.ToArray();
			_scratch.Clear();
			foreach (var operation in ended)
			{
				if (operation.Notified)
				{
					continue;
				}

				operation.Notified = true;
				_slots[operation.Index] = null;
				_operations.Remove(operation);
				OnTapCompleted(operation.Index, operation.Item);
				if (!inStop && !Enabled)
				{
					return;
				}
			}
		}

		/// <summary>
		/// Advances the plan of a running finger by one frame.
		/// </summary>
		void Advance(FingerOperation operation, float deltaTime)
		{
			if (operation.Ended || operation.EndQueued || !operation.Started)
			{
				return;
			}

			operation.Runner.Advance(deltaTime);
			RefreshItem(operation);
			ShowMarker(operation.Index, operation.Finger.Position);
			if (operation.Runner.Done)
			{
				operation.PlannedReason = operation.Runner.Reason;
				operation.EndQueued = true;
				operation.Finger.Release();
			}
		}

		/// <summary>
		/// Advances the plans of the running operations outside the slots by one frame, in the order they were started.
		/// </summary>
		void AdvanceBoundOperations(float deltaTime)
		{
			_boundScratch.Clear();
			foreach (var operation in _operations)
			{
				if (operation is DeviceBoundOperation bound && !bound.Ended)
				{
					_boundScratch.Add(bound);
				}
			}

			foreach (var bound in _boundScratch)
			{
				if (bound.Ended || bound.EndQueued || !bound.CanAdvance)
				{
					continue;
				}

				bound.AdvanceRunner(deltaTime);
				RefreshItem(bound);
				if (bound is PointerOperation pointerOperation)
				{
					ShowPointerMarker(pointerOperation.Pointer, pointerOperation.Position);
				}

				// What was pressed in this frame is released in a later one, never by the same input update.
				if (bound.RunnerDone && !bound.PressedInLastAdvance)
				{
					bound.PlannedReason = bound.RunnerReason;
					bound.EndQueued = true;
					bound.QueueEnd();
				}

				if (!Enabled)
				{
					break;
				}
			}

			_boundScratch.Clear();
		}

		void ShowMarker(int index, Vector2 position)
		{
			var marker = _markers != null ? _markers[index] : null;
			if (marker == null)
			{
				return;
			}

			marker.gameObject.SetActive(true);
			var scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
			marker.rectTransform.anchoredPosition = new Vector2(position.x / scaleFactor, position.y / scaleFactor);
		}

		void HideMarker(int index)
		{
			var marker = _markers != null ? _markers[index] : null;
			if (marker != null)
			{
				marker.gameObject.SetActive(false);
			}
		}

		void HideMarkers()
		{
			if (_markers != null)
			{
				for (var i = 0; i < _markers.Length; ++i)
				{
					HideMarker(i);
				}
			}

			foreach (var marker in _pointerMarkers.Values)
			{
				if (marker != null)
				{
					marker.gameObject.SetActive(false);
				}
			}
		}

		void ShowPointerMarker(VirtualPointer pointer, Vector2 position)
		{
			if (_canvas == null)
			{
				return;
			}

			if (!_pointerMarkers.TryGetValue(pointer, out var marker) || marker == null)
			{
				var markerGameObject = new GameObject($"Marker {pointer.Device.name}");
				markerGameObject.transform.SetParent(_canvas.transform, false);
				markerGameObject.transform.localPosition = Vector3.zero;
				marker = markerGameObject.AddComponent<Image>();
				marker.raycastTarget = false;
				marker.sprite = _markerSprite;
				marker.SetNativeSize();
				marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = Vector2.zero;
				_pointerMarkers[pointer] = marker;
			}

			marker.gameObject.SetActive(true);
			var scaleFactor = _canvas.scaleFactor;
			marker.rectTransform.anchoredPosition = new Vector2(position.x / scaleFactor, position.y / scaleFactor);
		}

		void HidePointerMarker(VirtualPointer pointer)
		{
			if (_pointerMarkers.TryGetValue(pointer, out var marker) && marker != null)
			{
				marker.gameObject.SetActive(false);
			}
		}

		void DestroyMarkers()
		{
			if (_markers != null)
			{
				foreach (var marker in _markers)
				{
					DestroyMarker(marker);
				}

				_markers = null;
			}

			foreach (var marker in _pointerMarkers.Values)
			{
				DestroyMarker(marker);
			}

			_pointerMarkers.Clear();
		}

		static void DestroyMarker(Image marker)
		{
			if (marker == null)
			{
				return;
			}

			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(marker.gameObject);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(marker.gameObject);
			}
		}
	}
}
#endif
