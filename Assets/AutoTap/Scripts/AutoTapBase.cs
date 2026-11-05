#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UnityAutoTap
{
	/// <summary>
	/// The base class of an auto tap. It operates virtual input devices as planned by what the subclass simulates, and
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
	/// Input is never delivered inside <c>Simulate</c> or <see cref="Update(float)"/>. It is queued, processed by the
	/// input update at the start of the next frame, and delivered to the UI by the event system.
	/// </para>
	/// <para>
	/// There are two kinds of operations:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// Slots. Slot <c>i</c> holds at most one finger at a time, started with <c>Simulate(i, gesture)</c>. The
	/// number of slots is given to the constructor, from 1 to 10, the number of touches that a touchscreen has.
	/// </description></item>
	/// <item><description>
	/// Operations outside the slots, started with the other <c>Simulate</c> methods: a pointer such as a mouse or a
	/// pen with a <see cref="Gesture"/>, any other device with a <see cref="ControlSequence"/>, and a key. Each
	/// virtual device holds at most one at a time.
	/// </description></item>
	/// </list>
	/// <para>
	/// The markers that show where the operations are on the screen are optional: <see cref="Update(float)"/> and
	/// <c>Simulate</c> work without <see cref="SetupMarkers"/>.
	/// </para>
	/// </remarks>
	public abstract partial class AutoTapBase : IDisposable
	{
		/// <summary>
		/// The instance that is started, or <c>null</c>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// It is <c>null</c> when the application starts, also when Play Mode is entered without a domain reload, in
		/// which the static state of the earlier Play Mode is kept.
		/// </para>
		/// <para>
		/// When Play Mode is exited or the application quits, the instance is stopped (<see cref="Stop"/>) before the
		/// virtual devices are removed, so it is <c>null</c> after that too.
		/// </para>
		/// </remarks>
		public static AutoTapBase Current { get; private set; }

		/// <summary>
		/// Forgets the instance that an earlier run left as <see cref="Current"/>, when Play Mode is entered without a
		/// domain reload.
		/// </summary>
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetCurrent()
		{
			Current = null;
		}

		/// <summary>
		/// Stops <see cref="Current"/>, if there is one, when Play Mode is exited or the application quits. It is called
		/// while the virtual devices are still there: <see cref="Stop"/> queues the release of what the operations hold,
		/// which throws once the devices were removed.
		/// </summary>
		/// <remarks>
		/// An exception that <see cref="Stop"/> throws, from <see cref="OnStop"/> for example, is logged, so that the
		/// virtual devices are removed after it as well.
		/// </remarks>
		internal static void StopCurrentBeforeExit()
		{
			var current = Current;
			if (current == null)
			{
				return;
			}

			try
			{
				current.Stop();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
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

		/// <summary>
		/// <c>true</c> from <see cref="Start"/> until <see cref="Stop"/>.
		/// </summary>
		public bool Enabled { get; private set; }

		readonly bool _ownsInput;
		IVirtualInput _input;
		VirtualInputSession _session;
		IVirtualInput _subscribedTo;

		FingerOperation[] _slots;
		Image[] _markers;
		Canvas _canvas;
		Sprite _markerSprite;
		readonly Dictionary<VirtualPointer, Image> _pointerMarkers = new Dictionary<VirtualPointer, Image>();
		readonly List<Operation> _operations = new List<Operation>();
		readonly Dictionary<int, FingerOperation> _fingerOperations = new Dictionary<int, FingerOperation>();
		readonly Dictionary<InputDevice, DeviceBoundOperation> _deviceOperations =
			new Dictionary<InputDevice, DeviceBoundOperation>();
		readonly List<Operation> _scratch = new List<Operation>();
		readonly List<DeviceBoundOperation> _boundScratch = new List<DeviceBoundOperation>();
		readonly List<LogEntry> _log = new List<LogEntry>();
		readonly List<LogEntry> _pendingLog = new List<LogEntry>();
		bool _inUpdate;
		int _logVersion;
		long _startSequence;

		readonly Vector3[] _fourCorners = new Vector3[4];
		readonly float[] _floats = new float[4];

		/// <summary>
		/// Creates an auto tap that uses <see cref="VirtualInput"/>: the virtual devices are acquired by
		/// <see cref="Start"/> and given up by <see cref="Stop"/>.
		/// </summary>
		/// <param name="slotCount">
		/// The number of slots, from 1 to 10, the number of touches that a touchscreen has.
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="slotCount"/> is not between 1 and 10.</exception>
		protected AutoTapBase(int slotCount)
		{
			_ownsInput = true;
			Initialize(slotCount);
		}

		/// <summary>
		/// Creates an auto tap that operates the devices of <paramref name="input"/>, a session that the caller shares
		/// with this auto tap. The caller owns <paramref name="input"/>: this class never disposes it, and
		/// <see cref="Start"/> does not acquire another session.
		/// </summary>
		/// <param name="slotCount">
		/// The number of slots, from 1 to 10, the number of touches that a touchscreen has.
		/// </param>
		/// <param name="input">
		/// The session that supplies the virtual devices, as returned by <see cref="VirtualInput.Acquire"/>.
		/// <c>null</c> makes the auto tap acquire its own session, as the other constructor does.
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="slotCount"/> is not between 1 and 10.</exception>
		protected AutoTapBase(int slotCount, IVirtualInput input)
		{
			_ownsInput = input == null;
			_input = input;
			Initialize(slotCount);
		}

		void Initialize(int slotCount)
		{
			if (slotCount < 1 || slotCount > VirtualInputCore.MaxFingers)
			{
				throw new ArgumentOutOfRangeException(
					nameof(slotCount), $"The slot count must be between 1 and {VirtualInputCore.MaxFingers}.");
			}

			_slots = new FingerOperation[slotCount];
		}

		/// <summary>
		/// Builds the log again at the start of <see cref="Update(float)"/>: what was kept since the earlier one
		/// becomes the log, and the sequences that were returned are out of date.
		/// </summary>
		void RebuildLog()
		{
			_log.Clear();
			_log.AddRange(_pendingLog);
			_pendingLog.Clear();
			++_logVersion;
		}

		/// <summary>
		/// Drops the log and what was kept for the next one, and with them the sequences that were returned.
		/// </summary>
		void DiscardLog()
		{
			_log.Clear();
			_pendingLog.Clear();
			++_logVersion;
		}

		/// <summary>
		/// Creates the markers that show where the operations are: one for each slot, and one for each pointer that
		/// is operated. Without markers nothing is shown, and everything else works as it does with them.
		/// </summary>
		/// <remarks>
		/// It can be called at any time, also while running. Calling it again replaces the markers that were made by
		/// the earlier call.
		/// </remarks>
		/// <param name="canvasForMarker">
		/// The canvas that shows the markers of the operations. <c>null</c> shows no marker.
		/// </param>
		/// <param name="markerSprite">The sprite of a marker.</param>
		/// <exception cref="ObjectDisposedException">This instance was disposed.</exception>
		public void SetupMarkers(Canvas canvasForMarker, Sprite markerSprite)
		{
			ThrowIfDisposed();
			DestroyMarkers();
			_canvas = canvasForMarker;
			_markerSprite = markerSprite;
			_markers = new Image[_slots.Length];
			if (canvasForMarker == null)
			{
				return;
			}

			for (var i = 0; i < _slots.Length; ++i)
			{
				var markerGameObject = new GameObject($"Marker {i + 1}");
				markerGameObject.transform.SetParent(canvasForMarker.transform, false);
				markerGameObject.transform.localPosition = Vector3.zero;
				markerGameObject.SetActive(false);
				var marker = markerGameObject.AddComponent<Image>();
				marker.raycastTarget = false;
				marker.sprite = markerSprite;
				marker.SetNativeSize();
				marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = Vector2.zero;
				_markers[i] = marker;
			}
		}

		void ThrowIfDisposed()
		{
			if (_slots == null)
			{
				throw new ObjectDisposedException(GetType().Name);
			}
		}

		/// <summary>
		/// Calls <see cref="Stop"/> if this instance is started, and releases what it holds.
		/// </summary>
		public virtual void Dispose()
		{
			if (Current == this)
			{
				Stop();
			}

			Unsubscribe();
			_session?.Dispose();
			_session = null;
			if (_ownsInput)
			{
				_input = null;
			}

			DestroyMarkers();
			_slots = null;
			_operations.Clear();
			_fingerOperations.Clear();
			_deviceOperations.Clear();
			DiscardLog();
		}

		/// <summary>
		/// Makes this instance <see cref="Current"/> and enables it. Calling it on the current instance does
		/// nothing.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The log is emptied: <see cref="GetLatestLogs"/> returns an empty sequence until the first
		/// <see cref="Update(float)"/> of this run builds it, and the sequences that were returned before are out of date
		/// (see <see cref="LogEventSequence"/>).
		/// </para>
		/// <para>
		/// What an earlier run kept for the log that the next <see cref="Update(float)"/> builds, such as the end of an
		/// operation that <see cref="Stop"/> ended outside <see cref="Update(float)"/>, is discarded, so that it does
		/// not show up in the log of this run.
		/// </para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">Another instance is the current one.</exception>
		/// <exception cref="ObjectDisposedException">This instance was disposed.</exception>
		public void Start()
		{
			if (Current != null)
			{
				if (Current == this)
				{
					return;
				}

				throw new InvalidOperationException("Another instance is started.");
			}

			ThrowIfDisposed();
			if (_ownsInput)
			{
				// Throws if the Input System cannot be driven by virtual input; nothing was changed yet.
				_session = VirtualInput.Acquire();
				_input = _session;
			}

			// What an earlier run left in the log, and what it kept for the next one, does not belong to this run.
			DiscardLog();
			Subscribe();
			Enabled = true;
			Current = this;
			OnStart();
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
		/// <para>
		/// When this method is called inside <see cref="Update(float)"/>, by a scenario for example, the ends of the
		/// operations that it ended are in the sequence that <see cref="GetLatestLogs"/> returns for that
		/// <see cref="Update(float)"/>. When it is called outside, they are not in any sequence: <see cref="Update(float)"/>
		/// does nothing while this instance is not <see cref="Enabled"/>, so no log is built that could take them, and
		/// <see cref="Start"/> discards them. For an operation that was started in a slot,
		/// <see cref="OnTapCompleted"/> is called inside this method in either case.
		/// </para>
		/// <para>
		/// When Play Mode is exited or the application quits while this instance is <see cref="Current"/>, this method
		/// is called before the virtual devices are removed. An exception that it throws then is logged.
		/// </para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">This instance is not the current one.</exception>
		public void Stop()
		{
			if (Current != this)
			{
				throw new InvalidOperationException("This instance is not the current one.");
			}

			Current = null;
			Enabled = false;

			// What ended already but was not reported yet is reported first, in the order it ended.
			NotifyCompleted(true);

			_scratch.Clear();
			_scratch.AddRange(_operations);
			_scratch.Sort((a, b) => a.StartSequence.CompareTo(b.StartSequence));
			var frame = Time.frameCount;
			foreach (var operation in _scratch)
			{
				if (operation.Ended)
				{
					continue;
				}

				operation.Interrupt();
				Complete(operation, EndReason.Interrupted, null, frame);
				AppendEnd(operation);
				if (operation.Index >= 0)
				{
					_slots[operation.Index] = null;
					operation.Notified = true;
					OnTapCompleted(operation.Index, operation.Item);
				}
			}

			_scratch.Clear();
			_operations.Clear();
			_fingerOperations.Clear();
			_deviceOperations.Clear();
			HideMarkers();
			OnStop();

			if (_session != null)
			{
				_session.Dispose();
				_session = null;
			}
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
		/// Returns the log of the latest <see cref="Update(float)"/>: what happened since the one before it, as one
		/// sequence in the order the Input System processed it. It is empty if nothing happened.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Each element is a <see cref="LogItem"/>, what happened to it (<see cref="LogEventKind"/>) and the sequence
		/// number of the processed change that caused it. When several things happened to one item since the earlier
		/// <see cref="Update(float)"/>, they are several elements.
		/// </para>
		/// <para>
		/// The sequence is built again at the start of every <see cref="Update(float)"/> that runs, that is, while this
		/// instance is <see cref="Enabled"/>. <see cref="Start"/> and <see cref="Dispose"/> empty it. Nothing else
		/// changes it, so the same elements are returned until the next <see cref="Update(float)"/> starts, also when
		/// frames pass without a call and after <see cref="Stop"/>.
		/// </para>
		/// <para>
		/// The sequence that is returned is valid only until the next <see cref="Update(float)"/> starts, or until
		/// <see cref="Start"/> or <see cref="Dispose"/> is called. Once that happened, enumerating it, reading its
		/// <see cref="LogEventSequence.Count"/> or an element of it throws
		/// <see cref="InvalidOperationException"/>. Copy what is needed later (<c>ToArray()</c> of LINQ, for example).
		/// Enumerating it with <c>foreach</c> allocates nothing. While <see cref="Update(float)"/> runs, what it causes
		/// is added to the sequence, and a sequence that was returned earlier in the same call shows it too (see
		/// <see cref="LogEventSequence"/>).
		/// </para>
		/// <para>
		/// Which sequence something goes into depends on whether it happens inside <see cref="Update(float)"/>:
		/// </para>
		/// <list type="bullet">
		/// <item><description>
		/// What happens outside <see cref="Update(float)"/> is kept, and becomes the start of the sequence that the next
		/// <see cref="Update(float)"/> builds. That is what the input update at the start of a frame processed (the start
		/// of an operation, the start of its drag, its end), and what happened since the previous
		/// <see cref="Update(float)"/> ended, such as the Input System resetting a device after it, which ends the
		/// operations that use it.
		/// </description></item>
		/// <item><description>
		/// What happens inside <see cref="Update(float)"/>, in <see cref="OnPreUpdate"/>, <see cref="Update(int)"/>,
		/// <see cref="OnPostUpdate"/>, <see cref="OnTapCompleted"/> and in the code that these call, is added to the
		/// sequence at once. That is the end of an operation that <see cref="Stop"/> ended, or that a reset or the
		/// disabling of a device ended, when it is called there. A scenario runs inside <see cref="Update(float)"/>, so
		/// what it does is in the sequence of that call.
		/// </description></item>
		/// </list>
		/// <para>
		/// Every one of these is in one sequence only: it is not listed twice and it is not lost, with the exception in
		/// the last paragraph.
		/// </para>
		/// <para>
		/// An operation started in frame N is queued by <c>Simulate</c> and processed by the input update of frame N + 1,
		/// so it is in the sequence that <see cref="Update(float)"/> builds in frame N + 1. It can be read in
		/// <see cref="OnPreUpdate"/>, in <see cref="Update(int)"/>, in <see cref="OnPostUpdate"/> and after
		/// <see cref="Update(float)"/> of that frame, until <see cref="Update(float)"/> of the next frame. The
		/// <see cref="LogItem"/> that <c>Simulate</c> returns is returned at once, before any of this. The items in the
		/// sequence are the same objects that <c>Simulate</c> returned, and they keep being filled in (see
		/// <see cref="LogItem"/>), so what an item shows when it is read is what is known then, not at the time of the
		/// element.
		/// </para>
		/// <para>
		/// Elements are ordered by <see cref="LogEvent.Order"/>. Elements without an order come after those that
		/// have one, in the order the operations were started. They are <see cref="LogEventKind.Completed"/> of
		/// </para>
		/// <list type="bullet">
		/// <item><description>
		/// items that were dropped (<see cref="EndReason.Dropped"/>) by an input update,
		/// </description></item>
		/// <item><description>
		/// items that <see cref="Stop"/> ended, and
		/// </description></item>
		/// <item><description>
		/// items whose end changed no control, such as a hover that presses nothing or a <c>Set</c> of the value that
		/// a control already has. Such an item has nothing to release, so it ends in the first input update after its
		/// plan is over.
		/// </description></item>
		/// </list>
		/// <para>
		/// The exception is the end of an operation that <see cref="Stop"/> ended outside <see cref="Update(float)"/>: it
		/// is not in any sequence. No <see cref="Update(float)"/> follows that would build one, because the instance is
		/// not <see cref="Enabled"/> any more. For an operation that was started in a slot,
		/// <see cref="OnTapCompleted"/> is called inside <see cref="Stop"/>.
		/// </para>
		/// </remarks>
		/// <returns>The sequence of the latest <see cref="Update(float)"/>.</returns>
		public LogEventSequence GetLatestLogs()
		{
			return new LogEventSequence(this, _logVersion);
		}

		/// <summary>
		/// Runs the auto tap for one frame. Call it once per frame; it does nothing while not <see cref="Enabled"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The work is done in this order:
		/// </para>
		/// <list type="number">
		/// <item><description>
		/// The sequence that <see cref="GetLatestLogs"/> returns is built again: what happened outside this method
		/// since the previous call becomes the log, and the sequences that were returned are out of date.
		/// </description></item>
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
		/// <para>
		/// What happens while this method runs and ends an operation (<see cref="Stop"/>, a reset of a device) is added
		/// to the sequence of this call at once. Nothing is built again while the instance is not
		/// <see cref="Enabled"/>.
		/// </para>
		/// </remarks>
		/// <param name="deltaTime">The time since the previous call, in seconds.</param>
		public void Update(float deltaTime)
		{
			if (!Enabled)
			{
				return;
			}

			RebuildLog();
			var wasInUpdate = _inUpdate;
			_inUpdate = true;
			try
			{
				RunUpdate(deltaTime);
			}
			finally
			{
				_inUpdate = wasInUpdate;
			}
		}

		void RunUpdate(float deltaTime)
		{
			OnPreUpdate(deltaTime);
			if (!Enabled)
			{
				return;
			}

			NotifyCompleted();
			if (!Enabled)
			{
				return;
			}

			for (var i = 0; i < _slots.Length; ++i)
			{
				var operation = _slots[i];
				if (operation != null)
				{
					Advance(operation, deltaTime);
				}
				else
				{
					Update(i);
				}

				if (!Enabled)
				{
					return;
				}
			}

			AdvanceBoundOperations(deltaTime);
			if (!Enabled)
			{
				return;
			}

			OnPostUpdate(deltaTime);
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
		/// <c>Simulate(index, gesture)</c>.
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
