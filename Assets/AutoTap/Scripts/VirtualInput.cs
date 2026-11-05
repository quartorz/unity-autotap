#if ENABLE_AUTOTAP
using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap
{
	/// <summary>
	/// Adds virtual devices to the Input System and queues input to them. Both AutoTap and a replay press through
	/// this class.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A virtual device is added the first time its (layout, number) pair is used during a run, not when
	/// <see cref="Acquire"/> is called. Devices that are not used are not added.
	/// </para>
	/// <para>
	/// <see cref="Acquire"/> is reference counted. While at least one session is alive:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// In the Editor only, the Input System settings that decide whether input reaches the game are replaced by a
	/// copy with <see cref="InputSettings.backgroundBehavior"/> set to
	/// <see cref="InputSettings.BackgroundBehavior.IgnoreFocus"/> and
	/// <see cref="InputSettings.editorInputBehaviorInPlayMode"/> set to
	/// <see cref="InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView"/>, so that input is
	/// delivered regardless of focus. The original settings are restored when the last session is disposed. A player
	/// keeps the settings of the project.
	/// </description></item>
	/// <item><description>
	/// On every platform, the pointer behavior of <see cref="UnityEngine.InputSystem.UI.InputSystemUIInputModule"/>
	/// is set to <see cref="UnityEngine.InputSystem.UI.UIPointerBehavior.AllPointersAsIs"/>, so that virtual fingers,
	/// mice and pens do not cancel each other. The original behavior is restored when the last session is disposed.
	/// </description></item>
	/// </list>
	/// <para>
	/// When the last session is disposed, everything that is still pressed is released, and the virtual devices are
	/// removed after the release was processed.
	/// </para>
	/// <para>
	/// Virtual devices that a previous run left behind are removed at start-up, when a session is acquired while none
	/// is alive, and at <see cref="UnityEngine.Application.quitting"/>. In the Editor, the virtual devices that are
	/// still there when Play Mode is exited are removed at that point, without waiting for a release to be processed.
	/// When the application quits and when Play Mode is exited, the auto tap that is running
	/// (<see cref="AutoTapBase.Current"/>) is stopped before the devices are removed.
	/// </para>
	/// <para>
	/// While virtual devices exist, <c>Touchscreen.current</c>, <c>Keyboard.current</c>, <c>Mouse.current</c>,
	/// <c>Pointer.current</c>, <c>Gamepad.all</c>, <see cref="InputSystem.onDeviceChange"/> and the control scheme
	/// chosen by a <c>PlayerInput</c> can change.
	/// </para>
	/// </remarks>
	public static class VirtualInput
	{
		/// <summary>
		/// Starts using virtual input.
		/// </summary>
		/// <returns>
		/// A new handle. Dispose it to give up the reference taken by this call; disposing a handle twice has no
		/// further effect.
		/// </returns>
		/// <exception cref="InvalidOperationException">
		/// <see cref="InputSettings.updateMode"/> is not <see cref="InputSettings.UpdateMode.ProcessEventsInDynamicUpdate"/>.
		/// Virtual input works only when events are processed in the dynamic update.
		/// </exception>
		public static VirtualInputSession Acquire()
		{
			return VirtualInputCore.Acquire();
		}

		/// <summary>
		/// Tells whether <paramref name="device"/> was added by <see cref="VirtualInput"/>.
		/// </summary>
		/// <param name="device">The device to examine.</param>
		/// <returns><c>true</c> for a virtual device, <c>false</c> for a real device or <c>null</c>.</returns>
		public static bool IsVirtual(InputDevice device)
		{
			return device != null && VirtualInputCore.IsVirtualName(device.name);
		}

		/// <summary>
		/// Gives the number of a virtual device: the <c>number</c> that tells it apart from the other devices of its
		/// layout, as passed to <see cref="IVirtualInput.GetPointer(string, int)"/>,
		/// <see cref="IVirtualInput.GetDevice(string, int)"/> or their generic forms. The virtual touchscreen, whose
		/// fingers <see cref="IVirtualInput.NewFinger"/> gives, has the number 0.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The number is the one that the device was added with. It is known while the virtual input holds the device:
		/// from the time the device is added until it is removed.
		/// </para>
		/// </remarks>
		/// <param name="device">The device to examine.</param>
		/// <param name="number">The number of the device, or 0 if this method returns <c>false</c>.</param>
		/// <returns>
		/// <c>true</c> for a device that the virtual input holds. <c>false</c> for a real device, for <c>null</c>, and for
		/// a virtual device that was removed or that an earlier run left behind (<see cref="IsVirtual"/> is <c>true</c>
		/// for these).
		/// </returns>
		public static bool TryGetNumber(InputDevice device, out int number)
		{
			return VirtualInputCore.TryGetNumber(device, out number);
		}
	}

	/// <summary>
	/// The part of a session that <see cref="AutoTapBase"/> uses: the source of the virtual devices that it
	/// operates, the reader of their changes and the notification at the end of each input update.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It is a way to share one session. <see cref="VirtualInputSession"/> is the only implementation, because the
	/// virtual fingers, pointers and devices that its members return can only be created by a session. Pass the
	/// session that <see cref="VirtualInput.Acquire"/> returned to <see cref="AutoTapBase(int, IVirtualInput)"/> so that
	/// an auto tap and any other user of the same virtual devices, such as a replay, work on the same devices and
	/// read the same <see cref="Reader"/>. The caller that acquired the session disposes it.
	/// </para>
	/// </remarks>
	public interface IVirtualInput
	{
		/// <summary>
		/// Gives a new finger of the virtual touchscreen.
		/// </summary>
		/// <returns>
		/// The finger, or <c>null</c> if all fingers are in use. At most 10 fingers, the number of touches that a
		/// touchscreen has, are in use at a time.
		/// </returns>
		VirtualFinger NewFinger();

		/// <summary>
		/// Cancels every touch of the virtual touchscreen that is in progress, as the soft reset of a
		/// <see cref="Touchscreen"/> by the Input System does.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Every finger that was pressed and was not released or canceled yet is canceled, as
		/// <see cref="VirtualFinger.Cancel"/> does: one event per finger, processed at the next input update. A finger
		/// whose press is queued but not processed yet is canceled too. Nothing is queued if no finger is pressed.
		/// </para>
		/// <para>
		/// The touches are over, as a touch of a real touchscreen is once it was canceled. The fingers cannot be used
		/// any more: <see cref="VirtualFinger.MoveTo"/>, <see cref="VirtualFinger.Release"/> and the other calls that
		/// change a touch throw <see cref="InvalidOperationException"/>, also when the owner of the finger makes them.
		/// To touch again, press a new finger (<see cref="NewFinger"/>). This differs from
		/// <see cref="VirtualDevice.ResetToDefault"/> of the other virtual devices, after which the device can be used
		/// on.
		/// </para>
		/// </remarks>
		void ResetTouchscreen();

		/// <summary>
		/// Gives a virtual pointer device other than a touchscreen.
		/// </summary>
		/// <param name="layout">The layout of a device derived from <see cref="Pointer"/>, for example <c>Mouse</c> or <c>Pen</c>.</param>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>The same instance for the same (layout, number) pair.</returns>
		/// <exception cref="ArgumentException">
		/// There is no such layout, or it is a touchscreen, or it is not derived from <see cref="Pointer"/>.
		/// </exception>
		VirtualPointer GetPointer(string layout, int number = 0);

		/// <summary>
		/// Gives a virtual pointer device other than a touchscreen, by its type.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The device is added with <see cref="InputSystem.AddDevice{TDevice}"/>, so the layout is the one that the Input
		/// System registered <typeparamref name="TDevice"/> under. It is the same device as the one that
		/// <see cref="GetPointer(string, int)"/> gives for the name of that layout and the same <paramref name="number"/>:
		/// <c>GetPointer&lt;Mouse&gt;()</c> and <c>GetPointer("Mouse")</c> are one <see cref="VirtualPointer"/>.
		/// </para>
		/// <para>
		/// <see cref="Touchscreen"/> is derived from <see cref="Pointer"/>, so the type constraint cannot refuse it; it
		/// is refused when this method is called.
		/// </para>
		/// </remarks>
		/// <typeparam name="TDevice">
		/// The type of the device, for example <see cref="Mouse"/> or <see cref="Pen"/>. It has to be registered as a layout
		/// of the Input System.
		/// </typeparam>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>The same instance for the same (layout, number) pair.</returns>
		/// <exception cref="ArgumentException">
		/// <typeparamref name="TDevice"/> is a <see cref="Touchscreen"/> or is not registered as a layout.
		/// </exception>
		VirtualPointer GetPointer<TDevice>(int number = 0) where TDevice : Pointer;

		/// <summary>
		/// Gives a virtual device that is not a pointer.
		/// </summary>
		/// <param name="layout">The layout of the device, for example <c>Keyboard</c> or <c>Gamepad</c>.</param>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>The same instance for the same (layout, number) pair.</returns>
		/// <exception cref="ArgumentException">There is no such layout, or it is derived from <see cref="Pointer"/>.</exception>
		VirtualDevice GetDevice(string layout, int number = 0);

		/// <summary>
		/// Gives a virtual device that is not a pointer, by its type.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The device is the same one that <see cref="GetDevice(string, int)"/> gives for the name of the layout of
		/// <typeparamref name="TDevice"/> and the same <paramref name="number"/>: <c>GetDevice&lt;Keyboard&gt;()</c> and
		/// <c>GetDevice("Keyboard")</c> are one <see cref="VirtualDevice"/>.
		/// </para>
		/// </remarks>
		/// <typeparam name="TDevice">
		/// The type of the device, for example <see cref="Keyboard"/> or <see cref="Gamepad"/>. It has to be registered as
		/// a layout of the Input System.
		/// </typeparam>
		/// <param name="number">Tells apart several devices of the same layout.</param>
		/// <returns>The same instance for the same (layout, number) pair.</returns>
		/// <exception cref="ArgumentException">
		/// <typeparamref name="TDevice"/> is derived from <see cref="Pointer"/> or is not registered as a layout.
		/// </exception>
		VirtualDevice GetDevice<TDevice>(int number = 0) where TDevice : InputDevice;

		/// <summary>
		/// Reports the changes of the virtual devices only, in processing order. Always the same instance.
		/// </summary>
		InputChangeReader Reader { get; }

		/// <summary>
		/// Raised at the end of each Dynamic input update, after the predictions of the virtual fingers and pointers
		/// were refreshed.
		/// </summary>
		event Action InputUpdated;
	}

	/// <summary>
	/// A handle to the virtual input obtained by <see cref="VirtualInput.Acquire"/>.
	/// </summary>
	public sealed class VirtualInputSession : IVirtualInput, IDisposable
	{
		readonly VirtualInputCore _core;
		bool _disposed;

		internal VirtualInputSession(VirtualInputCore core)
		{
			_core = core;
		}

		/// <inheritdoc/>
		public VirtualFinger NewFinger()
		{
			ThrowIfDisposed();
			return _core.NewFinger();
		}

		/// <inheritdoc/>
		public void ResetTouchscreen()
		{
			ThrowIfDisposed();
			_core.ResetTouchscreen();
		}

		/// <inheritdoc/>
		public VirtualPointer GetPointer(string layout, int number = 0)
		{
			ThrowIfDisposed();
			return _core.GetPointer(layout, number);
		}

		/// <inheritdoc/>
		public VirtualPointer GetPointer<TDevice>(int number = 0) where TDevice : Pointer
		{
			ThrowIfDisposed();
			return _core.GetPointer<TDevice>(number);
		}

		/// <inheritdoc/>
		public VirtualDevice GetDevice(string layout, int number = 0)
		{
			ThrowIfDisposed();
			return _core.GetDevice(layout, number);
		}

		/// <inheritdoc/>
		public VirtualDevice GetDevice<TDevice>(int number = 0) where TDevice : InputDevice
		{
			ThrowIfDisposed();
			return _core.GetDevice<TDevice>(number);
		}

		/// <inheritdoc/>
		public InputChangeReader Reader => _core.Reader;

		/// <inheritdoc/>
		public event Action InputUpdated
		{
			add => _core.InputUpdated += value;
			remove => _core.InputUpdated -= value;
		}

		/// <summary>
		/// Gives up the reference to the virtual input. Releasing what is pressed and removing the devices happens
		/// when the last reference is given up.
		/// </summary>
		/// <remarks>
		/// <para>
		/// When the last reference is given up and something is still pressed, the release is queued and the devices
		/// are removed a few input updates later, after the UI processed the release. When nothing is pressed they are
		/// removed at once. <see cref="Reader"/> and <see cref="InputUpdated"/> keep working until then.
		/// </para>
		/// </remarks>
		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_core.ReleaseReference();
		}

		void ThrowIfDisposed()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(VirtualInputSession));
			}
		}
	}
}
#endif
