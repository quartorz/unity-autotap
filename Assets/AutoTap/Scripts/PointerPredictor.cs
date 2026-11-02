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
	/// pointer, following the rules of the module without calling any handler.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The module handles a pointer once per frame, at the last position of the frame: the press happens at that
	/// position, a press and a release in the same frame are handled in this order, and a release is handled before
	/// the drag of the same frame. <see cref="Process"/> takes the input of one input update at once for that reason.
	/// </para>
	/// </remarks>
	internal sealed class PointerPredictor
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

		public PointerPrediction Prediction { get; } = new PointerPrediction();

		/// <summary>
		/// Finds what is under <paramref name="position"/> in the same way as the input module does.
		/// </summary>
		public static PressProbe Probe(Vector2 position)
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
		public bool IsBeyondDragThreshold(Vector2 position)
		{
			if (!_active || _pointerDrag == null || _dragging)
			{
				return false;
			}

			return !_useDragThreshold || ExceedsThreshold(position);
		}

		/// <summary>
		/// Handles what happened to the pointer in one input update.
		/// </summary>
		/// <param name="position">The position at the end of the update.</param>
		/// <param name="pressed">The pointer was pressed in the update.</param>
		/// <param name="released">The pointer was released or canceled in the update.</param>
		/// <param name="lastOrder">The order of the last change of the pointer in the update.</param>
		/// <param name="lastFrame">The frame of the last change of the pointer in the update.</param>
		/// <param name="firstBeyondOrder">
		/// The order of the first change of the update that moved the pointer beyond the drag threshold, or -1.
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
				prediction.PressEvaluated = true;
				prediction.ReleaseEvaluated = false;
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
				prediction.ReleaseEvaluated = true;

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
