#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// An <see cref="AutoTapBase"/> that exposes what the tests need.
	/// </summary>
	sealed class TestAutoTap : AutoTapBase
	{
		public int SlotCount = 3;
		public readonly List<KeyValuePair<int, LogItem>> Completed = new List<KeyValuePair<int, LogItem>>();
		public readonly List<string> Calls = new List<string>();
		public Func<RaycastResult, bool> Refuse;
		public Action<int> OnEmptySlot;
		public Action<int, LogItem> OnCompletedHook;

		public TestAutoTap(IVirtualInput input = null) : base(input)
		{
		}

		public override void Setup(Canvas canvasForMarker, Sprite markerSprite)
		{
			SetupInternal(canvasForMarker, markerSprite, SlotCount);
		}

		public LogItem Tap(int index, Vector2 position, float duration)
		{
			return Fire(index, position, duration);
		}

		public LogItem Drag(int index, Vector2 from, Vector2 to, float duration)
		{
			return Fire(index, from, to, duration);
		}

		public LogItem Do(int index, Gesture gesture)
		{
			return Fire(index, gesture);
		}

		protected override void OnTapCompleted(int index, LogItem logItem)
		{
			Calls.Add($"completed {index}");
			Completed.Add(new KeyValuePair<int, LogItem>(index, logItem));
			OnCompletedHook?.Invoke(index, logItem);
		}

		protected override bool ShouldNotPressOn(RaycastResult topmost)
		{
			return Refuse != null && Refuse(topmost);
		}

		protected override void Update(int index)
		{
			OnEmptySlot?.Invoke(index);
		}
	}

	/// <summary>
	/// A game object that handles the press and the click.
	/// </summary>
	sealed class ClickHandler : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
	{
		public void OnPointerDown(PointerEventData eventData)
		{
		}

		public void OnPointerClick(PointerEventData eventData)
		{
		}
	}

	/// <summary>
	/// A game object that handles only the click.
	/// </summary>
	sealed class ClickOnlyHandler : MonoBehaviour, IPointerClickHandler
	{
		public void OnPointerClick(PointerEventData eventData)
		{
		}
	}

	/// <summary>
	/// A game object that can be dragged.
	/// </summary>
	sealed class DragHandler : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		public void OnPointerDown(PointerEventData eventData)
		{
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
		}

		public void OnDrag(PointerEventData eventData)
		{
		}

		public void OnEndDrag(PointerEventData eventData)
		{
		}
	}

	/// <summary>
	/// A game object that handles the press, the click and the drag.
	/// </summary>
	sealed class ClickDragHandler : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IDragHandler
	{
		public void OnPointerDown(PointerEventData eventData)
		{
		}

		public void OnPointerClick(PointerEventData eventData)
		{
		}

		public void OnDrag(PointerEventData eventData)
		{
		}
	}

	/// <summary>
	/// A component that handles nothing.
	/// </summary>
	sealed class Plain : MonoBehaviour
	{
	}

	/// <summary>
	/// A game object that accepts a drop.
	/// </summary>
	sealed class DropHandler : MonoBehaviour, IDropHandler
	{
		public void OnDrop(PointerEventData eventData)
		{
		}
	}

	/// <summary>
	/// An event system with a raycaster that hits rectangles of the screen, laid out in fractions of the screen so
	/// that it works at any screen size. Nothing is rendered, which edit mode does not do for a canvas.
	/// </summary>
	sealed class TestScene : IDisposable
	{
		sealed class RectRaycaster : BaseRaycaster
		{
			public readonly List<Target> Targets = new List<Target>();

			public override Camera eventCamera => null;

			public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
			{
				foreach (var target in Targets)
				{
					if (target.GameObject == null
						|| !target.GameObject.activeInHierarchy
						|| !target.Rect.Contains(eventData.position))
					{
						continue;
					}

					resultAppendList.Add(new RaycastResult
					{
						gameObject = target.GameObject,
						module = this,
						depth = target.Depth,
						index = resultAppendList.Count,
						screenPosition = eventData.position,
					});
				}
			}
		}

		struct Target
		{
			public GameObject GameObject;
			public Rect Rect;
			public int Depth;
		}

		readonly List<GameObject> _roots = new List<GameObject>();
		readonly RectRaycaster _raycaster;
		int _depth;

		public TestScene()
		{
			var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
			_roots.Add(eventSystemObject);
			EventSystem = eventSystemObject.GetComponent<EventSystem>();
			var raycasterObject = new GameObject("Raycaster", typeof(RectRaycaster));
			_roots.Add(raycasterObject);
			_raycaster = raycasterObject.GetComponent<RectRaycaster>();

			var canvasObject = new GameObject("Canvas", typeof(Canvas));
			_roots.Add(canvasObject);
			Canvas = canvasObject.GetComponent<Canvas>();
			Canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			// The event system and the raycaster do not run in edit mode, where they would register themselves.
			Invoke(EventSystem, "OnEnable");
			Invoke(_raycaster, "OnEnable");
		}

		public EventSystem EventSystem { get; }

		/// <summary>
		/// A canvas for the markers. Nothing on it is hit.
		/// </summary>
		public Canvas Canvas { get; }

		static void Invoke(Behaviour target, string method)
		{
			target.GetType()
				.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.Invoke(target, null);
		}

		/// <summary>
		/// A point given as fractions of the screen size.
		/// </summary>
		public static Vector2 At(float x, float y)
		{
			return new Vector2(Screen.width * x, Screen.height * y);
		}

		/// <summary>
		/// Adds an object that is hit inside the rectangle, in front of what was added before, and has a component
		/// of type <typeparamref name="T"/>. The rectangle is given in fractions of the screen.
		/// </summary>
		public T Add<T>(string name, float xMin, float yMin, float xMax, float yMax, Transform parent = null)
			where T : Component
		{
			var gameObject = new GameObject(name, typeof(RectTransform));
			gameObject.transform.SetParent(parent != null ? parent : Canvas.transform, false);
			var rect = (RectTransform)gameObject.transform;
			rect.anchorMin = rect.anchorMax = Vector2.zero;
			rect.pivot = Vector2.zero;
			rect.sizeDelta = At(xMax - xMin, yMax - yMin);
			rect.position = At(xMin, yMin);

			var component = gameObject.AddComponent<T>();
			_raycaster.Targets.Add(new Target
			{
				GameObject = gameObject,
				Rect = Rect.MinMaxRect(
					Screen.width * xMin, Screen.height * yMin, Screen.width * xMax, Screen.height * yMax),
				Depth = ++_depth,
			});
			return component;
		}

		public void Dispose()
		{
			if (EventSystem != null)
			{
				Invoke(EventSystem, "OnDisable");
			}

			if (_raycaster != null)
			{
				Invoke(_raycaster, "OnDisable");
			}

			foreach (var root in _roots)
			{
				if (root != null)
				{
					UnityEngine.Object.DestroyImmediate(root);
				}
			}

			_roots.Clear();
		}
	}
}
#endif
