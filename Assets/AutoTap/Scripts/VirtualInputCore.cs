#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityAutoTap
{
	/// <summary>
	/// What happened to a <see cref="VirtualFinger"/> in the input update that is being processed.
	/// </summary>
	internal struct FingerUpdate
	{
		public bool Touched;
		public bool Pressed;
		public bool Released;
		public Vector2 LastPosition;
		public long LastOrder;
		public int LastFrame;
		public long FirstBeyondOrder;
		public int FirstBeyondFrame;

		public static FingerUpdate Fresh => new FingerUpdate { Touched = true, FirstBeyondOrder = -1 };
	}

	/// <summary>
	/// The one object behind <see cref="VirtualInput"/>: the virtual devices, the replaced settings and the
	/// bookkeeping that follows what the input updates did with the queued events.
	/// </summary>
	internal sealed class VirtualInputCore
	{
		public const int MaxFingers = 10;
		const string NamePrefix = "AutoTap-";

		static VirtualInputCore s_Instance;
		static int s_NextTouchId = 1;

		readonly InputChangeReader _reader;
		readonly Dictionary<int, VirtualFinger> _fingers = new Dictionary<int, VirtualFinger>();
		readonly List<VirtualFinger> _touched = new List<VirtualFinger>();
		readonly List<VirtualFinger> _settled = new List<VirtualFinger>();
		readonly Dictionary<InputSystemUIInputModule, UIPointerBehavior> _modules =
			new Dictionary<InputSystemUIInputModule, UIPointerBehavior>();
		readonly List<InputDevice> _devices = new List<InputDevice>();

		readonly InputSettings _originalSettings;
		readonly InputSettings _replacedSettings;

		Touchscreen _touchscreen;
		int _references;
		long _endedUpdates;
		bool _draining;
		int _drainStage;
		bool _tornDown;
		Action _inputUpdated;

		VirtualInputCore()
		{
			_originalSettings = InputSystem.settings;
			_replacedSettings = UnityEngine.Object.Instantiate(_originalSettings);
			_replacedSettings.hideFlags = HideFlags.HideAndDontSave;
			_replacedSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
			_replacedSettings.editorInputBehaviorInPlayMode =
				InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
			InputSystem.settings = _replacedSettings;

			_reader = new InputChangeReader(VirtualInput.IsVirtual);
			_reader.Changed += OnChange;
			InputSystem.onBeforeUpdate += OnBeforeUpdate;
			InputSystem.onAfterUpdate += OnAfterUpdate;
			Application.quitting += OnQuitting;
#if UNITY_EDITOR
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

			foreach (var module in UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(
				FindObjectsInactive.Include))
			{
				TrackModule(module);
			}
		}

		public InputChangeReader Reader => _reader;

		/// <summary>
		/// The number of Dynamic input updates that ended. Events queued while it has a value were processed by
		/// the update that ends with the next value, or by an earlier one.
		/// </summary>
		public long EndedUpdates => _endedUpdates;

		public event Action InputUpdated
		{
			add => _inputUpdated += value;
			remove => _inputUpdated -= value;
		}

		public static bool IsVirtualName(string name)
		{
			return name != null && name.StartsWith(NamePrefix, StringComparison.Ordinal);
		}

		public static string DeviceName(string layout, int number)
		{
			return $"{NamePrefix}{layout}-{number}";
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		static void ResetStatics()
		{
			s_Instance?.TearDown();
			s_Instance = null;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		static void RemoveLeftoversAtStartup()
		{
			if (s_Instance == null)
			{
				RemoveLeftovers();
			}

			Application.quitting -= RemoveLeftoversAtQuit;
			Application.quitting += RemoveLeftoversAtQuit;
		}

		static void RemoveLeftoversAtQuit()
		{
			s_Instance?.TearDown();
			RemoveLeftovers();
		}

#if UNITY_EDITOR
		[InitializeOnLoadMethod]
		static void RemoveLeftoversInEditor()
		{
			EditorApplication.delayCall += () =>
			{
				if (s_Instance == null)
				{
					RemoveLeftovers();
				}
			};
		}
#endif

		/// <summary>
		/// Removes the virtual devices that were left behind by a run that did not clean up.
		/// </summary>
		static void RemoveLeftovers()
		{
			List<InputDevice> leftovers = null;
			foreach (var device in InputSystem.devices)
			{
				if (VirtualInput.IsVirtual(device))
				{
					(leftovers ?? (leftovers = new List<InputDevice>())).Add(device);
				}
			}

			if (leftovers == null)
			{
				return;
			}

			foreach (var device in leftovers)
			{
				InputSystem.RemoveDevice(device);
			}
		}

		public static VirtualInputSession Acquire()
		{
			if (InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
			{
				throw new InvalidOperationException(
					"Virtual input works only when the Input System processes events in the dynamic update. " +
					$"InputSettings.updateMode is {InputSystem.settings.updateMode}.");
			}

			var core = s_Instance;
			if (core != null && !core.IsConsistent())
			{
				core.TearDown();
				core = null;
			}

			if (core == null)
			{
				RemoveLeftovers();
				core = new VirtualInputCore();
				s_Instance = core;
			}

			core._references++;
			core._draining = false;
			core._drainStage = 0;
			return new VirtualInputSession(core);
		}

		/// <summary>
		/// <c>false</c> if the Input System was reset behind the back of this object, which makes its settings and
		/// its devices stale.
		/// </summary>
		bool IsConsistent()
		{
			return !_tornDown
				&& InputSystem.settings == _replacedSettings
				&& (_touchscreen == null || IsInInputSystem(_touchscreen));
		}

		public void ReleaseReference()
		{
			if (_tornDown || --_references > 0)
			{
				return;
			}

			var outstanding = false;
			foreach (var finger in _fingers.Values)
			{
				if (finger.State == FingerState.Released)
				{
					continue;
				}

				if (!finger.PressCalled)
				{
					finger.Abandon();
					continue;
				}

				if (finger.CanRelease)
				{
					finger.Release();
				}

				outstanding = true;
			}

			if (outstanding)
			{
				_draining = true;
				_drainStage = 0;
			}
			else
			{
				TearDown();
			}
		}

		public VirtualFinger NewFinger()
		{
			if (_tornDown)
			{
				throw new ObjectDisposedException(nameof(VirtualInput));
			}

			var alive = 0;
			foreach (var finger in _fingers.Values)
			{
				if (finger.State != FingerState.Released)
				{
					++alive;
				}
			}

			if (alive >= MaxFingers)
			{
				return null;
			}

			if (_touchscreen == null)
			{
				_touchscreen = (Touchscreen)InputSystem.AddDevice("Touchscreen", DeviceName("Touchscreen", 0));
				_devices.Add(_touchscreen);
			}

			var touchId = s_NextTouchId;
			s_NextTouchId = s_NextTouchId >= 0xffffff ? 1 : s_NextTouchId + 1;
			var created = new VirtualFinger(this, _touchscreen, touchId);
			_fingers.Add(touchId, created);
			return created;
		}

		void OnChange(InputChange change)
		{
			if (_touchscreen == null || change.Device != _touchscreen)
			{
				return;
			}

			switch (change)
			{
				case TouchChange touch:
				{
					if (!_fingers.TryGetValue(touch.TouchId, out var finger))
					{
						return;
					}

					Touch(finger);
					finger.Update.LastPosition = touch.Position;
					finger.Update.LastOrder = touch.Order;
					finger.Update.LastFrame = touch.Frame;
					switch (touch)
					{
						case TouchBegan _:
							finger.BeganProcessed = true;
							finger.PressedFrame = touch.Frame;
							if (finger.State == FingerState.Queued)
							{
								finger.State = FingerState.Pressed;
							}

							finger.Update.Pressed = true;
							break;
						case TouchMoved _:
							if (finger.Update.FirstBeyondOrder < 0 && finger.Predictor.IsBeyondDragThreshold(touch.Position))
							{
								finger.Update.FirstBeyondOrder = touch.Order;
								finger.Update.FirstBeyondFrame = touch.Frame;
							}

							break;
						default:
							finger.State = FingerState.Released;
							finger.ReleasedFrame = touch.Frame;
							finger.Update.Released = true;
							break;
					}

					break;
				}
				case DeviceReset reset:
				{
					foreach (var finger in _fingers.Values)
					{
						if (finger.State == FingerState.Released || !finger.BeganProcessed)
						{
							continue;
						}

						Touch(finger);
						finger.State = FingerState.Released;
						finger.ReleasedFrame = reset.Frame;
						finger.Update.Released = true;
						finger.Update.LastOrder = reset.Order;
						finger.Update.LastFrame = reset.Frame;
					}

					break;
				}
			}
		}

		void Touch(VirtualFinger finger)
		{
			if (!finger.Update.Touched)
			{
				finger.Update = FingerUpdate.Fresh;
				finger.Update.LastPosition = finger.Position;
				_touched.Add(finger);
			}
		}

		void OnBeforeUpdate()
		{
			if (InputState.currentUpdateType != InputUpdateType.Dynamic)
			{
				return;
			}

			if (_draining && _drainStage >= 2)
			{
				TearDown();
			}
		}

		void OnAfterUpdate()
		{
			if (_tornDown || InputState.currentUpdateType != InputUpdateType.Dynamic)
			{
				return;
			}

			++_endedUpdates;

			foreach (var finger in _fingers.Values)
			{
				if (finger.BeganProcessed)
				{
					if (finger.State == FingerState.ReleaseQueued && finger.ReleaseStamp < _endedUpdates)
					{
						finger.ReleaseLost = true;
						finger.State = FingerState.Released;
					}
				}
				else if (finger.PressStamp >= 0
					&& finger.PressStamp < _endedUpdates
					&& finger.State != FingerState.Released)
				{
					finger.Dropped = true;
					finger.State = FingerState.Released;
				}
			}

			foreach (var finger in _touched)
			{
				var update = finger.Update;
				finger.Update.Touched = false;
				if (update.Pressed || update.Released || finger.BeganProcessed)
				{
					finger.Predictor.Process(
						update.LastPosition,
						update.Pressed,
						update.Released,
						update.LastOrder,
						update.LastFrame,
						update.FirstBeyondOrder,
						update.FirstBeyondFrame);
				}
			}

			_touched.Clear();

			RaiseInputUpdated();

			_settled.Clear();
			foreach (var finger in _fingers.Values)
			{
				if (finger.State == FingerState.Released)
				{
					_settled.Add(finger);
				}
			}

			foreach (var finger in _settled)
			{
				_fingers.Remove(finger.TouchId);
			}

			_settled.Clear();

			if (_draining)
			{
				if (_drainStage == 0)
				{
					if (_fingers.Count == 0)
					{
						_drainStage = 1;
					}
				}
				else
				{
					++_drainStage;
				}
			}

			var eventSystem = EventSystem.current;
			if (eventSystem != null && eventSystem.currentInputModule is InputSystemUIInputModule module)
			{
				TrackModule(module);
			}
		}

		void RaiseInputUpdated()
		{
			var handlers = _inputUpdated;
			if (handlers == null)
			{
				return;
			}

			foreach (var handler in handlers.GetInvocationList())
			{
				try
				{
					((Action)handler)();
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}
		}

		void TrackModule(InputSystemUIInputModule module)
		{
			if (module == null || _modules.ContainsKey(module))
			{
				return;
			}

			_modules[module] = module.pointerBehavior;
			module.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
		}

		void OnQuitting()
		{
			TearDown();
		}

#if UNITY_EDITOR
		void OnPlayModeStateChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.ExitingPlayMode)
			{
				TearDown();
			}
		}
#endif

		/// <summary>
		/// Tells whether the Input System that is in use now has the device. A device of an Input System that was
		/// reset (as tests do) still says that it was added.
		/// </summary>
		static bool IsInInputSystem(InputDevice device)
		{
			if (device == null || !device.added)
			{
				return false;
			}

			foreach (var candidate in InputSystem.devices)
			{
				if (candidate == device)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Removes the devices, gives the settings back and stops listening, at once.
		/// </summary>
		void TearDown()
		{
			if (_tornDown)
			{
				return;
			}

			_tornDown = true;
			if (s_Instance == this)
			{
				s_Instance = null;
			}

			InputSystem.onBeforeUpdate -= OnBeforeUpdate;
			InputSystem.onAfterUpdate -= OnAfterUpdate;
			Application.quitting -= OnQuitting;
#if UNITY_EDITOR
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
			_reader.Changed -= OnChange;
			_reader.Dispose();

			foreach (var device in _devices)
			{
				if (IsInInputSystem(device))
				{
					InputSystem.RemoveDevice(device);
				}
			}

			_devices.Clear();
			_fingers.Clear();
			_touched.Clear();

			foreach (var pair in _modules)
			{
				if (pair.Key != null)
				{
					pair.Key.pointerBehavior = pair.Value;
				}
			}

			_modules.Clear();

			if (InputSystem.settings == _replacedSettings)
			{
				InputSystem.settings = _originalSettings;
			}

			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(_replacedSettings);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(_replacedSettings);
			}

			_inputUpdated = null;
		}
	}
}
#endif
