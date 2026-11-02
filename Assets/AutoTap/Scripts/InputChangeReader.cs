#if ENABLE_AUTOTAP
using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace UnityAutoTap
{
	/// <summary>
	/// Reports the changes of input device states as <see cref="InputChange"/>s, one event at a time, in the order the
	/// Input System processed them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// What is watched:
	/// </para>
	/// <list type="bullet">
	/// <item><description>
	/// Updates: Dynamic updates, and the before-render update for devices that update before render.
	/// </description></item>
	/// <item><description>
	/// Devices: every enabled device, real and virtual. Narrow them down with the filter passed to the constructor.
	/// </description></item>
	/// </list>
	/// <para>
	/// What is reported: <see cref="TouchBegan"/>, <see cref="TouchMoved"/>, <see cref="TouchEnded"/>,
	/// <see cref="TouchCanceled"/>, <see cref="ButtonDown"/>, <see cref="ButtonUp"/>, <see cref="ValueChanged"/>,
	/// <see cref="Scrolled"/>, <see cref="TextEntered"/>, <see cref="CompositionChanged"/> and
	/// <see cref="DeviceReset"/>.
	/// </para>
	/// <para>
	/// What is not reported: events the Input System dropped (disabled device, focus handling, stale time stamp,
	/// handled by <see cref="InputSystem.onEvent"/>), and changes that are not caused by an event (the zeroing of
	/// deltas at the start of an update). Events that the Input System merged into a previous one are reported once,
	/// as the merged result.
	/// </para>
	/// <para>
	/// Raw events are neither recorded nor replayable through this class; use <see cref="InputEventTrace"/> for that.
	/// </para>
	/// <para>
	/// <see cref="Changed"/> is raised synchronously inside the input update, right after the state was written and
	/// before the actions are notified.
	/// </para>
	/// </remarks>
	public sealed class InputChangeReader : IDisposable
	{
		/// <summary>
		/// Starts reading.
		/// </summary>
		/// <param name="deviceFilter">
		/// Selects the devices to report. <c>null</c> reports every enabled device. The filter is evaluated for each
		/// change, so it sees devices that were added after the reader was created.
		/// </param>
		public InputChangeReader(Func<InputDevice, bool> deviceFilter = null)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Raised for each change, in the order of <see cref="InputChange.Order"/>.
		/// </summary>
		public event Action<InputChange> Changed
		{
			add => throw new NotImplementedException();
			remove => throw new NotImplementedException();
		}

		/// <summary>
		/// Raised at the end of each Dynamic input update, after every change of that update was raised through
		/// <see cref="Changed"/>. Use it to handle the changes frame by frame.
		/// </summary>
		public event Action UpdateEnded
		{
			add => throw new NotImplementedException();
			remove => throw new NotImplementedException();
		}

		/// <summary>
		/// Stops reading. No event is raised afterwards.
		/// </summary>
		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}
}
#endif
