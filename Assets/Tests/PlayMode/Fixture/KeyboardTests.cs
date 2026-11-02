#if ENABLE_AUTOTAP
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// A virtual keyboard. The back key (Escape and the <c>*/{Cancel}</c> action), a key read through
	/// <c>anyKey</c>, a shortcut with a modifier, and text. Experiments K1, K2, K3 and T1.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The back key of pocket-client is the <c>UI/Cancel</c> action, bound to <c>*/{Cancel}</c>. The usage Cancel is on
	/// the Escape key of a keyboard and on <c>buttonEast</c> of a gamepad (Keyboard.cs, 53; Gamepad.cs, 121). The input
	/// module delivers cancel to the selected object when the action was performed in the Dynamic update, and
	/// at most once per update (InputSystemUIInputModule.cs, 881-905).
	/// </para>
	/// <para>
	/// An operation of the auto tap never presses and releases in one input update, and a device runs one operation
	/// at a time, so the key can be pressed again in the second frame after the previous press: see
	/// <see cref="K1_EscapeTwiceInARow_CancelsTwice"/>. Pressing and releasing in one input update is possible only with
	/// <see cref="VirtualDevice"/> directly: <see cref="K1_EscapePressedAndReleasedInOneInputUpdate_StillCancels"/>.
	/// </para>
	/// </remarks>
	public class KeyboardTests : AutoTapFixture
	{
		InputAction CountCancels(List<int> frames)
		{
			var action = new InputAction("cancel", InputActionType.Button, "*/{Cancel}");
			action.performed += _ => frames.Add(Frame);
			action.Enable();
			OnTearDown(() =>
			{
				action.Disable();
				action.Dispose();
			});
			return action;
		}

		IEnumerator RigWithASelection(out ProbeSelectable target)
		{
			CreateRig();
			target = Rig.AddSelectable("Target", Rig.Cell(1, 1));
			Rig.EventSystem.firstSelectedGameObject = target.gameObject;
			return SettleAndWait();
		}

		IEnumerator SettleAndWait()
		{
			yield return Settle();
			yield return Steps(2);
		}

		[UnityTest]
		public IEnumerator K1_Escape_PerformsCancel_AndCancelsTheSelectedObject_InTheFrameItWasProcessed()
		{
			yield return RigWithASelection(out var target);
			Assert.That(Rig.EventSystem.currentSelectedGameObject, Is.SameAs(target.gameObject));
			var performed = new List<int>();
			CountCancels(performed);
			StartAuto();

			LogItem escape = null;
			yield return Step(() => escape = Auto.FireKeyPress(Key.Escape, 0.35f));
			Assert.That(escape, Is.Not.Null);
			Assert.That(performed, Is.Empty, "nothing is processed inside Fire");
			Assert.That(Rig.Log.CountOf("cancel"), Is.Zero);

			yield return Until(() => escape.Order.HasValue, "the key press to be processed");
			Assert.That(performed, Is.EqualTo(new[] { escape.StartedFrameCount }), "*/{Cancel} is performed in the frame of the press");
			Assert.That(Rig.Log.CountOf("cancel", target.gameObject), Is.EqualTo(1), Rig.Log.ToString());
			Assert.That(Rig.Log.First("cancel").Frame, Is.EqualTo(escape.StartedFrameCount), "and the module delivers it in that frame");

			yield return Until(() => escape.Completed, "the key to be released");
			yield return Steps(1);
			Assert.That(performed, Has.Count.EqualTo(1), "a held key does not repeat");
			Assert.That(Rig.Log.CountOf("cancel"), Is.EqualTo(1));
		}

		[UnityTest]
		public IEnumerator K1_EscapeTwiceInARow_CancelsTwice()
		{
			// Two presses of Escape, with a Fire attempt in every frame. The device runs one operation at a time, so
			// the attempt of the frame after the first Fire returns null (the release is not processed yet); the next
			// one is accepted, which is the second frame after the first press. Both presses cancel.
			yield return RigWithASelection(out var target);
			var performed = new List<int>();
			CountCancels(performed);
			StartAuto();

			var items = new List<LogItem>();
			var refused = 0;
			for (var frame = 0; frame < 20 && items.Count < 2; ++frame)
			{
				yield return Step(() =>
				{
					var item = Auto.FireKeyPress(Key.Escape);
					if (item != null)
					{
						items.Add(item);
					}
					else if (items.Count > 0)
					{
						++refused;
					}
				});
			}

			Assert.That(items, Has.Count.EqualTo(2), "both presses were accepted");
			yield return Until(() => items.All(i => i.Completed), "both presses to end");
			yield return Steps(1);

			var gap = items[1].StartedFrameCount - items[0].StartedFrameCount;
			TestContext.WriteLine($"Frames between the two presses: {gap}; Fire was refused {refused} time(s) in between.");
			Assert.That(refused, Is.GreaterThanOrEqualTo(1), "the device was busy until the release of the first press was processed");
			Assert.That(gap, Is.GreaterThanOrEqualTo(2), "a press and its release are in two input updates");
			Assert.That(performed, Has.Count.EqualTo(2), "*/{Cancel} was performed twice");
			Assert.That(Rig.Log.CountOf("cancel", target.gameObject), Is.EqualTo(2), "and the selected object was canceled twice");
			Assert.That(Rig.Log.Of("cancel").Select(c => c.Frame), Is.EqualTo(items.Select(i => i.StartedFrameCount)));
		}

		[UnityTest]
		public IEnumerator K1_EscapePressedAndReleasedInOneInputUpdate_StillCancels()
		{
			// The module polls WasPerformedThisDynamicUpdate, which stays true for the whole frame even when the
			// button was released in it (InputAction.cs, 1489-1498: the frame of the last performed). So VirtualDevice
			// can press and release in one input update and the cancel still arrives.
			yield return RigWithASelection(out var target);
			var performed = new List<int>();
			CountCancels(performed);
			AcquireSession();
			var keyboard = Session.GetDevice("Keyboard");
			yield return Steps(1);

			var fireFrame = 0;
			yield return Step(() =>
			{
				fireFrame = Frame;
				keyboard.Set("escape", 1f);
				keyboard.Set("escape", 0f);
			});
			yield return Step();

			Assert.That(performed, Is.EqualTo(new[] { fireFrame + 1 }));
			Assert.That(Rig.Log.CountOf("cancel", target.gameObject), Is.EqualTo(1), Rig.Log.ToString());
			Assert.That(Rig.Log.First("cancel").Frame, Is.EqualTo(fireFrame + 1));
		}

		[UnityTest]
		public IEnumerator K1_TwoEscapesInOneInputUpdate_GiveOneCancel()
		{
			// The limit of mashing: the module delivers cancel once per Process, however many times the action was
			// performed in the update (InputSystemUIInputModule.cs, 901-904). The action itself is performed twice.
			yield return RigWithASelection(out var target);
			var performed = new List<int>();
			CountCancels(performed);
			AcquireSession();
			var keyboard = Session.GetDevice("Keyboard");
			yield return Steps(1);

			yield return Step(() =>
			{
				keyboard.Set("escape", 1f);
				keyboard.Set("escape", 0f);
				keyboard.Set("escape", 1f);
				keyboard.Set("escape", 0f);
			});
			yield return Step();

			Assert.That(performed, Has.Count.EqualTo(2), "the action was performed twice");
			Assert.That(Rig.Log.CountOf("cancel", target.gameObject), Is.EqualTo(1), "the selected object was canceled once");
		}

		[UnityTest]
		public IEnumerator K2_AnyKey_WasPressedThisFrame_IsTrueInTheFrameOfThePress_AlsoWhenReleasedInTheSameUpdate()
		{
			// K2, read the way CardPackStateMachine does: Keyboard.current.anyKey.wasPressedThisFrame in every frame. The
			// first read of a control starts the bookkeeping of presses (ButtonControl.cs, 278-298), so the reading has
			// to begin before the press. A press that is released in the same update is remembered by the bookkeeping
			// of the update (InputManager.cs, 4022-4065).
			yield return BuildRig();
			AcquireSession();
			var device = Session.GetDevice("Keyboard");
			var keyboard = (Keyboard)device.Device;
			yield return Steps(2);

			bool Read()
			{
				return Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
			}

			Assert.That(Read(), Is.False);
			yield return Step(() => device.Set("space", 1f));
			Assert.That(Read(), Is.False, "nothing is processed inside the frame that queued it");

			yield return Step();
			Assert.That(Keyboard.current, Is.SameAs(keyboard), "the virtual keyboard is current");
			Assert.That(Read(), Is.True, "true in the frame of the press");

			yield return Step();
			Assert.That(keyboard.spaceKey.isPressed, Is.True);
			Assert.That(Read(), Is.False, "and not in the next frame, while the key is held");

			yield return Step(() => device.Set("space", 0f));
			yield return Step();
			Assert.That(Read(), Is.False, "a release is not a press");

			yield return Step(() =>
			{
				device.Set("space", 1f);
				device.Set("space", 0f);
			});
			yield return Step();
			Assert.That(keyboard.spaceKey.isPressed, Is.False);
			Assert.That(Read(), Is.True, "a press that was released in the same update is seen");
		}

		void UseShortcutModifiersInOrder()
		{
			// OneModifier with the default order needs the modifier first when this is set, because the bound
			// control is a button (OneModifierComposite.cs, 204-219). Set on the settings that are in force, which
			// are the ones of the virtual input while it is acquired.
			InputSystem.settings.shortcutKeysConsumeInput = true;
		}

		InputAction MakeRestartShortcut(List<int> frames)
		{
			var action = new InputAction("restart", InputActionType.Button);
			action.AddCompositeBinding("OneModifier")
				.With("Modifier", "<Keyboard>/ctrl")
				.With("Binding", "<Keyboard>/r");
			action.performed += _ => frames.Add(Frame);
			action.Enable();
			OnTearDown(() =>
			{
				action.Disable();
				action.Dispose();
			});
			return action;
		}

		[UnityTest]
		public IEnumerator K3_OneModifier_ModifierFirst_Performs()
		{
			yield return BuildRig();
			AcquireSession();
			UseShortcutModifiersInOrder();
			var performed = new List<int>();
			MakeRestartShortcut(performed);
			var keyboard = Session.GetDevice("Keyboard");
			OnTearDown(keyboard.ResetToDefault);
			yield return Steps(2);

			yield return Step(() => keyboard.Set("leftCtrl", 1f));
			yield return Steps(2);
			Assert.That(performed, Is.Empty, "the modifier alone does nothing");

			yield return Step(() => keyboard.Set("r", 1f));
			yield return Step();
			Assert.That(performed, Has.Count.EqualTo(1));
		}

		[UnityTest]
		public IEnumerator K3_OneModifier_ModifierAndKeyInOneEvent_DoesNotPerform_WhichIsWhyEveryStepIsItsOwnEvent()
		{
			// Both are pressed by one event, but the press time of a part is recorded by the state change monitor of
			// that control. The monitor that is notified first evaluates the composite and records the event id on it;
			// the notification of the other control of the same event is ignored (InputActionState.cs, 1520,
			// 1631-1641). So the press time of the key is not recorded yet when the modifier is compared with it
			// (InputActionState.cs, 1503, 2966-2983, OneModifierComposite.cs, 187-202). With the monitors notified in
			// the order of the controls in the state (ctrl before R), the composite does not perform. This is the reason
			// why a gesture queues its steps as separate events, which K3_OneModifier_ModifierThenKeyInOneFrame_Performs
			// shows; Queue is the one call that writes several controls into one event.
			yield return BuildRig();
			AcquireSession();
			UseShortcutModifiersInOrder();
			var performed = new List<int>();
			MakeRestartShortcut(performed);
			var keyboard = Session.GetDevice("Keyboard");
			OnTearDown(keyboard.ResetToDefault);
			yield return Steps(2);

			yield return Step(() => keyboard.Queue(ControlValue.Of("leftCtrl", 1f), ControlValue.Of("r", 1f)));
			yield return Step();
			Assert.That(performed, Is.Empty);
		}

		[UnityTest]
		public IEnumerator K3_OneModifier_ModifierThenKeyInOneFrame_Performs()
		{
			// The steps that fall into one frame are queued as separate events in the order of the gesture, so the
			// modifier is pressed first by the press time, too.
			yield return BuildRig();
			AcquireSession();
			UseShortcutModifiersInOrder();
			var performed = new List<int>();
			MakeRestartShortcut(performed);
			StartAuto();
			yield return Steps(2);

			LogItem item = null;
			yield return Step(() => item = Auto.FireKeyPress(Key.R, 0.25f, Key.LeftCtrl));
			Assert.That(item, Is.Not.Null);
			yield return Until(() => item.Completed, "the shortcut to end");
			yield return Steps(1);
			Assert.That(performed, Has.Count.EqualTo(1));
		}

		[UnityTest]
		public IEnumerator K3_OneModifier_KeyBeforeTheModifier_DoesNotPerform()
		{
			// The control of the test: pressed in the other order, the composite does not trigger.
			yield return BuildRig();
			AcquireSession();
			UseShortcutModifiersInOrder();
			var performed = new List<int>();
			MakeRestartShortcut(performed);
			var keyboard = Session.GetDevice("Keyboard");
			OnTearDown(keyboard.ResetToDefault);
			yield return Steps(2);

			yield return Step(() => keyboard.Set("r", 1f));
			yield return Steps(2);
			yield return Step(() => keyboard.Set("leftCtrl", 1f));
			yield return Steps(2);
			Assert.That(performed, Is.Empty);
		}

		[UnityTest]
		public IEnumerator T1_TextAndComposition_ReachTheKeyboard_AndTheReaderReportsThemInOrder()
		{
			// T1. A text event and an IME composition event are delivered to the keyboard when they are processed
			// (InputManager.cs, 3562-3594) and do not go through onChange; the reader takes them from onEvent and
			// numbers them in the same order as the key changes. Pressing a key sends no character.
			yield return BuildRig();
			AcquireSession();
			StartAuto();
			var device = Session.GetDevice("Keyboard");
			var keyboard = (Keyboard)device.Device;
			var typed = new List<char>();
			var composed = new List<string>();
			keyboard.onTextInput += typed.Add;
			keyboard.onIMECompositionChange += composition => composed.Add(composition.ToString());
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			yield return Steps(1);

			LogItem plainKey = null;
			yield return Step(() => plainKey = Auto.FireKeyPress(Key.B));
			yield return Until(() => plainKey.Completed, "the key to end");
			Assert.That(typed, Is.Empty, "a key press does not enter a character");
			changes.Items.Clear();

			var sequence = new ControlSequence()
				.Press(Key.A).Text("a").Release(Key.A)
				.Composition("か").Composition("かん").Composition("")
				.Text("漢");
			LogItem item = null;
			yield return Step(() => item = Auto.FireDeviceSequence("Keyboard", sequence));
			yield return Until(() => item.Completed, "the sequence to end");
			yield return Steps(1);

			Assert.That(typed, Is.EqualTo(new[] { 'a', '漢' }));
			Assert.That(composed, Is.EqualTo(new[] { "か", "かん", "" }));

			var order = new List<string>();
			foreach (var change in changes.Items)
			{
				if (change.Is<ButtonDown>() && change.ControlPath == "a")
				{
					order.Add("down:a");
				}
				else if (change.Is<ButtonUp>() && change.ControlPath == "a")
				{
					order.Add("up:a");
				}
				else if (change.Is<TextEntered>())
				{
					order.Add("text:" + change.Text);
				}
				else if (change.Is<CompositionChanged>())
				{
					order.Add("composition:" + change.Composition);
				}
			}

			Assert.That(order, Is.EqualTo(new[]
			{
				"down:a", "text:a", "up:a", "composition:か", "composition:かん", "composition:", "text:漢",
			}), changes.ToString());
			var numbers = changes.Items.Select(c => c.Order).ToList();
			Assert.That(numbers, Is.Ordered);
			Assert.That(numbers, Is.Unique);
		}

		[UnityTest]
		public IEnumerator T1_ACharacterBeyondTheBasicPlane_IsOneTextEventAndTwoCharactersForTheKeyboard()
		{
			// A text event carries a UTF-32 character; the keyboard gets the two surrogates of it one after the other
			// (InputManager.cs, 3566-3581). The reader reports the one event, with the code point as a string.
			yield return BuildRig();
			AcquireSession();
			var device = Session.GetDevice("Keyboard");
			var keyboard = (Keyboard)device.Device;
			var typed = new List<char>();
			keyboard.onTextInput += typed.Add;
			var changes = new ChangeLog(Session.Reader);
			OnTearDown(changes.Dispose);
			yield return Steps(1);

			const string grinningFace = "\U0001F600";
			yield return Step(() => device.Text(grinningFace));
			yield return Step();

			Assert.That(new string(typed.ToArray()), Is.EqualTo(grinningFace));
			Assert.That(typed, Has.Count.EqualTo(2));
			var entered = changes.Of<TextEntered>().ToList();
			Assert.That(entered, Has.Count.EqualTo(1));
			Assert.That(entered[0].Text, Is.EqualTo(grinningFace));
		}

		[UnityTest]
		public IEnumerator Text_IsRefusedByADeviceThatDoesNotReceiveText()
		{
			// Only ITextInputReceiver devices (Keyboard.cs, 1062) accept Text and Composition.
			yield return BuildRig();
			AcquireSession();
			var gamepad = Session.GetDevice("Gamepad");
			Assert.Throws<InvalidOperationException>(() => gamepad.Text("a"));
			Assert.Throws<InvalidOperationException>(() => gamepad.Composition("a"));
			yield return Steps(1);
		}
	}
}
#endif
