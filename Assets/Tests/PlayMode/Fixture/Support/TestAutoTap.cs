#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// A <see cref="AutoTapBase.LogItem"/> of a class derived from it, to check <c>NewLogItem</c>.
	/// </summary>
	public sealed class TestLogItem : AutoTapBase.LogItem
	{
	}

	/// <summary>
	/// An <see cref="AutoTapBase"/> that exposes the <c>Simulate</c> methods, lets a test run code inside
	/// <see cref="AutoTapBase.Update(float)"/> and records the callbacks.
	/// </summary>
	public sealed class TestAutoTap : AutoTapBase
	{
		/// <summary>
		/// Run once, at the start of the next <see cref="AutoTapBase.Update(float)"/>, which is where scenarios start operations.
		/// </summary>
		public Action PreUpdateAction;

		/// <summary>
		/// Called for each slot that is empty, from <see cref="AutoTapBase.Update(float)"/>.
		/// </summary>
		public Action<int> IdleSlotAction;

		/// <summary>
		/// Decides what <c>ShouldNotPressOn</c> returns. <c>null</c> never refuses.
		/// </summary>
		public Func<RaycastResult, bool> Refuse;

		/// <summary>
		/// How many times <c>ShouldNotPressOn</c> was asked.
		/// </summary>
		public int AskedCount;

		/// <summary>
		/// The argument of the last <c>ShouldNotPressOn</c>.
		/// </summary>
		public RaycastResult LastTopmost;

		/// <summary>
		/// The callbacks of <see cref="AutoTapBase.Update(float)"/> in the order they were made:
		/// <c>preUpdate</c>, <c>completed:slot</c>, <c>idle:slot</c>, <c>postUpdate</c>.
		/// </summary>
		public readonly List<string> Trace = new List<string>();

		/// <summary>
		/// The slot operations that ended, in the order <c>OnTapCompleted</c> was called.
		/// </summary>
		public readonly List<(int Index, LogItem Item, int Frame)> CompletedSlots =
			new List<(int Index, LogItem Item, int Frame)>();

		/// <summary>
		/// The slots <c>NewLogItem</c> was called for.
		/// </summary>
		public readonly List<int> CreatedItems = new List<int>();

		/// <summary>
		/// Creates an auto tap that acquires the virtual input itself at <see cref="AutoTapBase.Start"/>.
		/// </summary>
		public TestAutoTap(int slotCount = 4) : base(slotCount)
		{
		}

		/// <summary>
		/// Creates an auto tap that operates the devices of <paramref name="input"/>, which the caller owns.
		/// </summary>
		public TestAutoTap(IVirtualInput input, int slotCount = 4) : base(slotCount, input)
		{
		}

		/// <inheritdoc/>
		protected override void OnPreUpdate(float deltaTime)
		{
			Trace.Add("preUpdate");
			var action = PreUpdateAction;
			PreUpdateAction = null;
			action?.Invoke();
		}

		/// <inheritdoc/>
		protected override void OnPostUpdate(float deltaTime)
		{
			Trace.Add("postUpdate");
		}

		/// <inheritdoc/>
		protected override void Update(int index)
		{
			Trace.Add($"idle:{index}");
			IdleSlotAction?.Invoke(index);
		}

		/// <inheritdoc/>
		protected override void OnTapCompleted(int index, LogItem logItem)
		{
			Trace.Add($"completed:{index}");
			CompletedSlots.Add((index, logItem, Time.frameCount));
		}

		/// <inheritdoc/>
		protected override bool ShouldNotPressOn(RaycastResult topmost)
		{
			++AskedCount;
			LastTopmost = topmost;
			return Refuse != null && Refuse(topmost);
		}

		/// <inheritdoc/>
		protected override LogItem NewLogItem(int index)
		{
			CreatedItems.Add(index);
			return new TestLogItem();
		}

		public LogItem SimulateTap(int index, Vector2 position, float duration)
		{
			return Simulate(index, Gesture.PressAt(position).Hold(Mathf.Max(0f, duration)));
		}

		public LogItem SimulateDrag(int index, Vector2 from, Vector2 to, float duration)
		{
			return Simulate(index, Gesture.PressAt(from).MoveTo(to, Mathf.Max(0f, duration)));
		}

		public LogItem SimulateGesture(int index, Gesture gesture)
		{
			return Simulate(index, gesture);
		}

		public LogItem SimulatePointerGesture(string layout, Gesture gesture, int number = 0)
		{
			return Simulate(layout, gesture, number);
		}

		public LogItem SimulatePointerGesture<TDevice>(Gesture gesture, int number = 0) where TDevice : Pointer
		{
			return Simulate<TDevice>(gesture, number);
		}

		public LogItem SimulateDeviceSequence(string layout, ControlSequence sequence, int number = 0)
		{
			return Simulate(layout, sequence, number);
		}

		public LogItem SimulateDeviceSequence<TDevice>(ControlSequence sequence, int number = 0) where TDevice : InputDevice
		{
			return Simulate<TDevice>(sequence, number);
		}

		public LogItem SimulateMouseGesture(Gesture gesture, int number = 0)
		{
			return Simulate<Mouse>(gesture, number);
		}

		public LogItem SimulatePenGesture(Gesture gesture, int number = 0)
		{
			return Simulate<Pen>(gesture, number);
		}

		public LogItem SimulateScrollAt(Vector2 screenPosition, Vector2 delta)
		{
			return Simulate<Mouse>(Gesture.HoverAt(screenPosition).Scroll(delta));
		}

		public LogItem SimulateGamepadSequence(ControlSequence sequence, int number = 0)
		{
			return Simulate<Gamepad>(sequence, number);
		}

		public LogItem SimulateKeyPress(Key key, float duration = 0f, params Key[] modifiers)
		{
			return Simulate(key, duration, modifiers);
		}
	}
}
#endif
