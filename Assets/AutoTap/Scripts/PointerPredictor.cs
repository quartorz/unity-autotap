#if ENABLE_AUTOTAP
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityAutoTap
{
	/// <summary>
	/// What the input module is going to find under a point when a pointer is pressed there.
	/// </summary>
	internal readonly struct PressProbe
	{
		public PressProbe(
			RaycastResult[] hits, RaycastResult topmost, GameObject press, GameObject click, GameObject drag)
		{
			Hits = hits;
			Topmost = topmost;
			Press = press;
			Click = click;
			Drag = drag;
		}

		/// <summary>
		/// All raycast results, nearest first.
		/// </summary>
		public RaycastResult[] Hits { get; }

		/// <summary>
		/// The first result that has a game object (<see cref="RaycastResult.isValid"/> is <c>false</c> if none).
		/// </summary>
		public RaycastResult Topmost { get; }

		/// <summary>
		/// The object that receives the pointer-down, or, if there is none, the click.
		/// </summary>
		public GameObject Press { get; }

		/// <summary>
		/// The object that receives the click.
		/// </summary>
		public GameObject Click { get; }

		/// <summary>
		/// The object that receives the drag.
		/// </summary>
		public GameObject Drag { get; }
	}

	/// <summary>
	/// Works out what <see cref="UnityEngine.InputSystem.UI.InputSystemUIInputModule"/> is going to do for one
	/// pointer, following the rules of the module without calling any handler: which object receives the press,
	/// whether the press becomes a drag, and whether the release is a click or a drop. The result is
	/// <see cref="Prediction"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The virtual fingers and pointers predict with this class (<see cref="VirtualFinger.Prediction"/>,
	/// <see cref="VirtualPointer.GetPrediction"/>). It predicts for the pointer of any other device as well, a real one
	/// whose changes an <see cref="InputChangeReader"/> reports for example, when it is told what happened to the
	/// pointer in each input update:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// For an input update in which the pointer was pressed or released, or changed while it was pressed
	/// (<see cref="Active"/>), call <see cref="Process"/> once, with the position at the end of the update.
	/// </description></item>
	/// <item><description>
	/// For an input update in which the pointer only moved while it was not pressed, call <see cref="NotePosition"/>
	/// instead.
	/// </description></item>
	/// <item><description>
	/// Call them at the end of the input update, before the event system handles the frame. <see cref="Process"/>
	/// raycasts with <see cref="EventSystem.current"/> when it is called, and the input module raycasts when it handles
	/// the frame, so the two find the same objects only if the UI does not change in between.
	/// </description></item>
	/// </list>
	/// <para>
	/// The module handles a pointer once per frame, at the last position of the frame: the press happens at that
	/// position, a press and a release in the same frame are handled in this order, and a release is handled before
	/// the drag of the same frame. <see cref="Process"/> takes the input of one input update at once for that reason.
	/// </para>
	/// <para>
	/// The module lets the object that receives the drag switch the drag threshold off
	/// (<see cref="IInitializePotentialDragHandler"/>). No handler is called here, so the threshold is taken as switched
	/// off only for an active and enabled <see cref="Slider"/> or <see cref="Scrollbar"/>, the components of uGUI that
	/// switch it off.
	/// </para>
	/// </remarks>
	public sealed class PointerPredictor
	{
		static readonly List<RaycastResult> s_Results = new List<RaycastResult>();
		static PointerEventData s_Scratch;
		static EventSystem s_ScratchOwner;

		Vector2 _pressPosition;
		Vector2 _previousPosition;
		bool _hasPrevious;
		bool _active;
		bool _eligibleForClick;
		bool _dragging;
		bool _useDragThreshold = true;
		GameObject _pointerPress;
		GameObject _pointerClick;
		GameObject _pointerDrag;

		/// <summary>
		/// What the input module is going to do for the pointer, as worked out by the latest <see cref="Process"/>.
		/// It is always the same instance, which <see cref="Process"/> fills in.
		/// </summary>
		public PointerPrediction Prediction { get; } = new PointerPrediction();

		/// <summary>
		/// <c>true</c> from the press until the release.
		/// </summary>
		public bool Active => _active;

		/// <summary>
		/// Tells where the pointer is at the end of an input update in which it neither pressed nor released and was
		/// not held. The input module compares the position of a frame with the position of the frame before, so
		/// every update that moved the pointer has to be noted.
		/// </summary>
		/// <param name="position">The position at the end of the update, in screen pixels.</param>
		public void NotePosition(Vector2 position)
		{
			_previousPosition = position;
			_hasPrevious = true;
		}

		/// <summary>
		/// Finds what is under <paramref name="position"/> in the same way as the input module does.
		/// </summary>
		internal static PressProbe Probe(Vector2 position)
		{
			var eventSystem = EventSystem.current;
			if (eventSystem == null)
			{
				return new PressProbe(System.Array.Empty<RaycastResult>(), default, null, null, null);
			}

			if (s_Scratch == null || s_ScratchOwner != eventSystem)
			{
				s_Scratch = new PointerEventData(eventSystem);
				s_ScratchOwner = eventSystem;
			}

			s_Scratch.position = position;
			s_Results.Clear();
			eventSystem.RaycastAll(s_Scratch, s_Results);

			var topmost = default(RaycastResult);
			foreach (var result in s_Results)
			{
				if (result.gameObject != null)
				{
					topmost = result;
					break;
				}
			}

			var hits = s_Results.ToArray();
			s_Results.Clear();

			var over = topmost.gameObject;
			var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(over);
			var press = ExecuteEvents.GetEventHandler<IPointerDownHandler>(over);
			if (press == null)
			{
				press = click;
			}

			var drag = ExecuteEvents.GetEventHandler<IDragHandler>(over);
			return new PressProbe(hits, topmost, press, click, drag);
		}

		/// <summary>
		/// Tells whether a move to <paramref name="position"/> would start a drag.
		/// </summary>
		/// <param name="position">The position to move to, in screen pixels.</param>
		/// <returns>
		/// <c>true</c> if the pointer is pressed (<see cref="Active"/>) and not dragging yet, something receives the
		/// drag (<see cref="PointerPrediction.PointerDrag"/>), and the move from where the pointer was pressed reaches
		/// <see cref="EventSystem.pixelDragThreshold"/> or the threshold is switched off.
		/// </returns>
		public bool IsBeyondDragThreshold(Vector2 position)
		{
			if (!_active || _pointerDrag == null || _dragging)
			{
				return false;
			}

			return !_useDragThreshold || ExceedsThreshold(position);
		}

		/// <summary>
		/// Handles what happened to the pointer in one input update, and updates <see cref="Prediction"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// A press and a release in the same update are handled in this order. The press is evaluated at
		/// <paramref name="position"/>: what is under it becomes <see cref="PointerPrediction.PressHits"/>,
		/// <see cref="PointerPrediction.PointerPress"/>, <see cref="PointerPrediction.PointerClick"/> and
		/// <see cref="PointerPrediction.PointerDrag"/>. The release sets <see cref="PointerPrediction.ClickTarget"/>
		/// and <see cref="PointerPrediction.DropTarget"/>. A move while the pointer is pressed on something that
		/// receives the drag sets <see cref="PointerPrediction.Dragging"/> once it reaches the drag threshold.
		/// </para>
		/// <para>
		/// <paramref name="lastOrder"/>, <paramref name="lastFrame"/>, <paramref name="firstBeyondOrder"/> and
		/// <paramref name="firstBeyondFrame"/> tell the auto tap at which change the drag started
		/// (<see cref="AutoTapBase.LogItem.DragStartedOrder"/>). The public members of <see cref="Prediction"/> do not
		/// depend on them.
		/// </para>
		/// </remarks>
		/// <param name="position">The position at the end of the update, in screen pixels.</param>
		/// <param name="pressed">The pointer was pressed in the update.</param>
		/// <param name="released">The pointer was released or canceled in the update.</param>
		/// <param name="lastOrder">
		/// The order of the last change of the pointer in the update, such as <see cref="InputChange.Order"/>.
		/// </param>
		/// <param name="lastFrame">
		/// The frame of the last change of the pointer in the update, such as <see cref="InputChange.Frame"/>.
		/// </param>
		/// <param name="firstBeyondOrder">
		/// The order of the first change of the update that moved the pointer beyond the drag threshold
		/// (<see cref="IsBeyondDragThreshold"/>), or -1.
		/// </param>
		/// <param name="firstBeyondFrame">The frame of that change.</param>
		public void Process(
			Vector2 position, bool pressed, bool released, long lastOrder, int lastFrame, long firstBeyondOrder,
			int firstBeyondFrame)
		{
			var prediction = Prediction;
			var probe = Probe(position);
			var moving = !_hasPrevious || position != _previousPosition;

			if (pressed)
			{
				_active = true;
				_pressPosition = position;
				_eligibleForClick = true;
				_dragging = false;
				_pointerPress = probe.Press;
				_pointerClick = probe.Click;
				_pointerDrag = probe.Drag;
				_useDragThreshold = _pointerDrag == null || UsesDragThreshold(_pointerDrag);

				prediction.PressHits = probe.Hits;
				prediction.PointerPress = probe.Press;
				prediction.PointerClick = probe.Click;
				prediction.PointerDrag = probe.Drag;
				prediction.Dragging = false;
				prediction.ClickTarget = null;
				prediction.DropTarget = null;
				prediction.DragStartedOrder = null;
				prediction.DragStartedFrame = 0;
				++prediction.PressCount;
			}

			if (released && _active)
			{
				var over = probe.Topmost.gameObject;
				var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(over);
				var isClick = _pointerClick != null && _pointerClick == clickHandler && _eligibleForClick;
				prediction.ClickTarget = isClick ? _pointerClick : null;
				prediction.DropTarget = !isClick && _dragging && _pointerDrag != null
					? ExecuteEvents.GetEventHandler<IDropHandler>(over)
					: null;
				++prediction.ReleaseCount;

				_active = false;
				_eligibleForClick = false;
				_pointerPress = null;
				_pointerClick = null;
				_pointerDrag = null;
				_dragging = false;
			}
			else if (_active && _pointerDrag != null && moving)
			{
				if (!_dragging && (!_useDragThreshold || ExceedsThreshold(position)))
				{
					_dragging = true;
					prediction.Dragging = true;
					prediction.DragStartedOrder = firstBeyondOrder >= 0 ? firstBeyondOrder : lastOrder;
					prediction.DragStartedFrame = firstBeyondOrder >= 0 ? firstBeyondFrame : lastFrame;
				}

				if (_dragging && _pointerPress != _pointerDrag)
				{
					_pointerPress = null;
					_eligibleForClick = false;
				}
			}

			_previousPosition = position;
			_hasPrevious = true;
		}

		bool ExceedsThreshold(Vector2 position)
		{
			var eventSystem = EventSystem.current;
			double threshold = eventSystem != null ? eventSystem.pixelDragThreshold : 0;
			return (_pressPosition - position).sqrMagnitude >= threshold * threshold;
		}

		static bool UsesDragThreshold(GameObject pointerDrag)
		{
			// The module calls IInitializePotentialDragHandler on the drag handler and lets it switch the threshold
			// off. The components of uGUI that do so are listed here, because the handlers are not called.
			if (pointerDrag.TryGetComponent<Slider>(out var slider) && slider.isActiveAndEnabled)
			{
				return false;
			}

			if (pointerDrag.TryGetComponent<Scrollbar>(out var scrollbar) && scrollbar.isActiveAndEnabled)
			{
				return false;
			}

			return true;
		}
	}
}
#endif
