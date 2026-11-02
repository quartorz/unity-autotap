#if ENABLE_AUTOTAP
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// <see cref="VirtualInput"/> and <see cref="VirtualInputSession"/> on the fake input system: what a session
	/// requires, adds and changes. The tests that need the real input system of the Editor are in the other assembly.
	/// </summary>
	public class SessionTests : AutoTapFixture
	{
		[Test]
		public void Acquire_WhenTheUpdateModeIsNotDynamic_Throws()
		{
			// Virtual input works only when events are processed in the dynamic update (documented on
			// VirtualInput.Acquire).
			var original = InputSystem.settings.updateMode;
			try
			{
				InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInFixedUpdate;
				Assert.Throws<InvalidOperationException>(() => VirtualInput.Acquire());
				InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
				Assert.Throws<InvalidOperationException>(() => VirtualInput.Acquire());
			}
			finally
			{
				InputSystem.settings.updateMode = original;
			}
		}

		[UnityTest]
		public IEnumerator DevicesAreAddedWhenTheyAreFirstUsed_AndOnlyThen()
		{
			// Acquiring adds no device; each (layout, number) is added at its first use (documented on VirtualInput).
			yield return BuildRig();
			AcquireSession();
			Assert.That(AnyVirtualDevice(), Is.False, "nothing is added by Acquire");

			var keyboard = Session.GetDevice("Keyboard");
			Assert.That(VirtualInput.IsVirtual(keyboard.Device), Is.True);
			Assert.That(AnyVirtualDevice(), Is.True);
			Assert.That(InputSystem.devices, Has.None.InstanceOf<Mouse>(), "no mouse was asked for");
			Assert.That(InputSystem.devices, Has.None.InstanceOf<Touchscreen>(), "no finger was used");

			Assert.That(Session.GetDevice("Keyboard"), Is.SameAs(keyboard), "the same device for the same layout and number");
			Assert.That(Session.GetDevice("Keyboard", 1), Is.Not.SameAs(keyboard), "another number, another device");
			Assert.That(Session.GetDevice("Keyboard", 1).Device, Is.Not.SameAs(keyboard.Device));
			Assert.That(Session.GetPointer("Mouse"), Is.SameAs(Session.GetPointer("Mouse")));
			Assert.That(Session.GetPointer("Mouse").Device, Is.InstanceOf<Mouse>());
			Assert.That(Session.GetPointer("Pen").Device, Is.InstanceOf<Pen>());
			yield return Steps(1);
		}

		[UnityTest]
		public IEnumerator IsVirtual_IsTrueForTheDevicesOfTheVirtualInputOnly()
		{
			yield return BuildRig();
			Assert.That(VirtualInput.IsVirtual(null), Is.False);
			var real = AddRealDevice<Keyboard>("Keyboard", "RealKeyboard");
			Assert.That(VirtualInput.IsVirtual(real), Is.False);

			AcquireSession();
			var virtualKeyboard = Session.GetDevice("Keyboard").Device;
			Assert.That(VirtualInput.IsVirtual(virtualKeyboard), Is.True);
			Assert.That(VirtualInput.IsVirtual(real), Is.False);
			yield return Steps(1);
		}

		[UnityTest]
		public IEnumerator TheWrongKindOfLayout_IsAUsageError()
		{
			// A touchscreen is used through fingers, other pointers through GetPointer, and everything else through
			// GetDevice (documented on IVirtualInput).
			yield return BuildRig();
			AcquireSession();
			Assert.Throws<ArgumentException>(() => Session.GetPointer("Touchscreen"));
			Assert.Throws<ArgumentException>(() => Session.GetPointer("Keyboard"));
			Assert.Throws<ArgumentException>(() => Session.GetDevice("Mouse"));
			Assert.Throws<ArgumentException>(() => Session.GetDevice("Touchscreen"));
			yield return Steps(1);
		}

		[UnityTest]
		public IEnumerator ControlThatTheDeviceDoesNotHave_OrOfTheWrongType_IsAUsageError()
		{
			// The doc of VirtualDevice: a path that names no control, or a value of the wrong type, throws.
			yield return BuildRig();
			AcquireSession();
			var keyboard = Session.GetDevice("Keyboard");
			Assert.Throws<ArgumentException>(() => keyboard.Set("noSuchControl", 1f));
			Assert.Throws<ArgumentException>(() => keyboard.Set("a", Vector2.zero));
			yield return Steps(1);
		}

		[UnityTest]
		public IEnumerator Fire_WithAPlanThatDoesNotFitTheTarget_ThrowsAndStartsNothing()
		{
			// A gesture that does not fit its target is a usage error (documented on Gesture and on the Fire methods): a
			// hover or a scroll cannot be done by a finger, nor can a control that the device does not have, a pointer
			// layout given to FireDevice, and so on.
			yield return BuildRig();
			StartAuto();
			Assert.Throws<ArgumentException>(() => Auto.FireFinger(0, Gesture.HoverAt(new Vector2(100f, 100f))));
			Assert.Throws<ArgumentException>(() => Auto.FireFinger(0, Gesture.PressAt(new Vector2(100f, 100f)).Scroll(Vector2.up)));
			Assert.Throws<ArgumentException>(() => Auto.FireFinger(0, Gesture.PressAt(new Vector2(100f, 100f), "rightButton")));
			Assert.Throws<ArgumentException>(() => Auto.FirePointerGesture("Touchscreen", Gesture.HoverAt(new Vector2(100f, 100f))));
			Assert.Throws<ArgumentException>(() => Auto.FireDeviceSequence("Mouse", new ControlSequence().Press("leftButton")));
			Assert.Throws<ArgumentException>(() => Auto.FireDeviceSequence("Keyboard", new ControlSequence().Press("noSuchKey")));
			yield return Steps(2);

			LogItem tap = null;
			yield return Step(() => tap = Auto.FireTap(0, new Vector2(100f, 100f), 0f));
			Assert.That(tap, Is.Not.Null, "the slot was not taken by the failed calls");
			yield return Until(() => tap.Completed, "the tap to end");
		}

#if UNITY_EDITOR
		[UnityTest]
		public IEnumerator TheSettingsThatDecideWhetherInputArrives_AreReplacedWhileASessionIsAlive_AndComeBack()
		{
			// The focus related settings are replaced by IgnoreFocus and AllDeviceInputAlwaysGoesToGameView while a
			// session is alive, whichever they were, and put back when the last session is disposed (documented on
			// VirtualInput). Two sessions are counted.
			var settings = InputSystem.settings;
			settings.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
			settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
			yield return BuildRig();

			var first = VirtualInput.Acquire();
			OnTearDown(first.Dispose);
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.IgnoreFocus));
			Assert.That(InputSystem.settings.editorInputBehaviorInPlayMode,
				Is.EqualTo(InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView));

			var second = VirtualInput.Acquire();
			OnTearDown(second.Dispose);
			first.Dispose();
			first.Dispose();
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.IgnoreFocus),
				"one session is still alive, and disposing a handle twice gives up one reference only");

			second.Dispose();
			yield return Steps(2);
			Assert.That(InputSystem.settings, Is.SameAs(settings));
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices));
			Assert.That(InputSystem.settings.editorInputBehaviorInPlayMode,
				Is.EqualTo(InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus));
		}

		void SetPlayerFocus(bool focused)
		{
			// The test runtime of the Input System simulates the focus of the player. It is not public, so this goes
			// through reflection and the test is inconclusive if the shape changed.
			var runtimeProperty = typeof(InputTestFixture).GetProperty("runtime", BindingFlags.Instance | BindingFlags.NonPublic);
			var runtime = runtimeProperty?.GetValue(this);
			var method = runtime?.GetType().GetMethod(focused ? "PlayerFocusGained" : "PlayerFocusLost", BindingFlags.Instance | BindingFlags.Public);
			if (method == null)
			{
				Assert.Inconclusive("The test runtime of the Input System has no PlayerFocusLost / PlayerFocusGained.");
			}

			method.Invoke(runtime, null);
		}

		[UnityTest]
		public IEnumerator E5_WhileTheGameViewHasNoFocus_NothingReachesTheUiWithoutTheVirtualInput()
		{
			// The premise of E5. With the settings of the project (focus is respected) and the player out of focus, the
			// events of a pointer are left for the Editor update and never reach the UI (InputManager.cs, 3321-3334).
			yield return BuildRig();
			var target = Rig.Add("Target", Rig.Cell(1, 1));
			yield return Settle();
			InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
			InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
			var touchscreen = InputSystem.AddDevice<Touchscreen>();
			SetPlayerFocus(false);
			OnTearDown(() => SetPlayerFocus(true));
			yield return Steps(1);

			BeginTouch(1, Rig.Center(target), queueEventOnly: true, screen: touchscreen);
			yield return Steps(3);
			Assert.That(Rig.Log.CountOf("pointerDown"), Is.Zero, Rig.Log.ToString());
		}

		[UnityTest]
		public IEnumerator E5_WhileTheGameViewHasNoFocus_VirtualInputStillReachesTheUi_AndTheSettingsComeBack()
		{
			// E5. The same situation as above, but with a session: the replaced settings make the game get input
			// whatever the focus is (InputManager.cs, 402-414), so the tap arrives; afterwards the settings of the
			// project are back.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			var settings = InputSystem.settings;
			settings.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
			settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
			SetPlayerFocus(false);
			OnTearDown(() => SetPlayerFocus(true));
			yield return Steps(1);

			StartAuto(useSession: false);
			LogItem item = null;
			yield return Step(() => item = Auto.FireTap(0, Rig.Center(button.gameObject), 0f));
			yield return Until(() => item.Completed, "the tap to end");
			yield return Steps(2);
			Assert.That(button.ClickCount, Is.EqualTo(1), "the tap arrived although the player had no focus");

			Auto.Stop();
			yield return Until(() => !AnyVirtualDevice(), "the virtual devices to be removed", 30);
			Assert.That(InputSystem.settings, Is.SameAs(settings));
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices));
			Assert.That(InputSystem.settings.editorInputBehaviorInPlayMode,
				Is.EqualTo(InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus));
		}
#endif
	}
}
#endif
