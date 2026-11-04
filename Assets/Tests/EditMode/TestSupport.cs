#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// An <see cref="AutoTapBase"/> that exposes what the tests need.
	/// </summary>
	sealed class TestAutoTap : AutoTapBase
	{
		public readonly List<KeyValuePair<int, LogItem>> Completed = new List<KeyValuePair<int, LogItem>>();
		public readonly List<string> Calls = new List<string>();
		public Func<RaycastResult, bool> Refuse;
		public Action<int> OnEmptySlot;
		public Action<int, LogItem> OnCompletedHook;

		public TestAutoTap(IVirtualInput input = null, int slotCount = 3) : base(slotCount, input)
		{
		}

		public TestAutoTap(int slotCount) : base(slotCount)
		{
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

		public LogItem Pointer(string layout, Gesture gesture, int number = 0)
		{
			return FirePointer(layout, gesture, number);
		}

		public LogItem Mouse(Gesture gesture, int number = 0)
		{
			return FireMouse(gesture, number);
		}

		public LogItem Pen(Gesture gesture, int number = 0)
		{
			return FirePen(gesture, number);
		}

		public LogItem Scroll(Vector2 position, Vector2 delta)
		{
			return FireScroll(position, delta);
		}

		public LogItem Device(string layout, ControlSequence sequence, int number = 0)
		{
			return FireDevice(layout, sequence, number);
		}

		public LogItem Gamepad(ControlSequence sequence, int number = 0)
		{
			return FireGamepad(sequence, number);
		}

		public LogItem Key(Key key, float duration = 0f, params Key[] modifiers)
		{
			return FireKey(key, duration, modifiers);
		}

		public Action<float> PreUpdate;

		protected override void OnPreUpdate(float deltaTime)
		{
			PreUpdate?.Invoke(deltaTime);
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
	/// One event that the UI delivered to a <see cref="Recorder"/>.
	/// </summary>
	sealed class UiEvent
	{
		public string Name;
		public string Kind;
		public PointerEventData.InputButton Button;
		public int PointerId;
		public Vector2 Position;
		public Vector2 ScrollDelta;

		public override string ToString()
		{
			return $"{Name}.{Kind}";
		}
	}

	/// <summary>
	/// A game object that handles every pointer event and writes what it receives into a shared list.
	/// </summary>
	sealed class Recorder : MonoBehaviour,
		IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
		IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IScrollHandler,
		ISubmitHandler, ICancelHandler, IMoveHandler, ISelectHandler, IDeselectHandler
	{
		public List<UiEvent> Events;

		void Record(string kind, PointerEventData eventData)
		{
			Events?.Add(new UiEvent
			{
				Name = name,
				Kind = kind,
				Button = eventData.button,
				PointerId = eventData.pointerId,
				Position = eventData.position,
				ScrollDelta = eventData.scrollDelta,
			});
		}

		public void OnPointerEnter(PointerEventData eventData) => Record("enter", eventData);
		public void OnPointerExit(PointerEventData eventData) => Record("exit", eventData);
		public void OnPointerDown(PointerEventData eventData) => Record("down", eventData);
		public void OnPointerUp(PointerEventData eventData) => Record("up", eventData);
		public void OnPointerClick(PointerEventData eventData) => Record("click", eventData);
		public void OnBeginDrag(PointerEventData eventData) => Record("beginDrag", eventData);
		public void OnDrag(PointerEventData eventData) => Record("drag", eventData);
		public void OnEndDrag(PointerEventData eventData) => Record("endDrag", eventData);
		public void OnDrop(PointerEventData eventData) => Record("drop", eventData);
		public void OnScroll(PointerEventData eventData) => Record("scroll", eventData);

		void RecordNavigation(string kind)
		{
			Events?.Add(new UiEvent { Name = name, Kind = kind });
		}

		public void OnSubmit(BaseEventData eventData) => RecordNavigation("submit");
		public void OnCancel(BaseEventData eventData) => RecordNavigation("cancel");
		public void OnSelect(BaseEventData eventData) => RecordNavigation("select");
		public void OnDeselect(BaseEventData eventData) => RecordNavigation("deselect");

		public void OnMove(AxisEventData eventData)
		{
			Events?.Add(new UiEvent { Name = name, Kind = $"move {eventData.moveDir}" });
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

		/// <param name="withInputModule">
		/// Adds <see cref="InputSystemUIInputModule"/> and runs it, so that the UI receives what the virtual devices
		/// do. <see cref="ProcessUi"/> is what the event system does once per frame.
		/// </param>
		public TestScene(bool withInputModule = false)
		{
			var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
			_roots.Add(eventSystemObject);
			EventSystem = eventSystemObject.GetComponent<EventSystem>();
			if (withInputModule)
			{
				InputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
			}
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
			if (InputModule != null)
			{
				Invoke(InputModule, "Awake");
				Invoke(InputModule, "OnEnable");

				// The first update chooses the module; the ones after it process it.
				ProcessUi();
			}
		}

		public EventSystem EventSystem { get; }

		/// <summary>
		/// The input module of the event system, or <c>null</c>.
		/// </summary>
		public InputSystemUIInputModule InputModule { get; }

		/// <summary>
		/// Does what the event system does once per frame: lets the input module deliver what it received.
		/// </summary>
		public void ProcessUi()
		{
			Invoke(EventSystem, "Update");
		}

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
		/// <remarks>
		/// <para>
		/// In edit mode <see cref="Screen"/> is the size of the window that the editor drew last, not of the Game view.
		/// It can be as small as the status bar (40 pixels high), so a test that has to move a pointer beyond the drag
		/// threshold (10 pixels) moves it by more than a quarter of the height.
		/// </para>
		/// </remarks>
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
			if (InputModule != null)
			{
				Invoke(InputModule, "OnDisable");
			}

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
