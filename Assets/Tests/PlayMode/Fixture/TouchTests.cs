#if ENABLE_AUTOTAP
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using EndReason = UnityAutoTap.AutoTapBase.EndReason;
using LogEventKind = UnityAutoTap.AutoTapBase.LogEventKind;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;
using LogItemKind = UnityAutoTap.AutoTapBase.LogItemKind;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// Fingers: what reaches the UI, in which frame, and what the log items predict about it. Experiments E1 to E3
	/// and E8, and the predictions (<c>Target</c>, <c>ClickTarget</c>, <c>DragTarget</c>, <c>DropTarget</c>)
	/// against what was delivered.
	/// </summary>
	/// <remarks>
	/// The frame arithmetic: <c>Simulate</c> in frame N queues the press; the input update at the start of frame N + 1
	/// processes it and the real input module delivers in that frame (InputSystemUIInputModule.cs <c>Process</c>,
	/// 2381-2420); an operation never presses and releases in one input update, so a gesture without a wait releases
	/// in the input update of frame N + 2.
	/// </remarks>
	public class TouchTests : AutoTapFixture
	{
		[UnityTest]
		public IEnumerator E1_Tap_ClicksTheButton_AndEachHandlerRunsInTheFrameOfItsChange()
		{
			// Delivery order of one tap on a Button (InputSystemUIInputModule.cs): movement first, so pointerEnter comes
			// before pointerDown (521-635); the release gives pointerUp and then pointerClick (707-742). The dragging
			// handler of the recorder makes the module also send initializePotentialDrag after pointerDown (693),
			// which this test does not list.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			var fireFrame = 0;
			yield return Step(() =>
			{
				fireFrame = Frame;
				item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 0f);
			});

			Assert.That(item, Is.Not.Null);
			Assert.That(item.Index, Is.EqualTo(0));
			Assert.That(Rig.Log.Count, Is.Zero, "nothing is delivered inside Simulate");

			LogItem second = null;
			yield return Step(() => second = Auto.SimulateTap(0, Rig.Center(button.gameObject), 0f));
			Assert.That(second, Is.Null, "a slot that is running refuses a new operation");

			yield return Until(() => item.Completed, "the tap to end");
			yield return Steps(2);

			Assert.That(item.StartedFrameCount, Is.EqualTo(fireFrame + 1), "the press is processed by the next frame's input update");
			Assert.That(item.CompletedFrameCount, Is.GreaterThan(item.StartedFrameCount),
				"a press and its release are never in one input update");
			Assert.That(item.EndReason, Is.EqualTo(EndReason.Finished));

			var seen = Rig.Log.Trace(button.gameObject, "pointerEnter", "pointerDown", "pointerUp", "pointerClick", "pointerExit");
			Assert.That(seen, Is.EqualTo(new[]
			{
				"pointerEnter@Button", "pointerDown@Button", "pointerUp@Button", "pointerClick@Button", "pointerExit@Button",
			}), Rig.Log.ToString());

			var down = Rig.Log.First("pointerDown", button.gameObject);
			Assert.That(down.Frame, Is.EqualTo(item.StartedFrameCount));
			Assert.That(Rig.Log.First("pointerUp", button.gameObject).Frame, Is.EqualTo(item.CompletedFrameCount));
			Assert.That(Rig.Log.First("pointerClick", button.gameObject).Frame, Is.EqualTo(item.CompletedFrameCount));

			Assert.That(down.PointerType, Is.EqualTo(UIPointerType.Touch));
			Assert.That(down.PointerId, Is.EqualTo((item.Device.deviceId << 24) + down.TouchId),
				"the pointer id of a finger is (deviceId << 24) + touchId (ExtendedPointerEventData.cs MakePointerIdForTouch)");

			Assert.That(button.ClickCount, Is.EqualTo(1));
			Assert.That(button.States, Does.Contain("Pressed"));
			Assert.That(button.CurrentState, Is.Not.EqualTo("Pressed"));
		}

		[UnityTest]
		public IEnumerator Tap_AtAPointThatHitsNothing_PressesAnyway_AndAtAPointOutsideTheScreenDoesNot()
		{
			// A point where nothing is hit is pressed too; a point outside the screen starts nothing. ShouldNotPressOn is
			// not asked when nothing is hit (documented on AutoTapBase.ShouldNotPressOn).
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem onNothing = null;
			LogItem outside = null;
			yield return Step(() =>
			{
				onNothing = Auto.SimulateTap(0, Rig.EmptyPoint, 0f);
				outside = Auto.SimulateTap(1, new Vector2(-5f, -5f), 0f);
			});

			Assert.That(outside, Is.Null, "a point outside the screen");
			Assert.That(onNothing, Is.Not.Null, "a point that hits nothing is pressed");

			yield return Until(() => onNothing.Completed, "the tap to end");
			Assert.That(onNothing.Order, Is.Not.Null, "the press was processed");
			Assert.That(onNothing.Target, Is.Null);
			Assert.That(onNothing.ClickExecuted, Is.False);
			Assert.That(Rig.Log.Count, Is.Zero, "no object received anything");
			Assert.That(Auto.AskedCount, Is.Zero, "ShouldNotPressOn is not asked when nothing is hit");
		}

		[UnityTest]
		public IEnumerator E2_PressAndReleaseInOneInputUpdate_StillClicks()
		{
			// E2. The finger is used directly, which is the way to press and release in one input update. The module
			// reports the button as pressed and released in that frame (PointerModel.cs, 201-246) and sends
			// pointerDown, pointerUp and pointerClick from one Process.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			AcquireSession();

			VirtualFinger finger = null;
			var fireFrame = 0;
			yield return Step(() =>
			{
				fireFrame = Frame;
				finger = Session.NewFinger();
				finger.Press(Rig.Center(button.gameObject));
				finger.Release();
			});
			Assert.That(finger, Is.Not.Null);
			Assert.That(Rig.Log.Count, Is.Zero, "nothing is delivered before the input update");

			yield return Step();

			var seen = Rig.Log.Trace(button.gameObject, "pointerEnter", "pointerDown", "pointerUp", "pointerClick");
			Assert.That(seen, Is.EqualTo(new[] { "pointerEnter@Button", "pointerDown@Button", "pointerUp@Button", "pointerClick@Button" }),
				Rig.Log.ToString());
			Assert.That(Rig.Log.Of("pointerDown").Single().Frame, Is.EqualTo(fireFrame + 1));
			Assert.That(Rig.Log.Of("pointerUp").Single().Frame, Is.EqualTo(fireFrame + 1));
			Assert.That(Rig.Log.Of("pointerClick").Single().Frame, Is.EqualTo(fireFrame + 1));
			Assert.That(button.ClickCount, Is.EqualTo(1));
			Assert.That(finger.State, Is.EqualTo(FingerState.Released));
			Assert.That(finger.PressedFrame, Is.EqualTo(fireFrame + 1));
			Assert.That(finger.ReleasedFrame, Is.EqualTo(fireFrame + 1));
		}

		[UnityTest]
		public IEnumerator E3_TwoFingers_AreTwoPointers_AndClickTheirOwnButtons()
		{
			// E3 without the game-specific parts. A is pressed, then B, then A is released, then B: two slots with
			// different durations. The pointer ids differ because the touch ids do (ExtendedPointerEventData.cs
			// MakePointerIdForTouch), and the module does not merge touch pointers (InputSystemUIInputModule.cs, 1906-1907).
			yield return BuildRig();
			var a = Rig.AddButton("A", Rig.Cell(0, 1));
			var b = Rig.AddButton("B", Rig.Cell(2, 1));
			yield return Settle();
			StartAuto();

			LogItem itemA = null;
			LogItem itemB = null;
			yield return Step(() =>
			{
				itemA = Auto.SimulateTap(0, Rig.Center(a.gameObject), 0.25f);
				itemB = Auto.SimulateTap(1, Rig.Center(b.gameObject), 0.55f);
			});
			Assert.That(itemA, Is.Not.Null);
			Assert.That(itemB, Is.Not.Null);

			yield return Until(() => itemA.Completed && itemB.Completed, "both taps to end");
			yield return Steps(2);

			var downA = Rig.Log.First("pointerDown", a.gameObject);
			var downB = Rig.Log.First("pointerDown", b.gameObject);
			Assert.That(itemA.Device, Is.SameAs(itemB.Device), "both fingers belong to the one virtual touchscreen");
			Assert.That(downA.TouchId, Is.Not.EqualTo(downB.TouchId));
			Assert.That(downA.PointerId, Is.Not.EqualTo(downB.PointerId));
			Assert.That(downA.PointerId, Is.EqualTo((itemA.Device.deviceId << 24) + downA.TouchId));
			Assert.That(downB.PointerId, Is.EqualTo((itemB.Device.deviceId << 24) + downB.TouchId));
			Assert.That(downA.PointerType, Is.EqualTo(UIPointerType.Touch));
			Assert.That(downB.PointerType, Is.EqualTo(UIPointerType.Touch));

			Assert.That(a.ClickCount, Is.EqualTo(1));
			Assert.That(b.ClickCount, Is.EqualTo(1));

			var upA = Rig.Log.First("pointerUp", a.gameObject);
			var upB = Rig.Log.First("pointerUp", b.gameObject);
			Assert.That(downB.Sequence, Is.LessThan(upA.Sequence), "B was pressed while A was still down");
			Assert.That(upA.Frame, Is.LessThan(upB.Frame), "A is released before B");
		}

		[UnityTest]
		public IEnumerator OnTapCompleted_IsCalledInTheOrderOfTheEnds_BeforeTheSlotsAreVisited()
		{
			// The order inside AutoTapBase.Update is OnPreUpdate, OnTapCompleted in CompletedOrder order, the slots, the
			// operations outside the slots, OnPostUpdate (documented on AutoTapBase.Update). Slot 1 ends before slot 0,
			// so the calls are not in the order of the slots.
			yield return BuildRig();
			var a = Rig.AddButton("A", Rig.Cell(0, 1));
			var b = Rig.AddButton("B", Rig.Cell(2, 1));
			yield return Settle();
			StartAuto();

			LogItem slow = null;
			LogItem fast = null;
			yield return Step(() =>
			{
				slow = Auto.SimulateTap(0, Rig.Center(a.gameObject), 0.55f);
				fast = Auto.SimulateTap(1, Rig.Center(b.gameObject), 0.25f);
			});

			yield return Until(() => slow.Completed && fast.Completed, "both taps to end");
			yield return Steps(1);

			Assert.That(Auto.CompletedSlots.Select(c => c.Index), Is.EqualTo(new[] { 1, 0 }));
			Assert.That(fast.CompletedOrder, Is.LessThan(slow.CompletedOrder));

			var trace = Auto.Trace;
			var completed1 = trace.IndexOf("completed:1");
			var completed0 = trace.IndexOf("completed:0");
			Assert.That(completed1, Is.GreaterThanOrEqualTo(0));
			Assert.That(completed1, Is.LessThan(completed0));
			Assert.That(trace.IndexOf("idle:1", completed1), Is.GreaterThan(completed1),
				"the slot is visited, as an empty one, after its operation was reported in the same Update");
			Assert.That(trace.LastIndexOf("preUpdate", completed1), Is.GreaterThanOrEqualTo(0));
			Assert.That(trace.IndexOf("postUpdate", completed1), Is.GreaterThan(completed1));
		}

		[UnityTest]
		public IEnumerator Stop_ReleasesWhatIsPressedInTheNextInputUpdate_AndTheDevicesGoAway()
		{
			// Stop (documented on AutoTapBase.Stop): Current and Enabled are reset at once; the operation ends as
			// Interrupted inside Stop, and OnTapCompleted is called there for a slot; the release (pointerUp and the
			// click that follows it) reaches the UI in the next input update. E8: afterwards nothing stays pressed and no
			// virtual device is left.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto(useSession: false);

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 5f));
			yield return Until(() => item.Order.HasValue, "the press to be processed");
			Assert.That(Rig.Log.CountOf("pointerDown", button.gameObject), Is.EqualTo(1));
			Assert.That(Rig.Log.CountOf("pointerUp"), Is.Zero);
			Assert.That(button.CurrentState, Is.EqualTo("Pressed"));

			Auto.Stop();

			Assert.That(Auto.Enabled, Is.False);
			Assert.That(AutoTapBase.Current, Is.Null);
			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(EndReason.Interrupted));
			Assert.That(Auto.CompletedSlots.Select(c => c.Index), Is.EqualTo(new[] { 0 }),
				"OnTapCompleted of a slot is called from inside Stop");
			Assert.That(Rig.Log.CountOf("pointerUp"), Is.Zero, "the release reaches the UI in the next input update");

			yield return Step();

			Assert.That(Rig.Log.CountOf("pointerUp", button.gameObject), Is.EqualTo(1));
			Assert.That(Rig.Log.CountOf("pointerClick", button.gameObject), Is.EqualTo(1));
			Assert.That(button.ClickCount, Is.EqualTo(1));
			Assert.That(button.CurrentState, Is.Not.EqualTo("Pressed"));

			yield return Until(() => !AnyVirtualDevice(), "the virtual devices to be removed", 20);
			yield return Steps(2);
			Assert.That(button.CurrentState, Is.Not.EqualTo("Pressed"));
		}

		[UnityTest]
		public IEnumerator Stop_EndsRunningOperationsWithoutANumber_AndListsTheirEndsLastInTheNextInputUpdate()
		{
			// Stop ends what is running at once, and no processed change ended it, so CompletedOrder stays null. The
			// releases are processed by the next input update, whose sequence lists those ends, without a number, in the
			// order the operations were started; each end is listed once (documented on AutoTapBase.GetLatestLogs).
			yield return BuildRig();
			var a = Rig.AddButton("A", Rig.Cell(0, 1));
			var b = Rig.AddButton("B", Rig.Cell(2, 1));
			yield return Settle();
			StartAuto();

			LogItem first = null;
			LogItem second = null;
			yield return Step(() =>
			{
				first = Auto.SimulateTap(0, Rig.Center(a.gameObject), 5f);
				second = Auto.SimulateTap(1, Rig.Center(b.gameObject), 5f);
			});
			yield return Until(() => first.Order.HasValue && second.Order.HasValue, "both presses to be processed");

			var stopFrame = Frame;
			Auto.Stop();

			foreach (var item in new[] { first, second })
			{
				Assert.That(item.Completed, Is.True);
				Assert.That(item.EndReason, Is.EqualTo(EndReason.Interrupted));
				Assert.That(item.CompletedOrder, Is.Null, "no processed change ended it");
				Assert.That(item.CompletedFrameCount, Is.EqualTo(stopFrame));
				Assert.That(item.Order, Is.Not.Null, "the press had been processed before Stop");
			}

			yield return Step();

			var logs = LatestLogs();
			Assert.That(logs.Select(l => (l.Item, l.Kind, l.Order)), Is.EqualTo(new[]
			{
				(first, LogEventKind.Completed, (long?)null),
				(second, LogEventKind.Completed, (long?)null),
			}), Describe(logs));
			Assert.That(Rig.Log.CountOf("pointerUp", a.gameObject), Is.EqualTo(1));
			Assert.That(Rig.Log.CountOf("pointerUp", b.gameObject), Is.EqualTo(1));
			Assert.That(first.CompletedOrder, Is.Null, "what the release caused is not attributed to an item that Stop ended");

			yield return Step();
			Assert.That(LatestLogs(), Is.Empty, "the end of an item is listed once");
		}

		[UnityTest]
		public IEnumerator Stop_BeforeThePressWasProcessed_LeavesOrderNull_AndTheEndIsListedInTheNextInputUpdate()
		{
			// Simulate and Stop in one frame: the press and the release that Stop queued are processed together by the next
			// input update. The operation was cut off by then, so the processed press is not attributed to it and Order
			// stays null; its end is listed without a number in that update's sequence.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 5f));
			Assert.That(item, Is.Not.Null);
			Auto.Stop();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(EndReason.Interrupted));
			Assert.That(item.Order, Is.Null);
			Assert.That(Auto.CompletedSlots.Select(c => c.Index), Is.EqualTo(new[] { 0 }));

			yield return Step();

			Assert.That(item.Order, Is.Null, "the press was processed after the operation was cut off");
			Assert.That(item.StartedFrameCount, Is.Zero);
			Assert.That(item.CompletedOrder, Is.Null);
			var logs = LatestLogs();
			Assert.That(logs.Select(l => (l.Item, l.Kind, l.Order)), Is.EqualTo(new[] { (item, LogEventKind.Completed, (long?)null) }),
				Describe(logs));
			Assert.That(Rig.Log.CountOf("pointerDown", button.gameObject), Is.EqualTo(1), "the queued press is processed anyway");
			Assert.That(Rig.Log.CountOf("pointerUp", button.gameObject), Is.EqualTo(1), "and so is the release");
		}

		[UnityTest]
		public IEnumerator Prediction_Tap_MatchesWhatWasDelivered()
		{
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 0f));
			yield return Until(() => item.Completed, "the tap to end");

			var down = Rig.Log.First("pointerDown");
			var click = Rig.Log.First("pointerClick");
			Assert.That(down, Is.Not.Null);
			Assert.That(click, Is.Not.Null);
			Assert.That(item.Target, Is.SameAs(down.Target), "Target is the object that received pointerDown");
			Assert.That(item.ClickExecuted, Is.True);
			Assert.That(item.ClickTarget, Is.SameAs(click.Target));
			Assert.That(item.Dragged, Is.False);
			Assert.That(item.DragStartedOrder, Is.Null);
			Assert.That(item.DropTarget, Is.Null);
		}

		[UnityTest]
		public IEnumerator Prediction_Tap_DragTarget_IsWhatThePressFoundForADrag_AlsoWhenTheOperationDidNotDrag()
		{
			// DragTarget is the object that would receive begin-drag, drag and end-drag if the pointer is dragged. The
			// input module decides it when the pointer is pressed (that object receives initializePotentialDrag then), so
			// it is set for a tap as well; Dragged tells whether the pointer was dragged, and DropTarget stays empty
			// because no drop is delivered for a pointer that did not drag.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 0f));
			yield return Until(() => item.Completed, "the tap to end");

			var initialize = Rig.Log.First("initializePotentialDrag");
			Assert.That(initialize, Is.Not.Null, Rig.Log.ToString());
			Assert.That(item.DragTarget, Is.SameAs(initialize.Target), "the object that received initializePotentialDrag");
			Assert.That(item.DragTarget, Is.SameAs(button.gameObject));
			Assert.That(item.Dragged, Is.False);
			Assert.That(item.DragStartedOrder, Is.Null);
			Assert.That(Rig.Log.CountOf("beginDrag"), Is.Zero, Rig.Log.ToString());
			Assert.That(item.DropTarget, Is.Null);
		}

		[UnityTest]
		public IEnumerator Prediction_PressOnAChild_TargetsTheNearestHandler()
		{
			// ExecuteHierarchy goes up from the hit object to the first handler (InputSystemUIInputModule.cs, 664).
			yield return BuildRig();
			var parent = Rig.AddButton("Parent", Rig.Cell(1, 1));
			var child = Rig.Add("Child", new Rect(0.2f, 0.2f, 0.6f, 0.6f), parent.transform, record: false);
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(child), 0f));
			yield return Until(() => item.Completed, "the tap to end");

			Assert.That(item.Target, Is.SameAs(parent.gameObject));
			Assert.That(Rig.Log.First("pointerDown").Target, Is.SameAs(parent.gameObject));
			Assert.That(item.ClickTarget, Is.SameAs(parent.gameObject));
			Assert.That(Rig.Log.First("pointerClick").Target, Is.SameAs(parent.gameObject));
			Assert.That(parent.ClickCount, Is.EqualTo(1));
		}

		[UnityTest]
		public IEnumerator Prediction_PressOnOneButtonAndReleaseOnAnother_PredictsNoClick()
		{
			// The module clicks only if the click handler at the release is the one found at the press
			// (InputSystemUIInputModule.cs, 707). Buttons without a recorder have no drag handler, so nothing is
			// dragged either.
			yield return BuildRig();
			var a = Rig.AddButton("A", Rig.Cell(0, 1), record: false);
			var b = Rig.AddButton("B", Rig.Cell(2, 1), record: false);
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateGesture(0, Gesture.PressAt(Rig.Center(a.gameObject)).MoveTo(Rig.Center(b.gameObject), 0.3f)));
			yield return Until(() => item.Completed, "the gesture to end");
			yield return Steps(2);

			Assert.That(a.ClickCount, Is.Zero);
			Assert.That(b.ClickCount, Is.Zero);
			Assert.That(item.ClickExecuted, Is.False);
			Assert.That(item.ClickTarget, Is.Null);
			Assert.That(item.Dragged, Is.False);
			Assert.That(item.DragTarget, Is.Null);
			Assert.That(item.Target, Is.SameAs(a.gameObject));
		}

		[UnityTest]
		public IEnumerator Prediction_Drag_MatchesWhatWasDelivered_AndDragStartAndEndAreDetectedInTheSameFrames()
		{
			// Release order of a drag (InputSystemUIInputModule.cs, 707-742): pointerUp, then drop on the object under
			// the pointer, then endDrag. beginDrag is sent by the move that passes pixelDragThreshold (752-788).
			yield return BuildRig();
			var source = Rig.Add("Source", Rig.Cell(0, 0));
			var zone = Rig.Add("DropZone", Rig.Cell(3, 2));
			yield return Settle();
			AcquireSession();
			StartAuto();
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			CollectLogs = true;

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateGesture(0,
				Gesture.PressAt(Rig.Center(source)).MoveTo(Rig.Center(zone), 0.4f)));
			yield return Until(() => item.Completed, "the drag to end");
			yield return Steps(2);

			Assert.That(Rig.Log.Trace("pointerDown", "beginDrag", "drag", "pointerUp", "drop", "endDrag"), Is.EqualTo(new[]
			{
				"pointerDown@Source", "beginDrag@Source", "drag@Source", "pointerUp@Source", "drop@DropZone", "endDrag@Source",
			}), Rig.Log.ToString());
			Assert.That(Rig.Log.CountOf("pointerClick"), Is.Zero);

			// What was predicted is what was delivered.
			Assert.That(item.Target, Is.SameAs(Rig.Log.First("pointerDown").Target));
			Assert.That(item.Dragged, Is.True);
			Assert.That(item.DragTarget, Is.SameAs(Rig.Log.First("beginDrag").Target));
			Assert.That(item.DropTarget, Is.SameAs(Rig.Log.First("drop").Target));
			Assert.That(item.ClickExecuted, Is.False);
			Assert.That(item.ClickTarget, Is.Null);

			// The start and the end of the drag are detected in the frame the handlers ran in.
			Assert.That(item.DragStartedFrameCount, Is.EqualTo(Rig.Log.First("beginDrag").Frame));
			Assert.That(item.CompletedFrameCount, Is.EqualTo(Rig.Log.First("endDrag").Frame));
			Assert.That(item.CompletedFrameCount, Is.EqualTo(Rig.Log.First("drop").Frame));

			// DragStartedOrder is the first move that passes the drag threshold. The change numbers of this reader
			// are the numbers of the log items because the auto tap operates this session.
			var threshold = Rig.EventSystem.pixelDragThreshold;
			var first = changes.Of<TouchMoved>()
				.First(c => (c.Position - c.StartPosition).sqrMagnitude >= threshold * threshold);
			Assert.That(item.DragStartedOrder, Is.EqualTo(first.Order));
			Assert.That(item.DragStartedOrder, Is.GreaterThan(item.Order));
			Assert.That(item.CompletedOrder, Is.GreaterThan(item.DragStartedOrder));

			// The same facts, as GetLatestLogs reported them frame by frame.
			var started = LogTrace.Single(t => t.Event.Kind == LogEventKind.Started);
			var dragStarted = LogTrace.Single(t => t.Event.Kind == LogEventKind.DragStarted);
			var completed = LogTrace.Single(t => t.Event.Kind == LogEventKind.Completed);
			Assert.That(started.Frame, Is.EqualTo(item.StartedFrameCount));
			Assert.That(dragStarted.Frame, Is.EqualTo(Rig.Log.First("beginDrag").Frame));
			Assert.That(completed.Frame, Is.EqualTo(Rig.Log.First("endDrag").Frame));
			Assert.That(dragStarted.Event.Order, Is.EqualTo(item.DragStartedOrder));
			Assert.That(completed.Event.Order, Is.EqualTo(item.CompletedOrder));
		}

		[UnityTest]
		public IEnumerator ShouldNotPressOn_GetsTheTopmostHit_AndTheObjectsBehindAreNotTried()
		{
			// The argument of ShouldNotPressOn is the first result of the module's own raycast (FindFirstRaycast,
			// InputSystemUIInputModule.cs, 418). When it returns true the first press is refused, and nothing is delivered
			// to the objects behind.
			yield return BuildRig();
			var behind = Rig.AddButton("Behind", Rig.Cell(1, 1));
			var blocker = Rig.Add("Blocker", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();
			Auto.Refuse = hit => hit.gameObject == blocker;

			LogItem refused = null;
			yield return Step(() => refused = Auto.SimulateTap(0, Rig.Center(blocker), 0f));
			Assert.That(refused, Is.Null);
			Assert.That(Auto.AskedCount, Is.EqualTo(1));
			Assert.That(Auto.LastTopmost.gameObject, Is.SameAs(blocker));
			Assert.That(Auto.LastTopmost.module, Is.InstanceOf<GraphicRaycaster>());

			yield return Steps(3);
			Assert.That(Rig.Log.Count, Is.Zero, "nothing was pressed, in front or behind");
			Assert.That(behind.ClickCount, Is.Zero);
			Assert.That(LatestLogs(), Is.Empty);

			// A press that is not refused goes on as usual.
			Auto.Refuse = null;
			LogItem accepted = null;
			yield return Step(() => accepted = Auto.SimulateTap(0, Rig.Center(blocker), 0f));
			Assert.That(accepted, Is.Not.Null);
			yield return Until(() => accepted.Completed, "the tap to end");
			Assert.That(Rig.Log.First("pointerDown").Target, Is.SameAs(blocker));
			Assert.That(accepted.Target, Is.SameAs(blocker));
		}

		[UnityTest]
		public IEnumerator ShouldNotPressOn_InTheMiddleOfAGesture_EndsItAsRefused_AndReleasesWhatWasPressed()
		{
			// For a press after the first one, a refusal makes the operation release what it holds and end with Refused.
			// A mouse presses left at A, moves to B, and would press right there.
			yield return BuildRig();
			var a = Rig.Add("A", Rig.Cell(0, 1));
			var b = Rig.Add("B", Rig.Cell(2, 1));
			yield return Settle();
			StartAuto();
			Auto.Refuse = hit => hit.gameObject == b;

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateMouseGesture(Gesture
				.PressAt(Rig.Center(a))
				.MoveTo(Rig.Center(b), 0.2f)
				.Press("rightButton")
				.Hold(0.2f)));
			Assert.That(item, Is.Not.Null, "the first press is at A, which is not refused");

			yield return Until(() => item.Completed, "the gesture to end");
			yield return Steps(2);

			Assert.That(item.EndReason, Is.EqualTo(EndReason.Refused));
			Assert.That(Rig.Log.CountOf("pointerDown", b), Is.Zero, "the refused press was not made");
			Assert.That(Rig.Log.CountOf("pointerDown", a), Is.EqualTo(1));
			Assert.That(Rig.Log.Of("pointerUp", a).Count(), Is.EqualTo(1), "what was pressed is released");
		}

		[UnityTest]
		public IEnumerator NewLogItem_IsUsedForEveryAcceptedOperation()
		{
			// NewLogItem(index) is called once per accepted operation, with -1 outside the slots (documented on
			// AutoTapBase.NewLogItem).
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem tap = null;
			LogItem key = null;
			yield return Step(() =>
			{
				tap = Auto.SimulateTap(2, Rig.Center(button.gameObject), 0f);
				key = Auto.SimulateKeyPress(UnityEngine.InputSystem.Key.Space);
			});

			Assert.That(tap, Is.TypeOf<TestLogItem>());
			Assert.That(key, Is.TypeOf<TestLogItem>());
			Assert.That(Auto.CreatedItems, Is.EqualTo(new[] { 2, -1 }));
			Assert.That(tap.Kind, Is.EqualTo(LogItemKind.Finger));
			Assert.That(tap.Index, Is.EqualTo(2));
			Assert.That(key.Kind, Is.EqualTo(LogItemKind.Device));
			Assert.That(key.Index, Is.EqualTo(-1));
			yield return Until(() => tap.Completed && key.Completed, "both operations to end");
		}
	}
}
#endif
