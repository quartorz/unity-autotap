#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityAutoTap
{
	public abstract partial class AutoTapBase
	{
		/// <summary>
		/// One operation of a finger in a slot: what <c>Fire(index, ...)</c> starts.
		/// </summary>
		sealed class FingerOperation : GestureTarget
		{
			public FingerOperation(int index, LogItem item, long fireSequence, VirtualFinger finger, Gesture gesture, int stepCount)
			{
				Index = index;
				Item = item;
				FireSequence = fireSequence;
				Finger = finger;
				Runner = new GestureRunner(gesture, stepCount, this);
			}

			public int Index { get; }
			public LogItem Item { get; }
			public long FireSequence { get; }
			public VirtualFinger Finger { get; }
			public GestureRunner Runner { get; }

			/// <summary>
			/// Why the operation ends, once it was decided to release the finger. <c>null</c> before that.
			/// </summary>
			public EndReason? PlannedReason { get; set; }

			public bool ReleaseQueued { get; set; }
			public bool Ended { get; set; }
			public bool Notified { get; set; }
			public bool PressRefreshed { get; set; }
			public bool DragReported { get; set; }
			public bool ReleaseReported { get; set; }

			/// <summary>
			/// <c>true</c> once the press was processed by an input update.
			/// </summary>
			public bool Started => Item.Order.HasValue;

			public override Vector2 Position => Finger.Position;

			public override void MoveTo(Vector2 position)
			{
				Finger.MoveTo(position);
			}

			public override object ReadControl(string path)
			{
				return Finger.ReadControl(path);
			}

			public override void SetControl(string path, object value)
			{
				Finger.SetControl(path, value);
			}

			/// <summary>
			/// Queues the release of the finger, if it is still down or about to be and no release was queued yet.
			/// </summary>
			public void Interrupt()
			{
				if (Finger.CanRelease)
				{
					Finger.Release();
					ReleaseQueued = true;
				}
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
					if (!(reset.Device is Touchscreen))
					{
						break;
					}

					foreach (var operation in _operations)
					{
						if (!operation.Ended && operation.Started)
						{
							Complete(operation, EndReason.Interrupted, reset.Order, reset.Frame);
							AppendLog(operation.Item, LogEventKind.Completed, reset.Order);
						}
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
			if (_logIsStale)
			{
				_log.Clear();
				_logIsStale = false;
			}

			var frame = Time.frameCount;
			_logFrame = frame;
			foreach (var operation in _operations)
			{
				var finger = operation.Finger;
				if (!operation.Ended)
				{
					if (finger.Dropped)
					{
						Complete(operation, EndReason.Dropped, null, frame);
						_tail.Add(new KeyValuePair<long, LogItem>(operation.FireSequence, operation.Item));
						continue;
					}

					if (finger.ReleaseLost)
					{
						Complete(operation, EndReason.Interrupted, null, frame);
						_tail.Add(new KeyValuePair<long, LogItem>(operation.FireSequence, operation.Item));
						continue;
					}
				}

				var item = operation.Item;
				var prediction = finger.Prediction;
				if (!operation.PressRefreshed && prediction.PressEvaluated)
				{
					operation.PressRefreshed = true;
					item.Target = prediction.PointerPress;
					item.DragTarget = prediction.PointerDrag;
				}

				if (!operation.DragReported && prediction.Dragging && prediction.DragStartedOrder.HasValue)
				{
					operation.DragReported = true;
					item.Dragged = true;
					item.DragStartedOrder = prediction.DragStartedOrder;
					item.DragStartedFrameCount = prediction.DragStartedFrame;
					item.DragTarget = prediction.PointerDrag;
					AppendLog(item, LogEventKind.DragStarted, prediction.DragStartedOrder.Value);
				}

				if (!operation.ReleaseReported && prediction.ReleaseEvaluated)
				{
					operation.ReleaseReported = true;
					item.ClickTarget = prediction.ClickTarget;
					item.ClickExecuted = prediction.ClickTarget != null;
					item.DropTarget = prediction.DropTarget;
				}
			}

			SortLogByOrder();

			// Operations that ended without a processed change come after the ones that have an order, in the
			// order they were fired.
			if (_tail.Count > 0)
			{
				_tail.Sort((a, b) => a.Key.CompareTo(b.Key));
				foreach (var pair in _tail)
				{
					_log.Add(new LogEvent(pair.Value, LogEventKind.Completed, null));
				}

				_tail.Clear();
			}

			_logIsStale = true;
		}

		void AppendLog(LogItem item, LogEventKind kind, long order)
		{
			if (_logIsStale)
			{
				_log.Clear();
				_logIsStale = false;
			}

			_log.Add(new LogEvent(item, kind, order));
			_logFrame = Time.frameCount;
		}

		void SortLogByOrder()
		{
			// A stable insertion sort; the list is as long as the number of operations of one input update.
			for (var i = 1; i < _log.Count; ++i)
			{
				var current = _log[i];
				var j = i - 1;
				while (j >= 0 && _log[j].Order.GetValueOrDefault() > current.Order.GetValueOrDefault())
				{
					_log[j + 1] = _log[j];
					--j;
				}

				_log[j + 1] = current;
			}
		}

		/// <summary>
		/// Ends an operation: fills in the end of its <see cref="LogItem"/>.
		/// </summary>
		void Complete(FingerOperation operation, EndReason reason, long? order, int frame)
		{
			operation.Ended = true;
			var item = operation.Item;
			item.Completed = true;
			item.EndReason = reason;
			item.CompletedOrder = order;
			item.CompletedFrameCount = frame;
			_fingerOperations.Remove(operation.Finger.TouchId);
			HideMarker(operation.Index);
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
				if (operation.Ended && !operation.Notified)
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
				return order != 0 ? order : a.FireSequence.CompareTo(b.FireSequence);
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
		/// Advances the plan of a running operation by one frame.
		/// </summary>
		void Advance(FingerOperation operation, float deltaTime)
		{
			if (operation.Ended || operation.ReleaseQueued || !operation.Started)
			{
				return;
			}

			operation.Runner.Advance(deltaTime);
			ShowMarker(operation.Index, operation.Finger.Position);
			if (operation.Runner.Done)
			{
				operation.PlannedReason = operation.Runner.Reason;
				operation.ReleaseQueued = true;
				operation.Finger.Release();
			}
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
			if (_markers == null)
			{
				return;
			}

			for (var i = 0; i < _markers.Length; ++i)
			{
				HideMarker(i);
			}
		}

		void DestroyMarkers()
		{
			if (_markers == null)
			{
				return;
			}

			foreach (var marker in _markers)
			{
				if (marker == null)
				{
					continue;
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

			_markers = null;
		}
	}
}
#endif
