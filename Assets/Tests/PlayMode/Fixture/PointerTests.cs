#if ENABLE_AUTOTAP
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// Pointers other than fingers: hover, the buttons of a mouse, a pen, scroll, and several pointers at once.
	/// Experiments M1, M2, M3, P1 and W1.
	/// </summary>
	public class PointerTests : AutoTapFixture
	{
		[UnityTest]
		public IEnumerator M1_AMouseThatWasOnlyAdded_HoversNothing_UntilItMoves()
		{
			// M1. The module makes a pointer from the callback of an action (InputSystemUIInputModule.cs, 1837-2000,
			// 2146-2251), and an action does not tell about a control that still has its default value
			// (InputActionState.cs, 1324). A mouse that was only added sits at (0, 0) with the default position, so a
			// background that covers the whole screen is not entered. When the mouse moves, the module raycasts every
			// frame and delivers enter and exit without any press (520-635), and the Selectable under it is highlighted
			// (Selectable.cs, 1273-1327).
			yield return BuildRig();
			Rig.AddBackground();
			var target = Rig.AddSelectable("Hover", Rig.Cell(1, 1));
			yield return Settle();
			AcquireSession();

			var mouse = Session.GetPointer("Mouse");
			yield return Steps(3);
			Assert.That(Rig.Log.Count, Is.Zero, Rig.Log.ToString());
			Assert.That(target.CurrentState, Is.EqualTo("Normal"));

			yield return Step(() => mouse.MoveTo(Rig.Center(target.gameObject)));
			Assert.That(Rig.Log.Count, Is.Zero, "nothing is delivered before the input update");
			yield return Step();

			Assert.That(Rig.Log.CountOf("pointerEnter", target.gameObject), Is.EqualTo(1), Rig.Log.ToString());
			Assert.That(Rig.Log.Of("pointerEnter", target.gameObject).Single().PointerType, Is.EqualTo(UIPointerType.MouseOrPen));
			Assert.That(target.CurrentState, Is.EqualTo("Highlighted"));

			yield return Step(() => mouse.MoveTo(Rig.EmptyPoint));
			yield return Step();

			Assert.That(Rig.Log.CountOf("pointerExit", target.gameObject), Is.EqualTo(1));
			Assert.That(target.CurrentState, Is.EqualTo("Normal"));
			Assert.That(Rig.Log.CountOf("pointerDown"), Is.Zero, "hovering does not press");
		}

		IEnumerator PressWithMouseButton(string buttonPath, PointerEventData.InputButton expected, int expectedClicks)
		{
			// Left, right and middle go through the same code of the module and differ by eventData.button
			// (InputSystemUIInputModule.cs, 494-506). A Button reacts to the left button only (Button.cs, 112;
			// Selectable.cs, 1203, 1243).
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.FireMouseGesture(Gesture.PressAt(Rig.Center(button.gameObject), buttonPath).Hold(0.2f)));
			Assert.That(item, Is.Not.Null);
			yield return Until(() => item.Completed, "the press to end");
			yield return Steps(1);

			var seen = Rig.Log.Trace(button.gameObject, "pointerDown", "pointerUp", "pointerClick");
			Assert.That(seen, Is.EqualTo(new[] { "pointerDown@Button", "pointerUp@Button", "pointerClick@Button" }), Rig.Log.ToString());
			foreach (var name in new[] { "pointerDown", "pointerUp", "pointerClick" })
			{
				var call = Rig.Log.First(name, button.gameObject);
				Assert.That(call.Button, Is.EqualTo(expected), name);
				Assert.That(call.PointerType, Is.EqualTo(UIPointerType.MouseOrPen), name);
				Assert.That(call.PointerId, Is.EqualTo(item.Device.deviceId), "a mouse is one pointer, numbered by its device id");
			}

			Assert.That(button.ClickCount, Is.EqualTo(expectedClicks), "onClick");
			Assert.That(button.States.Contains("Pressed"), Is.EqualTo(expectedClicks > 0), "pressed look");

			// The prediction does not depend on the button: the module clicks with any of them.
			Assert.That(item.Target, Is.SameAs(button.gameObject));
			Assert.That(item.ClickExecuted, Is.True);
			Assert.That(item.ClickTarget, Is.SameAs(Rig.Log.First("pointerClick").Target));
		}

		[UnityTest]
		public IEnumerator M2_LeftButton_ClicksTheButton()
		{
			yield return PressWithMouseButton("leftButton", PointerEventData.InputButton.Left, 1);
		}

		[UnityTest]
		public IEnumerator M2_NoButtonGiven_IsTheLeftButton()
		{
			// null selects the control whose usage is PrimaryAction: the left button of a mouse.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.FireMouseGesture(Gesture.PressAt(Rig.Center(button.gameObject)).Hold(0.2f)));
			yield return Until(() => item.Completed, "the press to end");

			Assert.That(Rig.Log.First("pointerDown", button.gameObject).Button, Is.EqualTo(PointerEventData.InputButton.Left));
			Assert.That(button.ClickCount, Is.EqualTo(1));
		}

		[UnityTest]
		public IEnumerator M2_RightButton_DeliversDownUpClickAsRight_ButTheButtonDoesNotClick()
		{
			yield return PressWithMouseButton("rightButton", PointerEventData.InputButton.Right, 0);
		}

		[UnityTest]
		public IEnumerator M2_MiddleButton_DeliversDownUpClickAsMiddle_ButTheButtonDoesNotClick()
		{
			yield return PressWithMouseButton("middleButton", PointerEventData.InputButton.Middle, 0);
		}

		[UnityTest]
		public IEnumerator M2_ForwardAndBackButtons_AreNotDelivered()
		{
			// The default actions have no binding for them (DefaultInputActions.inputactions), so the module sees
			// nothing, although the press is processed by the Input System.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem forward = null;
			yield return Step(() => forward = Auto.FireMouseGesture(Gesture.PressAt(Rig.Center(button.gameObject), "forwardButton").Hold(0.2f)));
			yield return Until(() => forward.Completed, "the press to end");

			LogItem back = null;
			yield return Step(() => back = Auto.FireMouseGesture(Gesture.PressAt(Rig.Center(button.gameObject), "backButton").Hold(0.2f)));
			yield return Until(() => back.Completed, "the press to end");
			yield return Steps(1);

			Assert.That(forward.Order, Is.Not.Null, "processed");
			Assert.That(back.Order, Is.Not.Null, "processed");
			Assert.That(Rig.Log.CountOf("pointerDown"), Is.Zero, Rig.Log.ToString());
			Assert.That(button.ClickCount, Is.Zero);
		}

		[UnityTest]
		public IEnumerator M2_RightButtonDrag_DeliversTheDragAsRight()
		{
			// The module runs the drag detection once per button (InputSystemUIInputModule.cs, 494-506), so a drag with
			// the right button gives beginDrag, drag, drop and endDrag with button Right.
			yield return BuildRig();
			var source = Rig.Add("Source", Rig.Cell(0, 0));
			var zone = Rig.Add("Zone", Rig.Cell(3, 2));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.FireMouseGesture(
				Gesture.PressAt(Rig.Center(source), "rightButton").MoveTo(Rig.Center(zone), 0.4f)));
			yield return Until(() => item.Completed, "the drag to end");
			yield return Steps(1);

			var names = new[] { "pointerDown", "beginDrag", "drag", "pointerUp", "drop", "endDrag" };
			Assert.That(Rig.Log.Trace(names), Is.EqualTo(new[]
			{
				"pointerDown@Source", "beginDrag@Source", "drag@Source", "pointerUp@Source", "drop@Zone", "endDrag@Source",
			}), Rig.Log.ToString());
			Assert.That(Rig.Log.Calls.Where(c => names.Contains(c.Name)).All(c => c.Button == PointerEventData.InputButton.Right), Is.True);
			Assert.That(item.Dragged, Is.True);
			Assert.That(item.DragTarget, Is.SameAs(source));
			Assert.That(item.DropTarget, Is.SameAs(zone));
		}

		[UnityTest]
		public IEnumerator M3_FingerMousePenAndAnotherMouse_AreFourPointers_ThatDoNotCancelEachOther()
		{
			// M3 / E6 / W2. A virtual finger, a virtual mouse, a virtual pen and a mouse of the test are down at the
			// same time on four objects. The module does not use AllPointersAsIs by itself (the serialized default is
			// SingleMouseOrPenButMultiTouchAndTrack, InputSystemUIInputModule.cs, 2595); the virtual input switches it
			// on, because otherwise mouse and pen are one pointer and a finger is removed without pointerUp when a mouse
			// or a pen moves (1906-1932, 2292-2334). The pointer id of a mouse or a pen is its device id (1847, 1958).
			yield return BuildRig();
			Assert.That(Rig.Module.pointerBehavior, Is.EqualTo(UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack));
			var a = Rig.Add("Finger", Rig.Cell(0, 2));
			var b = Rig.Add("Mouse", Rig.Cell(1, 2));
			var c = Rig.Add("Pen", Rig.Cell(2, 2));
			var d = Rig.Add("TestMouse", Rig.Cell(3, 2));
			yield return Settle();
			StartAuto();
			var testMouse = AddRealDevice<Mouse>("Mouse", "TestMouse");

			LogItem finger = null;
			LogItem mouse = null;
			LogItem pen = null;
			yield return Step(() =>
			{
				finger = Auto.FireFinger(0, Gesture.PressAt(Rig.Center(a)).Hold(0.45f));
				mouse = Auto.FireMouseGesture(Gesture.PressAt(Rig.Center(b)).Hold(0.45f));
				pen = Auto.FirePenGesture(Gesture.PressAt(Rig.Center(c)).Hold(0.45f));
				Set(testMouse.position, Rig.Center(d));
				Press(testMouse.leftButton);
			});
			Assert.That(new[] { finger, mouse, pen }, Is.All.Not.Null);

			yield return Step();
			Assert.That(Rig.Module.pointerBehavior, Is.EqualTo(UIPointerBehavior.AllPointersAsIs));

			var downs = Rig.Log.Of("pointerDown").ToList();
			Assert.That(downs.Select(x => x.Target.name), Is.EquivalentTo(new[] { "Finger", "Mouse", "Pen", "TestMouse" }), Rig.Log.ToString());
			var ids = downs.Select(x => x.PointerId).ToList();
			Assert.That(ids, Is.Unique);

			var downFinger = Rig.Log.First("pointerDown", a);
			Assert.That(downFinger.PointerType, Is.EqualTo(UIPointerType.Touch));
			Assert.That(downFinger.PointerId, Is.EqualTo((finger.Device.deviceId << 24) + downFinger.TouchId));
			Assert.That(Rig.Log.First("pointerDown", b).PointerId, Is.EqualTo(mouse.Device.deviceId));
			Assert.That(Rig.Log.First("pointerDown", c).PointerId, Is.EqualTo(pen.Device.deviceId));
			Assert.That(Rig.Log.First("pointerDown", d).PointerId, Is.EqualTo(testMouse.deviceId));
			Assert.That(Rig.Log.First("pointerDown", b).PointerType, Is.EqualTo(UIPointerType.MouseOrPen));
			Assert.That(Rig.Log.First("pointerDown", c).PointerType, Is.EqualTo(UIPointerType.MouseOrPen));

			yield return Steps(1);
			Assert.That(Rig.Log.CountOf("pointerUp"), Is.Zero, "nobody was released");
			Assert.That(Rig.Log.CountOf("pointerExit"), Is.Zero, "no pointer was removed");

			yield return Step(() => Release(testMouse.leftButton));
			yield return Until(() => finger.Completed && mouse.Completed && pen.Completed, "the three operations to end");
			yield return Steps(2);

			foreach (var target in new[] { a, b, c, d })
			{
				Assert.That(Rig.Log.CountOf("pointerUp", target), Is.EqualTo(1), $"pointerUp on {target.name}");
				Assert.That(Rig.Log.CountOf("pointerClick", target), Is.EqualTo(1), $"pointerClick on {target.name}");
			}
		}

		[UnityTest]
		public IEnumerator E6_Premise_WithTheDefaultPointerBehavior_AMouseMoveRemovesAFingerFromThePointersWhileItIsDown()
		{
			// The reason why the virtual input switches the pointer behavior, on the plain devices of the test and
			// without the virtual input: with
			// SingleMouseOrPenButMultiTouchAndTrack, the first pointer that changes in a frame decides what survives, and
			// a mouse removes the touch pointers with pointerExit while the finger is pressed down all the time
			// (InputSystemUIInputModule.cs, 2292-2334). The press is not ended for the pointer of the finger: what comes
			// afterwards (a drag and the pointerUp of the release) carries the pointer id of the mouse, so the pointer
			// that got the pointerDown never gets its pointerUp.
			yield return BuildRig();
			var target = Rig.Add("A", Rig.Cell(0, 1));
			yield return Settle();
			var touchscreen = InputSystem.AddDevice<Touchscreen>();
			var mouse = InputSystem.AddDevice<Mouse>();
			Assert.That(Rig.Module.pointerBehavior, Is.EqualTo(UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack));

			BeginTouch(1, Rig.Center(target), queueEventOnly: true, screen: touchscreen);
			yield return Steps(2);
			Assert.That(Rig.Log.CountOf("pointerDown", target), Is.EqualTo(1));
			var fingerPointerId = Rig.Log.First("pointerDown", target).PointerId;
			Assert.That(fingerPointerId, Is.EqualTo((touchscreen.deviceId << 24) + 1));

			Set(mouse.position, new Vector2(Screen.width - 5f, Screen.height - 5f));
			yield return Steps(2);
			Assert.That(Rig.Log.CountOf("pointerExit", target), Is.GreaterThanOrEqualTo(1), "the finger's pointer was removed");

			EndTouch(1, Rig.Center(target), queueEventOnly: true, screen: touchscreen);
			yield return Steps(2);
			Assert.That(Rig.Log.Of("pointerUp").Select(x => x.PointerId), Has.No.Member(fingerPointerId),
				"the pointer of the finger never got its pointerUp\n" + Rig.Log);
		}

		[UnityTest]
		public IEnumerator E6_Premise_WithAllPointersAsIs_AMouseMoveDoesNotTouchAFinger()
		{
			yield return BuildRig();
			var target = Rig.Add("A", Rig.Cell(0, 1));
			yield return Settle();
			Rig.Module.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
			var touchscreen = InputSystem.AddDevice<Touchscreen>();
			var mouse = InputSystem.AddDevice<Mouse>();

			BeginTouch(1, Rig.Center(target), queueEventOnly: true, screen: touchscreen);
			yield return Steps(2);
			Set(mouse.position, new Vector2(Screen.width - 5f, Screen.height - 5f));
			yield return Steps(2);
			Assert.That(Rig.Log.CountOf("pointerExit", target), Is.Zero, "the finger's pointer stays");

			EndTouch(1, Rig.Center(target), queueEventOnly: true, screen: touchscreen);
			yield return Steps(2);
			Assert.That(Rig.Log.CountOf("pointerUp", target), Is.EqualTo(1));
			Assert.That(Rig.Log.CountOf("pointerClick", target), Is.EqualTo(1));
		}

		[UnityTest]
		public IEnumerator W2_ScrollingWhileAFingerIsDown_DoesNotRemoveTheFinger()
		{
			// W2. A scroll counts as a change of the mouse pointer, and with the default pointer behavior a changing
			// mouse pointer removes the pointer of a finger without pointerUp (InputSystemUIInputModule.cs, 2292-2334;
			// PointerModel.cs, 59-66). The virtual input switches the behavior, so the finger stays down until it is
			// released by its own plan.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem finger = null;
			yield return Step(() => finger = Auto.FireTap(0, Rig.Center(button.gameObject), 1.05f));
			yield return Until(() => finger.Order.HasValue, "the press to be processed");

			LogItem scroll = null;
			yield return Step(() => scroll = Auto.FireScrollAt(Rig.EmptyPoint, new Vector2(0f, -1f)));
			Assert.That(scroll, Is.Not.Null);
			yield return Steps(3);
			Assert.That(finger.Completed, Is.False, "the finger is still down");
			Assert.That(Rig.Log.CountOf("pointerUp"), Is.Zero, Rig.Log.ToString());
			Assert.That(Rig.Log.CountOf("pointerExit", button.gameObject), Is.Zero, "its pointer was not removed");
			Assert.That(button.CurrentState, Is.EqualTo("Pressed"));

			yield return Until(() => finger.Completed, "the tap to end", 90);
			yield return Steps(2);
			Assert.That(Rig.Log.CountOf("pointerUp", button.gameObject), Is.EqualTo(1));
			Assert.That(button.ClickCount, Is.EqualTo(1));
		}

		[UnityTest]
		public IEnumerator P1_PenHover_EntersAndExits_EvenThoughInRangeIsZeroAfterTheOperation()
		{
			// P1. The module reads neither inRange nor the tip for a hover: a pen moves like a mouse (Plugins/UI has no
			// use of inRange). The virtual input holds inRange for the length of the operation and returns it to the
			// default afterwards (Gesture documents the return to the defaults), and the position stays, so the hover
			// goes on.
			yield return BuildRig();
			var first = Rig.AddSelectable("First", Rig.Cell(1, 1));
			var second = Rig.AddSelectable("Second", Rig.Cell(2, 1));
			yield return Settle();
			StartAuto();

			LogItem hover = null;
			yield return Step(() => hover = Auto.FirePenGesture(Gesture.HoverAt(Rig.Center(first.gameObject)).Hold(0.2f)));
			Assert.That(hover, Is.Not.Null);
			yield return Until(() => hover.Order.HasValue, "the hover to be processed");
			var pen = (Pen)hover.Device;
			Assert.That(pen.inRange.isPressed, Is.True, "inRange is held while the operation runs");
			Assert.That(Rig.Log.CountOf("pointerEnter", first.gameObject), Is.EqualTo(1), Rig.Log.ToString());
			Assert.That(first.CurrentState, Is.EqualTo("Highlighted"));

			yield return Until(() => hover.Completed, "the hover to end");
			yield return Steps(2);
			Assert.That(pen.inRange.isPressed, Is.False, "inRange is back to its default");
			Assert.That(Rig.Log.CountOf("pointerExit", first.gameObject), Is.Zero, "the pen is still over it");
			Assert.That(first.CurrentState, Is.EqualTo("Highlighted"));

			LogItem move = null;
			yield return Step(() => move = Auto.FirePenGesture(Gesture.HoverAt(Rig.Center(second.gameObject)).Hold(0.2f)));
			yield return Until(() => move.Completed, "the second hover to end");
			Assert.That(Rig.Log.CountOf("pointerExit", first.gameObject), Is.EqualTo(1));
			Assert.That(Rig.Log.CountOf("pointerEnter", second.gameObject), Is.EqualTo(1));
			Assert.That(first.CurrentState, Is.EqualTo("Normal"));
			Assert.That(second.CurrentState, Is.EqualTo("Highlighted"));
			Assert.That(Rig.Log.CountOf("pointerDown"), Is.Zero);
		}

		[UnityTest]
		public IEnumerator P1_PenPressure_ReachesThePointerEventData()
		{
			// ExtendedPointerEventData.ReadDeviceState copies the pressure of the pen at every Process (EPED, 130-138).
			yield return BuildRig();
			var target = Rig.Add("Target", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.FirePenGesture(
				Gesture.HoverAt(Rig.Center(target)).Set("pressure", 0.75f).Press().Hold(0.2f)));
			yield return Until(() => item.Completed, "the press to end");

			var down = Rig.Log.First("pointerDown", target);
			Assert.That(down, Is.Not.Null, Rig.Log.ToString());
			Assert.That(down.Pressure, Is.EqualTo(0.75f).Within(0.001f));
			Assert.That(down.PointerType, Is.EqualTo(UIPointerType.MouseOrPen));
			Assert.That(down.PointerId, Is.EqualTo(item.Device.deviceId));
		}

		[UnityTest]
		public IEnumerator W1_Scroll_MovesAScrollRect_AndTheScrollValueOfTheMouseLastsOneFrame()
		{
			// W1. The module turns the scroll of the mouse into eventData.scrollDelta (InputSystemUIInputModule.cs,
			// 2237-2250; 6 per tick by default) and sends it to the object under the pointer (790-799); ScrollRect moves
			// its content by minus the y of it (ScrollRect.cs, 648-682). The scroll control of the mouse keeps the sum of
			// the events of one update and is zeroed at the start of the next one (FastMouse.partial.cs, 7-12).
			yield return BuildRig();
			var scroll = Rig.AddScrollRect("Scroll", Rig.Cell(1, 1));
			yield return Settle();
			scroll.verticalNormalizedPosition = 0.5f;
			StartAuto();

			var before = scroll.content.anchoredPosition.y;
			LogItem item = null;
			yield return Step(() => item = Auto.FireScrollAt(Rig.Center(scroll.gameObject), new Vector2(0f, -3f)));
			Assert.That(item, Is.Not.Null);
			Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(before), "nothing moved inside Fire");

			yield return Until(() => Rig.Log.CountOf("scroll", scroll.gameObject) > 0, "the scroll to be delivered");

			var virtualMouse = VirtualDeviceOf<Mouse>();
			Assert.That(Mouse.current, Is.SameAs(virtualMouse), "the virtual mouse is current from its first event");
			Assert.That(virtualMouse.scroll.ReadUnprocessedValue().y, Is.Not.Zero, "the scroll is there in the frame it was processed");

			var call = Rig.Log.First("scroll", scroll.gameObject);
			Assert.That(call, Is.Not.Null, Rig.Log.ToString());
			// The hover that puts the pointer over the ScrollRect and the scroll are two events of one input update, and
			// the scroll needs no frame of waiting: the pointer enters the ScrollRect first and the scroll follows in the
			// first frame after Fire, which is also the frame in which the mouse was added to the module.
			Assert.That(call.Frame, Is.EqualTo(item.FrameCount + 1), Rig.Log.ToString());
			Assert.That(Rig.Log.Trace(scroll.gameObject, "pointerEnter", "scroll"), Is.EqualTo(new[] { "pointerEnter@Scroll", "scroll@Scroll" }), Rig.Log.ToString());
			Assert.That(call.ScrollDelta.y, Is.LessThan(0f), "the sign of the scroll is kept");
			Assert.That(scroll.content.anchoredPosition.y - before,
				Is.EqualTo(-call.ScrollDelta.y * scroll.scrollSensitivity).Within(0.01f));
			Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(0.5f), "the content moved up, which shows what is below");

			yield return Step();
			Assert.That(virtualMouse.scroll.ReadUnprocessedValue(), Is.EqualTo(Vector2.zero), "and it is gone in the next frame");
			Assert.That(Rig.Log.CountOf("scroll", scroll.gameObject), Is.EqualTo(1), "delivered once");
		}
	}
}
#endif
