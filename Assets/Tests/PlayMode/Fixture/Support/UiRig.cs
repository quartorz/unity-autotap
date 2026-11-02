#if ENABLE_AUTOTAP
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// One call of an event handler of <see cref="HandlerRecorder"/>.
	/// </summary>
	public sealed class Call
	{
		/// <summary>The handler, for example <c>pointerDown</c> or <c>beginDrag</c>.</summary>
		public string Name;

		/// <summary>The object whose recorder was called.</summary>
		public GameObject Target;

		/// <summary><see cref="Time.frameCount"/> when the handler was called.</summary>
		public int Frame;

		/// <summary>The position of the call in the log.</summary>
		public int Sequence;

		public int PointerId;
		public PointerEventData.InputButton Button;
		public Vector2 Position;
		public Vector2 ScrollDelta;
		public UIPointerType PointerType;
		public InputDevice Device;
		public int TouchId;
		public float Pressure;
		public MoveDirection MoveDirection;

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"#{Sequence} frame {Frame}: {Name} on {(Target != null ? Target.name : "(destroyed)")} " +
				$"(pointer {PointerId}, {Button})";
		}
	}

	/// <summary>
	/// The calls of every <see cref="HandlerRecorder"/> of a rig, in the order they were made.
	/// </summary>
	public sealed class CallLog
	{
		readonly List<Call> _calls = new List<Call>();

		public IReadOnlyList<Call> Calls => _calls;

		public int Count => _calls.Count;

		internal void Add(Call call)
		{
			call.Sequence = _calls.Count;
			call.Frame = Time.frameCount;
			_calls.Add(call);
		}

		public void Clear()
		{
			_calls.Clear();
		}

		/// <summary>
		/// The calls named <paramref name="name"/>, on <paramref name="target"/> if given, in order.
		/// </summary>
		public IEnumerable<Call> Of(string name, GameObject target = null)
		{
			return _calls.Where(c => c.Name == name && (target == null || c.Target == target));
		}

		public int CountOf(string name, GameObject target = null)
		{
			return Of(name, target).Count();
		}

		public Call First(string name, GameObject target = null)
		{
			return Of(name, target).FirstOrDefault();
		}

		public Call Last(string name, GameObject target = null)
		{
			return Of(name, target).LastOrDefault();
		}

		/// <summary>
		/// The <c>name@target</c> of the calls that have one of <paramref name="names"/>, in order. Calls with the
		/// same name on the same target that follow each other are listed once.
		/// </summary>
		public List<string> Trace(params string[] names)
		{
			return Trace((GameObject)null, names);
		}

		/// <inheritdoc cref="Trace(string[])"/>
		/// <param name="target">Only the calls on this object, or all objects if <c>null</c>.</param>
		/// <param name="names">The handlers to list.</param>
		public List<string> Trace(GameObject target, params string[] names)
		{
			var result = new List<string>();
			foreach (var call in _calls)
			{
				if (target != null && call.Target != target)
				{
					continue;
				}

				if (names.Length > 0 && !names.Contains(call.Name))
				{
					continue;
				}

				var entry = $"{call.Name}@{(call.Target != null ? call.Target.name : "(destroyed)")}";
				if (result.Count == 0 || result[result.Count - 1] != entry)
				{
					result.Add(entry);
				}
			}

			return result;
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			return string.Join("\n", _calls.Select(c => c.ToString()));
		}
	}

	/// <summary>
	/// Records every call of the event handlers that the event system can deliver, into a <see cref="CallLog"/>.
	/// Pointer move, update-selected and the like are deliberately not recorded: they are delivered on every frame
	/// and have nothing to tell about what AutoTap did.
	/// </summary>
	public sealed class HandlerRecorder : MonoBehaviour,
		IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
		IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
		IScrollHandler, IMoveHandler, ISubmitHandler, ICancelHandler, ISelectHandler, IDeselectHandler
	{
		public CallLog Log;

		void RecordPointer(string name, PointerEventData data)
		{
			var call = new Call
			{
				Name = name,
				Target = gameObject,
				PointerId = data.pointerId,
				Button = data.button,
				Position = data.position,
				ScrollDelta = data.scrollDelta,
			};

			if (data is ExtendedPointerEventData extended)
			{
				call.PointerType = extended.pointerType;
				call.Device = extended.device;
				call.TouchId = extended.touchId;
				call.Pressure = extended.pressure;
			}

			Log?.Add(call);
		}

		void RecordBase(string name, MoveDirection direction = MoveDirection.None)
		{
			Log?.Add(new Call { Name = name, Target = gameObject, MoveDirection = direction });
		}

		public void OnPointerEnter(PointerEventData eventData) => RecordPointer("pointerEnter", eventData);
		public void OnPointerExit(PointerEventData eventData) => RecordPointer("pointerExit", eventData);
		public void OnPointerDown(PointerEventData eventData) => RecordPointer("pointerDown", eventData);
		public void OnPointerUp(PointerEventData eventData) => RecordPointer("pointerUp", eventData);
		public void OnPointerClick(PointerEventData eventData) => RecordPointer("pointerClick", eventData);
		public void OnInitializePotentialDrag(PointerEventData eventData) => RecordPointer("initializePotentialDrag", eventData);
		public void OnBeginDrag(PointerEventData eventData) => RecordPointer("beginDrag", eventData);
		public void OnDrag(PointerEventData eventData) => RecordPointer("drag", eventData);
		public void OnEndDrag(PointerEventData eventData) => RecordPointer("endDrag", eventData);
		public void OnDrop(PointerEventData eventData) => RecordPointer("drop", eventData);
		public void OnScroll(PointerEventData eventData) => RecordPointer("scroll", eventData);
		public void OnMove(AxisEventData eventData) => RecordBase("move", eventData.moveDir);
		public void OnSubmit(BaseEventData eventData) => RecordBase("submit");
		public void OnCancel(BaseEventData eventData) => RecordBase("cancel");
		public void OnSelect(BaseEventData eventData) => RecordBase("select");
		public void OnDeselect(BaseEventData eventData) => RecordBase("deselect");
	}

	/// <summary>
	/// A <see cref="Button"/> that remembers its selection states and its clicks.
	/// </summary>
	public sealed class ProbeButton : Button
	{
		public readonly List<string> States = new List<string>();

		public int ClickCount;

		public string CurrentState => currentSelectionState.ToString();

		protected override void DoStateTransition(SelectionState state, bool instant)
		{
			base.DoStateTransition(state, instant);
			States.Add(state.ToString());
		}
	}

	/// <summary>
	/// A <see cref="Selectable"/> that remembers its selection states.
	/// </summary>
	public sealed class ProbeSelectable : Selectable
	{
		public readonly List<string> States = new List<string>();

		public string CurrentState => currentSelectionState.ToString();

		protected override void DoStateTransition(SelectionState state, bool instant)
		{
			base.DoStateTransition(state, instant);
			States.Add(state.ToString());
		}
	}

	/// <summary>
	/// An event system with the real <see cref="InputSystemUIInputModule"/> (default actions) and an overlay canvas,
	/// built by code. Positions are given as fractions of the screen, so that the layout does not depend on the size
	/// of the Game view.
	/// </summary>
	public sealed class UiRig
	{
		public const int Columns = 4;
		public const int Rows = 3;

		const int MinimumWidth = 480;
		const int MinimumHeight = 360;

		public GameObject Root { get; private set; }
		public EventSystem EventSystem { get; private set; }
		public InputSystemUIInputModule Module { get; private set; }
		public Canvas Canvas { get; private set; }
		public Sprite MarkerSprite { get; private set; }
		public CallLog Log { get; } = new CallLog();

		UiRig()
		{
		}

		/// <summary>
		/// Builds the rig. Wait for a frame and call <see cref="Canvas.ForceUpdateCanvases"/> before reading
		/// positions from it.
		/// </summary>
		public static UiRig Create()
		{
			if (Screen.width < MinimumWidth || Screen.height < MinimumHeight)
			{
				Assert.Inconclusive(
					$"The Game view is {Screen.width}x{Screen.height}; at least {MinimumWidth}x{MinimumHeight} is needed.");
			}

			var rig = new UiRig();
			rig.Build();
			return rig;
		}

		void Build()
		{
			Root = new GameObject("AutoTapTestRig");

			var eventSystemObject = new GameObject("EventSystem");
			eventSystemObject.transform.SetParent(Root.transform, false);
			EventSystem = eventSystemObject.AddComponent<EventSystem>();
			Module = eventSystemObject.AddComponent<InputSystemUIInputModule>();

			var canvasObject = new GameObject("Canvas", typeof(RectTransform));
			canvasObject.transform.SetParent(Root.transform, false);
			Canvas = canvasObject.AddComponent<Canvas>();
			Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvasObject.AddComponent<GraphicRaycaster>();

			var texture = new Texture2D(16, 16);
			MarkerSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
		}

		/// <summary>
		/// Destroys everything the rig created. Call it after the auto tap was disposed.
		/// </summary>
		public void Destroy()
		{
			if (Root != null)
			{
				Object.DestroyImmediate(Root);
			}

			if (MarkerSprite != null)
			{
				Object.DestroyImmediate(MarkerSprite.texture);
				Object.DestroyImmediate(MarkerSprite);
			}
		}

		/// <summary>
		/// The area of a cell of the 4 x 3 grid, as fractions of the screen. Row 0 is the bottom row. A cell is at
		/// least 120 x 90 pixels with the smallest supported Game view.
		/// </summary>
		public Rect Cell(int column, int row)
		{
			const float pad = 0.1f;
			var w = 1f / Columns;
			var h = 1f / Rows;
			return new Rect((column + pad) * w, (row + pad) * h, (1 - 2 * pad) * w, (1 - 2 * pad) * h);
		}

		/// <summary>
		/// A raycast target with a <see cref="HandlerRecorder"/>. Objects added later are in front of the earlier ones.
		/// </summary>
		/// <param name="name">The name, which shows up in <see cref="CallLog.Trace(string[])"/>.</param>
		/// <param name="area">The area, as fractions of the parent (the screen for the canvas).</param>
		/// <param name="parent">The parent, or <c>null</c> for the canvas.</param>
		/// <param name="record">Whether to put a recorder on it.</param>
		public GameObject Add(string name, Rect area, Transform parent = null, bool record = true)
		{
			var go = new GameObject(name, typeof(RectTransform), typeof(Image));
			var rectTransform = (RectTransform)go.transform;
			rectTransform.SetParent(parent != null ? parent : Canvas.transform, false);
			rectTransform.anchorMin = area.min;
			rectTransform.anchorMax = area.max;
			rectTransform.offsetMin = Vector2.zero;
			rectTransform.offsetMax = Vector2.zero;

			var image = go.GetComponent<Image>();
			image.color = new Color(1f, 1f, 1f, 0.25f);
			image.raycastTarget = true;

			if (record)
			{
				go.AddComponent<HandlerRecorder>().Log = Log;
			}

			return go;
		}

		/// <summary>
		/// A recorder that covers the whole screen. It is behind everything added after it.
		/// </summary>
		public GameObject AddBackground(string name = "Background")
		{
			return Add(name, new Rect(0, 0, 1, 1));
		}

		/// <summary>
		/// A <see cref="ProbeButton"/>, with a recorder unless <paramref name="record"/> is <c>false</c>. Its
		/// <see cref="ProbeButton.ClickCount"/> counts onClick. A button without a recorder has no drag handler, so
		/// nothing is dragged from it.
		/// </summary>
		public ProbeButton AddButton(string name, Rect area, Transform parent = null, bool record = true)
		{
			var go = Add(name, area, parent, record);
			var button = go.AddComponent<ProbeButton>();
			button.transition = Selectable.Transition.None;
			button.onClick.AddListener(() => button.ClickCount++);
			return button;
		}

		/// <summary>
		/// A <see cref="ProbeSelectable"/> with a recorder.
		/// </summary>
		public ProbeSelectable AddSelectable(string name, Rect area)
		{
			var go = Add(name, area);
			var selectable = go.AddComponent<ProbeSelectable>();
			selectable.transition = Selectable.Transition.None;
			return selectable;
		}

		/// <summary>
		/// A vertical <see cref="ScrollRect"/> that clamps its content and has no inertia, whose content is several
		/// times higher than its viewport. It has a recorder, so it also records the scroll it receives.
		/// </summary>
		public ScrollRect AddScrollRect(string name, Rect area, float contentHeightFactor = 4f)
		{
			var go = Add(name, area);
			var scrollRect = go.AddComponent<ScrollRect>();

			var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
			var viewportRect = (RectTransform)viewport.transform;
			viewportRect.SetParent(go.transform, false);
			viewportRect.anchorMin = Vector2.zero;
			viewportRect.anchorMax = Vector2.one;
			viewportRect.offsetMin = Vector2.zero;
			viewportRect.offsetMax = Vector2.zero;

			var content = new GameObject("Content", typeof(RectTransform));
			var contentRect = (RectTransform)content.transform;
			contentRect.SetParent(viewport.transform, false);
			contentRect.anchorMin = new Vector2(0, 1);
			contentRect.anchorMax = new Vector2(1, 1);
			contentRect.pivot = new Vector2(0.5f, 1);
			contentRect.sizeDelta = new Vector2(0, area.height * Screen.height * contentHeightFactor);
			contentRect.anchoredPosition = Vector2.zero;

			scrollRect.viewport = viewportRect;
			scrollRect.content = contentRect;
			scrollRect.horizontal = false;
			scrollRect.vertical = true;
			scrollRect.movementType = ScrollRect.MovementType.Clamped;
			scrollRect.inertia = false;
			scrollRect.scrollSensitivity = 1f;
			return scrollRect;
		}

		/// <summary>
		/// The screen position at (<paramref name="tx"/>, <paramref name="ty"/>) of the rectangle of
		/// <paramref name="go"/>, where (0, 0) is the lower left corner and (1, 1) the upper right one. The canvas is
		/// an overlay, so that its world positions are screen pixels.
		/// </summary>
		public Vector2 Point(GameObject go, float tx, float ty)
		{
			var corners = new Vector3[4];
			((RectTransform)go.transform).GetWorldCorners(corners);
			return new Vector2(
				Mathf.Lerp(corners[0].x, corners[2].x, tx),
				Mathf.Lerp(corners[0].y, corners[2].y, ty));
		}

		/// <summary>
		/// The center of <paramref name="go"/> on the screen.
		/// </summary>
		public Vector2 Center(GameObject go)
		{
			return Point(go, 0.5f, 0.5f);
		}

		/// <summary>
		/// A screen position that no object of the rig covers: the lower left corner, which is outside the padding of
		/// every cell. Not valid while a background is present.
		/// </summary>
		public Vector2 EmptyPoint => new Vector2(2f, 2f);

		/// <summary>
		/// Selects <paramref name="go"/> as the object that receives submit, cancel and move.
		/// </summary>
		public void Select(GameObject go)
		{
			EventSystem.SetSelectedGameObject(go);
		}
	}
}
#endif
