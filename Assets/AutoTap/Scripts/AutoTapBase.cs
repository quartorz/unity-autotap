#if !DISABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AutoTap
{
	public abstract class AutoTapBase : IDisposable
	{
		public static AutoTapBase Current { get; private set; }

		public class LogItem
		{
			internal bool Active;
			public int Index;
			public DateTime DateTime;
			public GameObject Target;
			public Vector2 ScreenPoint;
			public Vector2? DragTo;
			public int FrameCount;
			public float Duration;

			public override string ToString()
			{
				return
					$@"{(DragTo.HasValue ? "Drag" : "Tap")} Index: {Index} FrameCount: {FrameCount}, Duration: {Duration}, DateTime: {DateTime}
ScreenPoint: {ScreenPoint}{(DragTo.HasValue ? $" → {DragTo}" : "")} GameObject: {(Target != null ? Target.name : "(none)")}";
			}
		}

		protected internal static class ComponentFinder<T> where T : Component
		{
			static List<T> _list = new List<T>();

			public static IEnumerable<T> GetComponents(Component root, bool includeInactive = false)
			{
				if (root == null)
				{
					yield break;
				}

				try
				{
					root.GetComponentsInChildren(includeInactive, _list);
					for (var i = 0; i < _list.Count; ++i)
					{
						yield return _list[i];
					}
				}
				finally
				{
					_list.Clear();
				}
			}

			public static T GetComponent(Component root, string name, bool includeInactive = false)
			{
				foreach (var x in GetComponents(root, includeInactive))
				{
					if (x.name == name)
					{
						return x;
					}
				}

				return null;
			}
		}

		class Tap : IDisposable
		{
			public bool Active => _marker.gameObject.activeSelf;
			public GameObject PointerPress => _eventData.pointerPress;
			Image _marker;
			PointerEventData _eventData;
			Vector2 _startPosition;
			Vector2 _endPosition;
			float _duration;
			float _currentTime;

			readonly List<RaycastResult> _list;
			readonly AutoTapBase _owner;

			public Tap(Image marker, int id, AutoTapBase owner, List<RaycastResult> raycastResults)
			{
				_marker = marker;
				_eventData = new PointerEventData(EventSystem.current)
				{
					pointerId = id,
					useDragThreshold = true
				};
				_owner = owner;
				_list = raycastResults;
			}

			public void Dispose()
			{
				UnityEngine.Object.Destroy(_marker.gameObject);
				_marker = null;
				_eventData = null;
			}

			public bool Start(Vector2 startPosition, Vector2 endPosition, float duration)
			{
				_eventData.position = startPosition;
				_startPosition = startPosition;
				_endPosition = endPosition;
				_duration = duration;

				var o = Raycast();
				if (o != null)
				{
					PointerDown(o);
					return true;
				}

				return false;
			}

			public void Stop()
			{
				if (!Active)
				{
					return;
				}

				var o = Raycast();
				PointerUp(o);
			}

			public void Update(float deltaTime)
			{
				var eventSystem = EventSystem.current;
				if (eventSystem == null || !eventSystem.enabled)
				{
					PointerUp(null);
				}

				if (_currentTime < _duration)
				{
					_currentTime += deltaTime;
					var t = _duration <= 0 ? 1 : Mathf.InverseLerp(0, _duration, _currentTime);
					var newPosition = Vector2.Lerp(_startPosition, _endPosition, t);

					var scaleFactor = _marker.canvas.scaleFactor;
					_marker.rectTransform.anchoredPosition =
						new Vector2(newPosition.x / scaleFactor, newPosition.y / scaleFactor);

					_eventData.delta = newPosition - _eventData.position;
					_eventData.position = newPosition;

					var target = Raycast();

					if (t == 1)
					{
						PointerUp(target);
					}
					else if (_eventData.dragging)
					{
						Drag(target);
					}
					else
					{
						if (eventSystem != null
							&& eventSystem.enabled
							&& (newPosition - _startPosition).sqrMagnitude >= eventSystem.pixelDragThreshold)
						{
							BeginDrag();
						}
					}
				}
			}

			GameObject Raycast()
			{
				var eventSystem = EventSystem.current;
				if (eventSystem == null)
				{
					return null;
				}

				eventSystem.RaycastAll(_eventData, _list);
				foreach (var r in _list)
				{
					var t = r.gameObject.transform;
					if (!_owner.ToBeIgnored(t))
					{
						_eventData.pointerCurrentRaycast = r;
						return r.gameObject;
					}
				}

				return null;
			}

			void PointerDown(GameObject target)
			{
				_eventData.delta = Vector2.zero;
				_eventData.dragging = false;
				_eventData.useDragThreshold = true;
				_eventData.eligibleForClick = true;
				_eventData.pressPosition = _eventData.position;
				_eventData.pointerPressRaycast = _eventData.pointerCurrentRaycast;
				_eventData.rawPointerPress = target;

				var fired = ExecuteEvents.ExecuteHierarchy(target, _eventData, ExecuteEvents.pointerDownHandler);

				if (fired != null)
				{
					_eventData.pointerPress = fired;
				}
				else
				{
					fired = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
				}

				var time = Time.unscaledTime;

				if (fired == _eventData.lastPress && time - _eventData.clickTime < 0.3f)
				{
					++_eventData.clickCount;
				}
				else
				{
					_eventData.clickCount = 1;
				}

				_eventData.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(target);
				if (_eventData.pointerDrag != null)
				{
					ExecuteEvents.Execute(_eventData.pointerDrag, _eventData, ExecuteEvents.initializePotentialDrag);
				}

				_eventData.clickTime = time;
				_currentTime = 0;

				_marker.gameObject.SetActive(true);

				var scaleFactor = _marker.canvas.scaleFactor;
				_marker.rectTransform.anchoredPosition = new Vector2(_eventData.position.x / scaleFactor,
					_eventData.position.y / scaleFactor);
			}

			void PointerUp(GameObject target)
			{
				ExecuteEvents.Execute(_eventData.pointerPress, _eventData, ExecuteEvents.pointerUpHandler);

				if (_eventData.eligibleForClick)
				{
					var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
					if (_eventData.pointerPress == clickHandler)
					{
						ExecuteEvents.Execute(clickHandler, _eventData, ExecuteEvents.pointerClickHandler);
					}
				}

				_eventData.pointerPress = null;
				_eventData.rawPointerPress = null;

				if (_eventData.dragging)
				{
					EndDrag(target);
				}

				_marker.gameObject.SetActive(false);
			}

			void BeginDrag()
			{
				if (_eventData.pointerDrag != null)
				{
					_eventData.dragging = true;
					_eventData.useDragThreshold = true;

					ExecuteEvents.Execute(_eventData.pointerDrag, _eventData, ExecuteEvents.beginDragHandler);

					if (_eventData.pointerPress != _eventData.pointerDrag)
					{
						ExecuteEvents.Execute(_eventData.pointerPress, _eventData, ExecuteEvents.pointerUpHandler);
					}
				}

				_eventData.eligibleForClick = false;
			}

			void Drag(GameObject target)
			{
				if (_eventData.pointerDrag == null)
				{
					PointerUp(target);
				}
				else
				{
					ExecuteEvents.Execute(_eventData.pointerDrag, _eventData, ExecuteEvents.dragHandler);
				}
			}

			void EndDrag(GameObject target)
			{
				ExecuteEvents.Execute(target, _eventData, ExecuteEvents.dropHandler);

				if (_eventData.pointerDrag != null)
				{
					ExecuteEvents.Execute(_eventData.pointerDrag, _eventData, ExecuteEvents.endDragHandler);
				}

				_eventData.dragging = false;
				_eventData.pointerDrag = null;
			}
		}

		public bool Enabled { get; private set; }

		int _tapCount;
		Canvas _canvas;

		Tap[] _taps;
		LogItem[] _logItems;

		readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

		Vector3[] _fourCorners;
		float[] _floats;

		protected virtual void SetupInternal(
			Canvas canvasForMarker, Sprite markerSprite, int tapCount)
		{
			_tapCount = tapCount;
			Enabled = false;
			_canvas = canvasForMarker;
			_taps = new Tap[_tapCount];
			_logItems = new LogItem[_tapCount];
			_fourCorners = new Vector3[4];
			_floats = new float[4];

			for (var i = 0; i < _tapCount; ++i)
			{
				var markerGameObject = new GameObject($"Marker {i + 1}");
				markerGameObject.transform.SetParent(_canvas.transform, false);
				markerGameObject.transform.localPosition = Vector3.zero;
				markerGameObject.SetActive(false);
				var marker = markerGameObject.AddComponent<Image>();
				marker.raycastTarget = false;
				marker.sprite = markerSprite;
				marker.SetNativeSize();
				marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = Vector2.zero;
				_taps[i] = new Tap(marker, i + 100, this, _raycastResults);
			}
		}

		/// <remarks>
		/// needs to call <see cref="SetupInternal"/>.
		/// </remarks>
		/// <param name="canvasForMarker">canvas to show tap markers</param>
		/// <param name="markerSprite">sprite for tap marker</param>
		public abstract void Setup(Canvas canvasForMarker, Sprite markerSprite);

		public virtual void Dispose()
		{
			if (_taps != null)
			{
				foreach (var t in _taps)
				{
					t.Dispose();
				}

				_taps = null;
			}

			_logItems = null;
			_canvas = null;
			_fourCorners = null;
			_floats = null;
		}

		public void Start()
		{
			if (Current != null)
			{
				if (Current == this)
				{
					return;
				}

				throw new InvalidOperationException();
			}

			Enabled = true;
			Current = this;
			OnStart();
		}

		protected virtual void OnStart()
		{
		}

		public void Stop()
		{
			if (Current != this)
			{
				throw new InvalidOperationException();
			}

			Current = null;
			Enabled = false;
			foreach (var tap in _taps)
			{
				tap.Stop();
			}

			for (var i = 0; i < _logItems.Length; ++i)
			{
				_logItems[i] = null;
			}

			OnStop();
		}

		protected virtual void OnStop()
		{
		}

		protected virtual void CreateLog(
			int index, GameObject target, Vector2 startPosition, Vector2 endPosition, float duration,
			out LogItem logItem)
		{
			logItem = _logItems[index] ?? new LogItem();
			logItem.Index = index;
			logItem.DateTime = DateTime.Now;
			logItem.Target = target;
			logItem.ScreenPoint = startPosition;
			logItem.DragTo = (startPosition == endPosition) ? null : (Vector2?)endPosition;
			logItem.Duration = duration;
			logItem.FrameCount = Time.frameCount;
		}

		protected virtual bool ToBeIgnored(Transform transform)
		{
			return false;
		}

		public IEnumerable<LogItem> GetLatestLog()
		{
			foreach (var logItem in _logItems)
			{
				if (logItem is {Active: true})
				{
					yield return logItem;
				}
			}
		}

		public void Update(float deltaTime)
		{
			if (!Enabled)
			{
				return;
			}

			OnPreUpdate(deltaTime);

			for (var i = 0; i < _taps.Length; ++i)
			{
				var tap = _taps[i];
				if (_logItems[i] is { } logItem)
				{
					logItem.Active = false;
				}

				if (tap.Active)
				{
					tap.Update(deltaTime);
				}
				else
				{
					Update(i);
				}
			}

			OnPostUpdate(deltaTime);
		}

		protected virtual void OnPreUpdate(float deltaTime)
		{
		}

		protected virtual void OnPostUpdate(float deltaTime)
		{
		}

		protected virtual void Update(int index)
		{
		}

		protected internal GameObject Fire(int index, Vector2 position, float duration)
		{
			return Fire(index, position, position, duration);
		}

		protected internal GameObject Fire(int index, Vector2 startPosition, Vector2 endPosition, float duration)
		{
			if (startPosition.x < 0 || startPosition.x > Screen.width || startPosition.y < 0 ||
				startPosition.y > Screen.height)
			{
				return null;
			}

			if (endPosition.x < 0 || endPosition.x > Screen.width || endPosition.y < 0 || endPosition.y > Screen.height)
			{
				return null;
			}

			if (!Input.multiTouchEnabled)
			{
				foreach (var t in _taps)
				{
					if (t.Active)
					{
						return null;
					}
				}
			}

			var tap = _taps[index];

			if (tap.Start(startPosition, endPosition, duration))
			{
				CreateLog(index, tap.PointerPress, startPosition, endPosition, duration, out _logItems[index]);
				if (_logItems[index] is { } logItem)
				{
					logItem.Active = true;
				}

				return tap.PointerPress;
			}

			return null;
		}

		/// <summary>
		/// Calculates the screen point of the transform located under the canvas with <see cref="Canvas.worldCamera"/>.
		/// </summary>
		protected internal Vector2 GetScreenPoint(Camera camera, RectTransform t)
		{
			var position = Vector2.zero;
			t.GetWorldCorners(_fourCorners);
			foreach (var p in _fourCorners)
			{
				position += RectTransformUtility.WorldToScreenPoint(camera, p);
			}

			return position / 4;
		}

		/// <summary>
		/// Calculates the screen point of the transform located under the canvas with <see cref="Canvas.worldCamera"/>.
		/// </summary>
		protected internal Vector2 GetScreenPoint(Camera camera, RectTransform t, float tx, float ty)
		{
			t.GetWorldCorners(_fourCorners);
			for (var i = 0; i < 4; ++i)
			{
				_fourCorners[i] = RectTransformUtility.WorldToScreenPoint(camera, _fourCorners[i]);
			}

			for (var i = 0; i < 4; ++i)
			{
				_floats[i] = _fourCorners[i].x;
			}

			var x = Mathf.Lerp(
				Mathf.Min(_floats),
				Mathf.Max(_floats),
				tx);

			for (var i = 0; i < 4; ++i)
			{
				_floats[i] = _fourCorners[i].y;
			}

			var y = Mathf.Lerp(
				Mathf.Min(_floats),
				Mathf.Max(_floats),
				ty);

			return new Vector2(x, y);
		}

		/// <summary>
		/// Calculates the screen point of the transform located under the canvas that doesn't have a <see cref="Canvas.worldCamera"/>.
		/// </summary>
		protected internal Vector2 GetScreenPoint(RectTransform t)
		{
			var position = Vector2.zero;
			t.GetWorldCorners(_fourCorners);
			foreach (var p in _fourCorners)
			{
				position += (Vector2)p;
			}

			return position / 4;
		}

		/// <summary>
		/// Calculates the screen point of the transform located under the canvas that doesn't have a <see cref="Canvas.worldCamera"/>.
		/// </summary>
		protected internal Vector2 GetScreenPoint(RectTransform t, float tx, float ty)
		{
			t.GetWorldCorners(_fourCorners);

			for (var i = 0; i < 4; ++i)
			{
				_floats[i] = _fourCorners[i].x;
			}

			var x = Mathf.Lerp(
				Mathf.Min(_floats),
				Mathf.Max(_floats),
				tx);

			for (var i = 0; i < 4; ++i)
			{
				_floats[i] = _fourCorners[i].y;
			}

			var y = Mathf.Lerp(
				Mathf.Min(_floats),
				Mathf.Max(_floats),
				ty);

			return new Vector2(x, y);
		}
	}
}
#endif