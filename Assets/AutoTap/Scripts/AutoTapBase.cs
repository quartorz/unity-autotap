#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityAutoTap
{
	/// <summary>
	/// The base class of an auto tap. It operates virtual input devices as planned by what the subclass fires, and
	/// the real <see cref="UnityEngine.InputSystem.UI.InputSystemUIInputModule"/> of the scene delivers the input to
	/// the UI.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Requirements:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// <see cref="UnityEngine.InputSystem.InputSettings.updateMode"/> is
	/// <see cref="UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsInDynamicUpdate"/>.
	/// </description></item>
	/// <item><description>
	/// The event system uses <see cref="UnityEngine.InputSystem.UI.InputSystemUIInputModule"/>.
	/// </description></item>
	/// </list>
	/// <para>
	/// Input is never delivered inside <c>Fire</c> or <see cref="Update(float)"/>. It is queued, processed by the
	/// input update at the start of the next frame, and delivered to the UI by the event system.
	/// </para>
	/// <para>
	/// There are two kinds of operations:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// Slots. Slot <c>i</c> holds at most one finger at a time, started with <c>Fire(i, ...)</c>. The number of
	/// slots is the tap count passed to <see cref="SetupInternal"/>, and at most 10 fingers exist in total.
	/// </description></item>
	/// <item><description>
	/// Operations outside the slots, started with <see cref="FirePointer"/>, <see cref="FireDevice"/> and their
	/// shortcuts. Each virtual device holds at most one at a time.
	/// </description></item>
	/// </list>
	/// </remarks>
	public abstract partial class AutoTapBase : IDisposable
	{
		/// <summary>
		/// The instance that is started, or <c>null</c>.
		/// </summary>
		public static AutoTapBase Current { get; private set; }

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

		/// <summary>
		/// <c>true</c> from <see cref="Start"/> until <see cref="Stop"/>.
		/// </summary>
		public bool Enabled { get; private set; }

		readonly IVirtualInput _input;

		readonly Vector3[] _fourCorners = new Vector3[4];
		readonly float[] _floats = new float[4];

		/// <summary>
		/// Creates an auto tap that uses <see cref="VirtualInput"/>: the virtual devices are acquired by
		/// <see cref="Start"/> and given up by <see cref="Stop"/>.
		/// </summary>
		protected AutoTapBase()
		{
		}

		/// <summary>
		/// Creates an auto tap that operates the devices of <paramref name="input"/>. The caller owns
		/// <paramref name="input"/>: this class never disposes it. Meant for replacing the Input System in tests.
		/// </summary>
		/// <param name="input">The source of the virtual devices.</param>
		protected AutoTapBase(IVirtualInput input)
		{
			_input = input;
		}

		/// <summary>
		/// Prepares the slots and their markers.
		/// </summary>
		/// <param name="canvasForMarker">The canvas that shows the markers of the operations.</param>
		/// <param name="markerSprite">The sprite of a marker.</param>
		/// <param name="tapCount">
		/// The number of slots, from 1 to 10
		/// (<see cref="UnityEngine.InputSystem.LowLevel.TouchscreenState.MaxTouches"/>).
		/// </param>
		protected virtual void SetupInternal(
			Canvas canvasForMarker, Sprite markerSprite, int tapCount)
		{
			throw new NotImplementedException();
		}

		/// <remarks>
		/// needs to call <see cref="SetupInternal"/>.
		/// </remarks>
		/// <param name="canvasForMarker">canvas to show tap markers</param>
		/// <param name="markerSprite">sprite for tap marker</param>
		public abstract void Setup(Canvas canvasForMarker, Sprite markerSprite);

		/// <summary>
		/// Calls <see cref="Stop"/> if this instance is started, and releases what it holds.
		/// </summary>
		public virtual void Dispose()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Makes this instance <see cref="Current"/> and enables it. Calling it on the current instance does
		/// nothing.
		/// </summary>
		/// <exception cref="InvalidOperationException">Another instance is the current one.</exception>
		public void Start()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Called by <see cref="Start"/> after this instance became <see cref="Current"/>.
		/// </summary>
		protected virtual void OnStart()
		{
		}

		/// <summary>
		/// Stops this instance.
		/// </summary>
		/// <remarks>
		/// <para>
		/// <see cref="Current"/> and <see cref="Enabled"/> are reset immediately. Running operations are ended in the
		/// order they were started: whatever they hold pressed is released, and their <see cref="LogItem"/>s end with
		/// <see cref="EndReason.Interrupted"/>. <see cref="OnTapCompleted"/> is called for those that were started in
		/// a slot.
		/// </para>
		/// <para>
		/// The releases reach the UI in the next input update, not inside this method.
		/// </para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">This instance is not the current one.</exception>
		public void Stop()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Called by <see cref="Stop"/> after <see cref="Current"/> was reset.
		/// </summary>
		protected virtual void OnStop()
		{
		}

		/// <summary>
		/// Called when an operation that was started in a slot ended.
		/// </summary>
		/// <remarks>
		/// <para>
		/// It is called from <see cref="Update(float)"/> before the slots are visited, in the order the ends were
		/// processed (<see cref="LogItem.CompletedOrder"/>). Operations that were ended by <see cref="Stop"/> are
		/// reported from inside <see cref="Stop"/>. It is not called for operations outside the slots; read their
		/// ends with <see cref="GetLatestLogs"/>.
		/// </para>
		/// <para>
		/// For an operation that includes a drag, <see cref="LogItem.Dragged"/> is <c>true</c> and the
		/// <see cref="LogItem.CompletedOrder"/> of the item is the end of the drag. Whether the click or the drop
		/// was delivered is a prediction at that point (<see cref="LogItem.ClickExecuted"/>,
		/// <see cref="LogItem.DropTarget"/>).
		/// </para>
		/// </remarks>
		/// <param name="index">The slot.</param>
		/// <param name="logItem">The operation that ended.</param>
		protected virtual void OnTapCompleted(int index, LogItem logItem)
		{
		}

		/// <summary>
		/// Returns what happened in the latest Dynamic input update, as one sequence in the order the Input System
		/// processed it.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Each element is a <see cref="LogItem"/>, what happened to it (<see cref="LogEventKind"/>) and the sequence
		/// number of the processed change that caused it. When several things happened to one item in one input
		/// update, they are several elements.
		/// </para>
		/// <para>
		/// The sequence is renewed at the start of each Dynamic input update, not at the start of
		/// <see cref="Update(float)"/>. An operation is processed by the input update at the start of a frame, so it
		/// can be read in <see cref="OnPreUpdate"/>, in <see cref="Update(int)"/>, in <see cref="OnPostUpdate"/> and
		/// after <see cref="Update(float)"/> of that frame, until the next frame's input update.
		/// </para>
		/// <para>
		/// An operation fired in frame N is queued by <c>Fire</c> and processed by the input update of frame N + 1,
		/// so it is in the sequence of frame N + 1. The <see cref="LogItem"/> that <c>Fire</c> returns is returned at
		/// once, before any of this.
		/// </para>
		/// <para>
		/// Elements are ordered by <see cref="LogEvent.Order"/>. Elements without an order come after those that
		/// have one: <see cref="LogEventKind.Completed"/> of items that were dropped
		/// (<see cref="EndReason.Dropped"/>) at the end of that input update, in the order the items were fired.
		/// </para>
		/// </remarks>
		public IEnumerable<LogEvent> GetLatestLogs()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Runs the auto tap for one frame. Call it once per frame; it does nothing while not <see cref="Enabled"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The work is done in this order:
		/// </para>
		/// <list type="number">
		/// <item><description><see cref="OnPreUpdate"/>.</description></item>
		/// <item><description>
		/// <see cref="OnTapCompleted"/> for the slot operations that ended, in the order of
		/// <see cref="LogItem.CompletedOrder"/>.
		/// </description></item>
		/// <item><description>
		/// Each slot in turn: a running operation is advanced and its next step queued; an empty slot gets
		/// <see cref="Update(int)"/>.
		/// </description></item>
		/// <item><description>
		/// The running operations outside the slots, in the order they were started, are advanced.
		/// </description></item>
		/// <item><description><see cref="OnPostUpdate"/>.</description></item>
		/// </list>
		/// </remarks>
		/// <param name="deltaTime">The time since the previous call, in seconds.</param>
		public void Update(float deltaTime)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Called at the start of <see cref="Update(float)"/>.
		/// </summary>
		/// <param name="deltaTime">The time passed to <see cref="Update(float)"/>.</param>
		protected virtual void OnPreUpdate(float deltaTime)
		{
		}

		/// <summary>
		/// Called at the end of <see cref="Update(float)"/>.
		/// </summary>
		/// <param name="deltaTime">The time passed to <see cref="Update(float)"/>.</param>
		protected virtual void OnPostUpdate(float deltaTime)
		{
		}

		/// <summary>
		/// Called once per <see cref="Update(float)"/> for each slot that is empty. Start an operation with
		/// <c>Fire(index, ...)</c>.
		/// </summary>
		/// <param name="index">The empty slot.</param>
		protected virtual void Update(int index)
		{
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

			var x = Mathf.LerpUnclamped(
				Mathf.Min(_floats),
				Mathf.Max(_floats),
				tx);

			for (var i = 0; i < 4; ++i)
			{
				_floats[i] = _fourCorners[i].y;
			}

			var y = Mathf.LerpUnclamped(
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

			var x = Mathf.LerpUnclamped(
				Mathf.Min(_floats),
				Mathf.Max(_floats),
				tx);

			for (var i = 0; i < 4; ++i)
			{
				_floats[i] = _fourCorners[i].y;
			}

			var y = Mathf.LerpUnclamped(
				Mathf.Min(_floats),
				Mathf.Max(_floats),
				ty);

			return new Vector2(x, y);
		}
	}
}
#endif
