#if ENABLE_AUTOTAP
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using LogEvent = UnityAutoTap.AutoTapBase.LogEvent;
using LogEventKind = UnityAutoTap.AutoTapBase.LogEventKind;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// The base of the tests that run on the fake input system of <see cref="InputTestFixture"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A frame of a test is <see cref="Step"/>: it waits for the next frame, so that the input update and the event
	/// system of that frame have run, and then calls <see cref="AutoTapBase.Update(float)"/> of <see cref="Auto"/> with
	/// <see cref="Dt"/>. This is the order a game runs <c>Update</c> in: input update, event system, auto tap.
	/// </para>
	/// <para>
	/// The input system of these tests is not driven by the real clock. In every Dynamic update, the clock of the input
	/// system advances by 1/30 s (<c>InputTestRuntime.Update</c> of the Input System 1.19.0), and
	/// <see cref="Dt"/> is what the auto tap counts. The two are independent.
	/// </para>
	/// </remarks>
	public abstract class AutoTapFixture : InputTestFixture
	{
		/// <summary>
		/// The time, in seconds, that <see cref="Step"/> passes to <see cref="AutoTapBase.Update(float)"/>.
		/// </summary>
		protected const float Dt = 0.1f;

		protected UiRig Rig { get; private set; }

		protected TestAutoTap Auto { get; private set; }

		protected VirtualInputSession Session { get; private set; }

		/// <summary>
		/// <c>true</c> to append <see cref="AutoTapBase.GetLatestLogs"/> to <see cref="LogTrace"/> after every
		/// <see cref="Step"/>.
		/// </summary>
		protected bool CollectLogs { get; set; }

		/// <summary>
		/// The elements of <see cref="AutoTapBase.GetLatestLogs"/> of every frame since <see cref="CollectLogs"/> was
		/// switched on, with the frame they were read in.
		/// </summary>
		protected readonly List<(int Frame, LogEvent Event)> LogTrace = new List<(int Frame, LogEvent Event)>();

		readonly List<Action> _cleanups = new List<Action>();

		/// <summary>
		/// Registers something to undo when the test ends, in the opposite order of the registration.
		/// </summary>
		protected void OnTearDown(Action cleanup)
		{
			_cleanups.Add(cleanup);
		}

		/// <inheritdoc/>
		public override void TearDown()
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

			try
			{
				if (Auto != null)
				{
					if (Auto.Enabled)
					{
						Auto.Stop();
					}

					Auto.Dispose();
				}
			}
			catch (Exception e)
			{
				TestContext.Error.WriteLine($"Disposing the auto tap failed: {e}");
			}

			Auto = null;

			try
			{
				Session?.Dispose();
			}
			catch (Exception e)
			{
				TestContext.Error.WriteLine($"Disposing the session failed: {e}");
			}

			Session = null;

			try
			{
				Rig?.Destroy();
			}
			catch (Exception e)
			{
				TestContext.Error.WriteLine($"Destroying the rig failed: {e}");
			}

			Rig = null;

			base.TearDown();
		}

		/// <summary>
		/// Builds <see cref="Rig"/> without waiting, so that things that the event system reads when it starts
		/// (<c>firstSelectedGameObject</c>) can still be set. Wait with <see cref="Settle"/> afterwards.
		/// </summary>
		protected void CreateRig()
		{
			Rig = UiRig.Create();
		}

		/// <summary>
		/// Builds <see cref="Rig"/> and lets the event system start.
		/// </summary>
		protected IEnumerator BuildRig()
		{
			CreateRig();
			Canvas.ForceUpdateCanvases();
			yield return null;
			yield return null;
		}

		/// <summary>
		/// Waits for a frame so that the layout of what was just added is done.
		/// </summary>
		protected IEnumerator Settle()
		{
			Canvas.ForceUpdateCanvases();
			yield return null;
			Canvas.ForceUpdateCanvases();
		}

		/// <summary>
		/// Acquires the virtual input as <see cref="Session"/>, for tests that use the devices directly or that
		/// share the numbering of the changes with the auto tap.
		/// </summary>
		protected VirtualInputSession AcquireSession()
		{
			Session = VirtualInput.Acquire();
			return Session;
		}

		/// <summary>
		/// Creates <see cref="Auto"/>, sets it up on <see cref="Rig"/> and starts it.
		/// </summary>
		/// <param name="useSession">
		/// <c>true</c> to let the auto tap operate <see cref="Session"/> (which is acquired if needed), so that the
		/// numbers of its log items can be compared with <see cref="IVirtualInput.Reader"/>. <c>false</c> to let the
		/// auto tap acquire the virtual input itself.
		/// </param>
		/// <param name="slotCount">The number of slots.</param>
		protected TestAutoTap StartAuto(bool useSession = true, int slotCount = 4)
		{
			if (useSession)
			{
				Auto = new TestAutoTap(Session ?? AcquireSession(), slotCount);
			}
			else
			{
				Auto = new TestAutoTap(slotCount);
			}

			Auto.SetupMarkers(Rig.Canvas, Rig.MarkerSprite);
			Auto.Start();
			return Auto;
		}

		/// <summary>
		/// Waits for the next frame, then runs <see cref="AutoTapBase.Update(float)"/> if <see cref="Auto"/> is
		/// running.
		/// </summary>
		/// <param name="inUpdate">
		/// Code to run inside that <c>Update</c>, before the slots are visited, which is where a scenario starts operations. It runs
		/// even if <see cref="Auto"/> is not running.
		/// </param>
		protected IEnumerator Step(Action inUpdate = null)
		{
			yield return null;
			Tick(inUpdate);
			if (CollectLogs && Auto != null)
			{
				foreach (var logEvent in Auto.GetLatestLogs())
				{
					LogTrace.Add((Frame, logEvent));
				}
			}
		}

		/// <summary>
		/// Runs <see cref="AutoTapBase.Update(float)"/> of <see cref="Auto"/> without waiting for a frame.
		/// </summary>
		protected void Tick(Action inUpdate = null)
		{
			if (Auto == null || !Auto.Enabled)
			{
				inUpdate?.Invoke();
				return;
			}

			Auto.PreUpdateAction = inUpdate;
			Auto.Update(Dt);
		}

		protected IEnumerator Steps(int count)
		{
			for (var i = 0; i < count; ++i)
			{
				yield return Step();
			}
		}

		/// <summary>
		/// Steps until <paramref name="condition"/> holds, and fails the test if it does not within
		/// <paramref name="maxFrames"/> frames.
		/// </summary>
		protected IEnumerator Until(Func<bool> condition, string what, int maxFrames = 60)
		{
			for (var i = 0; i < maxFrames; ++i)
			{
				if (condition())
				{
					yield break;
				}

				yield return Step();
			}

			if (!condition())
			{
				Assert.Fail($"Timed out after {maxFrames} frames, waiting for {what}.");
			}
		}

		protected static bool AnyVirtualDevice()
		{
			return InputSystem.devices.Any(VirtualInput.IsVirtual);
		}

		/// <summary>
		/// The virtual device of type <typeparamref name="T"/>. Fails if there is none or more than one.
		/// </summary>
		protected static T VirtualDeviceOf<T>() where T : InputDevice
		{
			var devices = InputSystem.devices.OfType<T>().Where(VirtualInput.IsVirtual).ToList();
			Assert.That(devices, Has.Count.EqualTo(1), $"virtual devices of type {typeof(T).Name}");
			return devices[0];
		}

		/// <summary>
		/// What <see cref="AutoTapBase.GetLatestLogs"/> returns now, as a list that does not change afterwards.
		/// </summary>
		protected List<LogEvent> LatestLogs()
		{
			return Auto.GetLatestLogs().ToList();
		}

		/// <summary>
		/// Describes the elements of a log, for the message of a failed assertion.
		/// </summary>
		protected static string Describe(IEnumerable<LogEvent> logs)
		{
			return string.Join(", ", logs.Select(l =>
				$"{l.Kind}(slot {l.Item.Index}, {l.Item.Kind}, order {(l.Order.HasValue ? l.Order.Value.ToString() : "-")})"));
		}

		/// <summary>
		/// The number of the frame that is running.
		/// </summary>
		protected static int Frame => Time.frameCount;

		/// <summary>
		/// A reader of every enabled device, real and virtual.
		/// </summary>
		protected ChangeLog ReadAllDevices(Func<InputDevice, bool> filter = null)
		{
			var log = new ChangeLog(filter);
			OnTearDown(log.Dispose);
			return log;
		}

		/// <summary>
		/// A real (not virtual) device, added by layout name in the way the virtual devices are, so that it has the
		/// same class as a virtual device of that layout.
		/// </summary>
		protected static T AddRealDevice<T>(string layout, string name) where T : InputDevice
		{
			return (T)InputSystem.AddDevice(layout, name);
		}
	}
}
#endif
