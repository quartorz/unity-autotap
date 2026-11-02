#if ENABLE_AUTOTAP
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UnityAutoTap.Tests.RealInput
{
	/// <summary>
	/// <see cref="VirtualInput"/> against the real input system of the running Editor, not the fake one of
	/// <c>InputTestFixture</c>: the settings and the pointer behavior that are replaced and put back, and the
	/// devices that are removed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// These tests change the state of the Editor that runs them (devices are added and removed, the settings are
	/// swapped), so they put everything back in <see cref="TearDown"/> and must not run while the Editor is used for
	/// something else, as for every run of the virtual input.
	/// </para>
	/// <para>
	/// They do not change the project's input settings, so they are inconclusive if the update mode of the project is
	/// not Dynamic.
	/// </para>
	/// </remarks>
	public class VirtualInputRealInputTests
	{
		readonly List<Action> _cleanups = new List<Action>();

		[SetUp]
		public void SetUp()
		{
			if (InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
			{
				Assert.Inconclusive("The input settings of the project do not process events in the Dynamic update.");
			}

			// A device left by an earlier run would make every test start from a state that is not the one it is about.
			foreach (var device in InputSystem.devices.Where(VirtualInput.IsVirtual).ToList())
			{
				InputSystem.RemoveDevice(device);
			}
		}

		[TearDown]
		public void TearDown()
		{
			for (var i = _cleanups.Count - 1; i >= 0; --i)
			{
				try
				{
					_cleanups[i]();
				}
				catch (Exception e)
				{
					TestContext.Error.WriteLine($"Cleanup failed: {e}");
				}
			}

			_cleanups.Clear();

			// Whatever a failed test left behind.
			foreach (var device in InputSystem.devices.Where(VirtualInput.IsVirtual).ToList())
			{
				InputSystem.RemoveDevice(device);
			}
		}

		static IEnumerator WaitUntil(Func<bool> condition, string what, int maxFrames = 30)
		{
			for (var i = 0; i < maxFrames; ++i)
			{
				if (condition())
				{
					yield break;
				}

				yield return null;
			}

			if (!condition())
			{
				Assert.Fail($"Timed out after {maxFrames} frames, waiting for {what}.");
			}
		}

		static bool AnyVirtualDevice()
		{
			return InputSystem.devices.Any(VirtualInput.IsVirtual);
		}

#if UNITY_EDITOR
		[UnityTest]
		public IEnumerator Acquire_InTheEditor_ReplacesTheSettings_AndTheLastDisposePutsThemBack()
		{
			// In the Editor, while a session is alive, background behavior is IgnoreFocus and the Editor input behavior
			// is AllDeviceInputAlwaysGoesToGameView, so that input arrives although the Game view has no focus (the
			// Editor may run in the background); the original settings come back with the last dispose (documented on
			// VirtualInput).
			var original = InputSystem.settings;
			var backgroundBehavior = original.backgroundBehavior;
			var editorInputBehavior = original.editorInputBehaviorInPlayMode;
			if (backgroundBehavior == InputSettings.BackgroundBehavior.IgnoreFocus
				&& editorInputBehavior == InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView)
			{
				Assert.Inconclusive("The settings of the project already are what the session sets; there is nothing to see.");
			}

			var first = VirtualInput.Acquire();
			_cleanups.Add(first.Dispose);
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.IgnoreFocus));
			Assert.That(InputSystem.settings.editorInputBehaviorInPlayMode,
				Is.EqualTo(InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView));

			var second = VirtualInput.Acquire();
			_cleanups.Add(second.Dispose);
			first.Dispose();
			first.Dispose();
			yield return null;
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(InputSettings.BackgroundBehavior.IgnoreFocus),
				"the second session is alive, and a handle given up twice counts once");

			second.Dispose();
			yield return null;
			yield return null;
			Assert.That(InputSystem.settings, Is.SameAs(original));
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(backgroundBehavior));
			Assert.That(InputSystem.settings.editorInputBehaviorInPlayMode, Is.EqualTo(editorInputBehavior));
		}
#else
		[UnityTest]
		public IEnumerator Acquire_InAPlayer_LeavesTheSettingsOfTheProjectAlone()
		{
			// Only the Editor has its settings replaced (documented on VirtualInput); a player keeps the settings of
			// the project while a session is alive and after it.
			var original = InputSystem.settings;
			var backgroundBehavior = original.backgroundBehavior;
			var editorInputBehavior = original.editorInputBehaviorInPlayMode;

			var session = VirtualInput.Acquire();
			_cleanups.Add(session.Dispose);
			Assert.That(InputSystem.settings, Is.SameAs(original));
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(backgroundBehavior));
			Assert.That(InputSystem.settings.editorInputBehaviorInPlayMode, Is.EqualTo(editorInputBehavior));

			session.Dispose();
			yield return null;
			yield return null;
			Assert.That(InputSystem.settings, Is.SameAs(original));
			Assert.That(InputSystem.settings.backgroundBehavior, Is.EqualTo(backgroundBehavior));
		}
#endif

		static (GameObject Root, InputSystemUIInputModule Module) CreateEventSystem()
		{
			var root = new GameObject("RealInputTestEventSystem");
			root.AddComponent<EventSystem>();
			var module = root.AddComponent<InputSystemUIInputModule>();
			return (root, module);
		}

		[UnityTest]
		public IEnumerator Acquire_SetsAllPointersAsIs_OnTheEventSystemThatIsThere_AndDisposePutsTheBehaviorBack()
		{
			// On every platform, so that a virtual finger, mouse and pen do not remove each other (documented on
			// VirtualInput).
			var (root, module) = CreateEventSystem();
			_cleanups.Add(() => Object.DestroyImmediate(root));
			yield return null;
			var before = module.pointerBehavior;
			Assert.That(before, Is.Not.EqualTo(UIPointerBehavior.AllPointersAsIs), "the default of the module is another behavior");

			var session = VirtualInput.Acquire();
			_cleanups.Add(session.Dispose);
			yield return null;
			yield return null;
			Assert.That(module.pointerBehavior, Is.EqualTo(UIPointerBehavior.AllPointersAsIs));

			session.Dispose();
			yield return null;
			yield return null;
			Assert.That(module.pointerBehavior, Is.EqualTo(before), "the behavior that the module had is back");
		}

		[UnityTest]
		public IEnumerator Acquire_SetsAllPointersAsIs_AlsoOnAnEventSystemThatAppearsLater()
		{
			// The module of a scene that is loaded while the auto tap runs is treated like one that was there. The
			// documentation of VirtualInput does not say so explicitly, so this is the first test to look at if the
			// behavior of a late module is decided otherwise.
			var session = VirtualInput.Acquire();
			_cleanups.Add(session.Dispose);
			yield return null;

			var (root, module) = CreateEventSystem();
			_cleanups.Add(() => Object.DestroyImmediate(root));
			yield return null;
			yield return null;
			Assert.That(module.pointerBehavior, Is.EqualTo(UIPointerBehavior.AllPointersAsIs));
		}

		[UnityTest]
		public IEnumerator Dispose_RemovesTheVirtualDevices_AfterWhatIsPressedWasReleased()
		{
			// When the last session is disposed, what is pressed is released and the devices are removed after the release
			// was processed (documented on VirtualInput). This runs against the devices of the Editor, so it presses at a
			// point of an empty screen.
			var session = VirtualInput.Acquire();
			_cleanups.Add(session.Dispose);
			Assert.That(AnyVirtualDevice(), Is.False, "nothing is added by Acquire");

			var finger = session.NewFinger();
			Assert.That(finger, Is.Not.Null);
			finger.Press(new Vector2(30f, 30f));
			var keyboard = session.GetDevice("Keyboard");
			var mouse = session.GetPointer("Mouse");
			keyboard.Set("space", 1f);
			mouse.MoveTo(new Vector2(40f, 40f));
			mouse.Press();
			yield return null;
			yield return null;
			Assert.That(AnyVirtualDevice(), Is.True);
			Assert.That(finger.State, Is.EqualTo(FingerState.Pressed));

			session.Dispose();
			yield return WaitUntil(() => !AnyVirtualDevice(), "the virtual devices to be removed");
			Assert.That(finger.State, Is.EqualTo(FingerState.Released), "the finger was released before its device went");
			Assert.That(InputSystem.devices.Where(d => d.name.StartsWith("AutoTap")), Is.Empty);
		}

		[UnityTest]
		public IEnumerator Acquire_RemovesAVirtualDeviceThatWasLeftBehindByAnEarlierRun()
		{
			// A device whose name is the name of a virtual device, left by an earlier run (the Editor does
			// not remove devices when Play mode ends), is removed when a session is acquired. The name is learned from a
			// device of the virtual input, so that the test does not repeat the naming scheme.
			var learning = VirtualInput.Acquire();
			_cleanups.Add(learning.Dispose);
			var name = learning.GetDevice("Keyboard").Device.name;
			learning.Dispose();
			yield return WaitUntil(() => !AnyVirtualDevice(), "the virtual devices to be removed");

			var leftover = InputSystem.AddDevice("Keyboard", name);
			_cleanups.Add(() =>
			{
				if (leftover.added)
				{
					InputSystem.RemoveDevice(leftover);
				}
			});
			Assert.That(VirtualInput.IsVirtual(leftover), Is.True, "a device is virtual by its name");
			Assert.That(InputSystem.devices, Does.Contain(leftover));

			var session = VirtualInput.Acquire();
			_cleanups.Add(session.Dispose);
			yield return null;
			Assert.That(leftover.added, Is.False, "the device that was left behind is gone");
			Assert.That(AnyVirtualDevice(), Is.False);
		}

		[UnityTest]
		public IEnumerator DevicesThatAreNotVirtual_AreNotTouched()
		{
			// The cleanup is by name, so a device that has another name stays.
			var other = InputSystem.AddDevice("Keyboard", "NotAutoTapKeyboard");
			_cleanups.Add(() =>
			{
				if (other.added)
				{
					InputSystem.RemoveDevice(other);
				}
			});
			Assert.That(VirtualInput.IsVirtual(other), Is.False);

			var session = VirtualInput.Acquire();
			session.GetDevice("Keyboard");
			session.Dispose();
			yield return WaitUntil(() => !AnyVirtualDevice(), "the virtual devices to be removed");
			Assert.That(other.added, Is.True);
		}
	}
}
#endif
