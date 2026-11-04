#if ENABLE_AUTOTAP
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// A virtual gamepad: navigation, submit and cancel through the real input module, and the control scheme of a
	/// <see cref="PlayerInput"/>. Experiments G1 and G2.
	/// </summary>
	/// <remarks>
	/// The module reads the move action with a callback and polls submit and cancel with
	/// <c>WasPerformedThisDynamicUpdate</c> (InputSystemUIInputModule.cs, 800-905). Without a selected object none of
	/// them is delivered. The selection starts from <c>firstSelectedGameObject</c> (231-240).
	/// </remarks>
	public class GamepadTests : AutoTapFixture
	{
		static Navigation Explicit(Selectable up = null, Selectable down = null, Selectable left = null, Selectable right = null)
		{
			return new Navigation
			{
				mode = Navigation.Mode.Explicit,
				selectOnUp = up,
				selectOnDown = down,
				selectOnLeft = left,
				selectOnRight = right,
			};
		}

		[UnityTest]
		public IEnumerator G1_StickDpadSubmitAndCancel_ReachTheSelectedObject()
		{
			CreateRig();
			var a = Rig.AddSelectable("A", Rig.Cell(0, 2));
			var b = Rig.AddSelectable("B", Rig.Cell(0, 1));
			var c = Rig.AddSelectable("C", Rig.Cell(1, 2));
			a.navigation = Explicit(down: b, right: c);
			b.navigation = Explicit(up: a, right: c);
			c.navigation = Explicit(left: a, down: b);
			Rig.EventSystem.firstSelectedGameObject = a.gameObject;
			yield return Settle();
			yield return Steps(2);
			Assert.That(Rig.EventSystem.currentSelectedGameObject, Is.SameAs(a.gameObject), "the module starts from firstSelectedGameObject");

			var cancelAction = new InputAction("cancel", InputActionType.Button, "*/{Cancel}");
			var cancelPerformed = 0;
			cancelAction.performed += _ => ++cancelPerformed;
			cancelAction.Enable();
			OnTearDown(() =>
			{
				cancelAction.Disable();
				cancelAction.Dispose();
			});
			StartAuto();

			// Stick down: A is selected, so A gets a move to Down and the selection goes to B (explicit navigation).
			LogItem stick = null;
			yield return Step(() => stick = Auto.SimulateGamepadSequence(new ControlSequence().Set("leftStick", Vector2.down).Hold(0.25f)));
			Assert.That(stick, Is.Not.Null);
			yield return Until(() => stick.Completed, "the stick to be released");
			var moves = Rig.Log.Of("move").ToList();
			Assert.That(moves, Has.Count.EqualTo(1), Rig.Log.ToString());
			Assert.That(moves[0].Target, Is.SameAs(a.gameObject));
			Assert.That(moves[0].MoveDirection, Is.EqualTo(MoveDirection.Down));
			Assert.That(moves[0].Frame, Is.EqualTo(stick.StartedFrameCount), "delivered in the frame the stick was processed");
			Assert.That(Rig.EventSystem.currentSelectedGameObject, Is.SameAs(b.gameObject));
			Assert.That(Rig.Log.CountOf("select", b.gameObject), Is.EqualTo(1));

			// D-pad right: B gets the move, the selection goes to C.
			LogItem dpad = null;
			yield return Step(() => dpad = Auto.SimulateGamepadSequence(new ControlSequence().Press(GamepadButton.DpadRight).Hold(0.15f)));
			yield return Until(() => dpad.Completed, "the d-pad to be released");
			var rightMove = Rig.Log.Of("move").Last();
			Assert.That(rightMove.Target, Is.SameAs(b.gameObject));
			Assert.That(rightMove.MoveDirection, Is.EqualTo(MoveDirection.Right));
			Assert.That(Rig.EventSystem.currentSelectedGameObject, Is.SameAs(c.gameObject));
			Assert.That(Rig.Log.CountOf("move"), Is.EqualTo(2));

			// South submits, East cancels; both once, in the frame they were processed, and to the selected object.
			LogItem submit = null;
			yield return Step(() => submit = Auto.SimulateGamepadSequence(new ControlSequence().Press(GamepadButton.South).Hold(0.25f)));
			yield return Until(() => submit.Completed, "South to be released");
			Assert.That(Rig.Log.CountOf("submit"), Is.EqualTo(1), "a held button does not repeat submit");
			Assert.That(Rig.Log.First("submit").Target, Is.SameAs(c.gameObject));
			Assert.That(Rig.Log.First("submit").Frame, Is.EqualTo(submit.StartedFrameCount));

			Assert.That(cancelPerformed, Is.Zero);
			LogItem cancel = null;
			yield return Step(() => cancel = Auto.SimulateGamepadSequence(new ControlSequence().Press(GamepadButton.East).Hold(0.25f)));
			yield return Until(() => cancel.Completed, "East to be released");
			Assert.That(Rig.Log.CountOf("cancel"), Is.EqualTo(1), "a held button does not repeat cancel");
			Assert.That(Rig.Log.First("cancel").Target, Is.SameAs(c.gameObject));
			Assert.That(Rig.Log.First("cancel").Frame, Is.EqualTo(cancel.StartedFrameCount));
			Assert.That(cancelPerformed, Is.EqualTo(1), "buttonEast is a source of */{Cancel} as well as Escape");

			// Without a selected object nothing is delivered.
			Rig.EventSystem.SetSelectedGameObject(null);
			yield return Steps(1);
			var before = Rig.Log.Count;
			LogItem silentStick = null;
			yield return Step(() => silentStick = Auto.SimulateGamepadSequence(new ControlSequence().Set("leftStick", Vector2.up).Hold(0.25f)));
			yield return Until(() => silentStick.Completed, "the stick to be released");
			LogItem silentSubmit = null;
			yield return Step(() => silentSubmit = Auto.SimulateGamepadSequence(new ControlSequence().Press(GamepadButton.South).Hold(0.25f)));
			yield return Until(() => silentSubmit.Completed, "South to be released");
			Assert.That(Rig.Log.Count, Is.EqualTo(before), "no move, submit or cancel without a selection");
			Assert.That(Rig.EventSystem.currentSelectedGameObject, Is.Null);
		}

		[UnityTest]
		public IEnumerator G1_HeldStick_RepeatsTheMoveAfterTheDelay_AndThenAtTheRate()
		{
			// The module repeats a held move after moveRepeatDelay and then every moveRepeatRate, measured with
			// InputRuntime.unscaledGameTime (InputSystemUIInputModule.cs, 800-879) and at most once per frame. The input
			// runtime of the test fixture advances that clock by 1/30 s in every Dynamic update
			// (InputTestRuntime.Update), so one frame is 1/30 s here. The delay of 0.5 s is 15 frames and is compared
			// with "greater than", so the second move comes 16 frames after the first (one more or less is allowed);
			// the rate of 0.1 s is 3 frames, which gives 4.
			CreateRig();
			var a = Rig.AddSelectable("A", Rig.Cell(0, 2));
			var b = Rig.AddSelectable("B", Rig.Cell(0, 1));
			a.navigation = Explicit(down: b);
			b.navigation = Explicit(up: a);
			Rig.EventSystem.firstSelectedGameObject = a.gameObject;
			yield return Settle();
			yield return Steps(2);
			Assert.That(Rig.Module.moveRepeatDelay, Is.EqualTo(0.5f));
			Assert.That(Rig.Module.moveRepeatRate, Is.EqualTo(0.1f));
			StartAuto();

			LogItem stick = null;
			yield return Step(() => stick = Auto.SimulateGamepadSequence(new ControlSequence().Set("leftStick", Vector2.down).Hold(2.5f)));
			yield return Until(() => stick.Completed, "the stick to be released", 90);

			var moves = Rig.Log.Of("move").ToList();
			Assert.That(moves.Count, Is.GreaterThanOrEqualTo(3), Rig.Log.ToString());
			Assert.That(moves[0].Frame, Is.EqualTo(stick.StartedFrameCount), "the first move comes at once");
			Assert.That(moves[1].Frame - moves[0].Frame, Is.InRange(15, 17), "the delay");
			Assert.That(moves[2].Frame - moves[1].Frame, Is.InRange(3, 5), "the rate");
			Assert.That(moves.All(m => m.MoveDirection == MoveDirection.Down));
		}

		[UnityTest]
		public IEnumerator G2_UsingAVirtualGamepad_SwitchesTheControlSchemeOfAPlayerInput()
		{
			// G2. A PlayerInput of a single player listens for the use of devices that are not paired with it and
			// switches to the control scheme that fits (PlayerInput.cs, 1843-1868, 2068-2128). A device that was added is
			// not enough; it has to produce input.
			yield return BuildRig();
			InputSystem.AddDevice<Keyboard>();
			InputSystem.AddDevice<Mouse>();

			var asset = ScriptableObject.CreateInstance<InputActionAsset>();
			OnTearDown(() => Object.DestroyImmediate(asset));
			var fire = asset.AddActionMap("Player").AddAction("Fire", InputActionType.Button);
			fire.AddBinding("<Keyboard>/space", groups: "KeyboardMouse");
			fire.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");
			asset.AddControlScheme("KeyboardMouse").WithRequiredDevice<Keyboard>().WithRequiredDevice<Mouse>();
			asset.AddControlScheme("Gamepad").WithRequiredDevice<Gamepad>();

			var playerObject = new GameObject("Player");
			playerObject.SetActive(false);
			OnTearDown(() => Object.DestroyImmediate(playerObject));
			var player = playerObject.AddComponent<PlayerInput>();
			player.actions = asset;
			player.defaultActionMap = "Player";
			player.defaultControlScheme = "KeyboardMouse";
			player.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
			var schemes = new List<string>();
			player.onControlsChanged += p => schemes.Add(p.currentControlScheme);
			playerObject.SetActive(true);
			yield return Steps(2);
			Assert.That(player.currentControlScheme, Is.EqualTo("KeyboardMouse"));

			StartAuto();
			LogItem pad = null;
			yield return Step(() => pad = Auto.SimulateGamepadSequence(new ControlSequence().Press(GamepadButton.South).Hold(0.15f)));
			Assert.That(player.currentControlScheme, Is.EqualTo("KeyboardMouse"), "nothing was processed yet");
			yield return Until(() => pad.Completed, "South to be released");
			yield return Steps(2);

			Assert.That(player.currentControlScheme, Is.EqualTo("Gamepad"));
			Assert.That(schemes, Does.Contain("Gamepad"));
			Assert.That(player.devices.Contains(pad.Device), Is.True, "the virtual gamepad is paired with the player");
			Assert.That(Gamepad.current, Is.SameAs(pad.Device));
			Assert.That(VirtualInput.IsVirtual(Gamepad.current), Is.True);
		}
	}
}
#endif
