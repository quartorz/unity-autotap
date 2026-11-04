#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace UnityAutoTap
{
	public abstract class Scenario : IDisposable
	{
		protected AutoTapBase Owner;

		public abstract bool Active { get; protected set; }
		public virtual string Status => null;

		public abstract Condition RepeatCondition { get; }

		public bool IsActiveAndRepeatable => Active && RepeatCondition.Value;

		protected Scenario(AutoTapBase owner = null)
		{
			Owner = owner;
		}

		public virtual void Dispose()
		{
			Owner = null;
		}

		/// <summary>
		/// Decides whether a press is made at a point. See <see cref="AutoTapBase"/>'s <c>ShouldNotPressOn</c>, to
		/// which this is forwarded.
		/// </summary>
		/// <param name="topmost">The topmost hit at the point of the press.</param>
		/// <returns><c>true</c> not to press.</returns>
		public virtual bool ShouldNotPressOn(RaycastResult topmost) => false;
		public abstract void UpdateActive();

		public virtual void Prepare()
		{
		}

		public virtual void OnPreUpdate(float deltaTime)
		{
		}

		public virtual void OnUpdate(int index)
		{
		}

		public virtual void OnPostUpdate(float deltaTime)
		{
		}

		public virtual void OnTapCompleted(int index, AutoTapBase.LogItem logItem)
		{
		}

		/// <inheritdoc cref="AutoTapBase.Simulate(int, Gesture)"/>
		protected AutoTapBase.LogItem Simulate(int slot, Gesture gesture)
		{
			return Owner.Simulate(slot, gesture);
		}

		/// <inheritdoc cref="AutoTapBase.Simulate{TDevice}(Gesture, int)"/>
		protected AutoTapBase.LogItem Simulate<TDevice>(Gesture gesture, int number = 0) where TDevice : Pointer
		{
			return Owner.Simulate<TDevice>(gesture, number);
		}

		/// <inheritdoc cref="AutoTapBase.Simulate{TDevice}(ControlSequence, int)"/>
		protected AutoTapBase.LogItem Simulate<TDevice>(ControlSequence sequence, int number = 0) where TDevice : InputDevice
		{
			return Owner.Simulate<TDevice>(sequence, number);
		}

		/// <inheritdoc cref="AutoTapBase.Simulate(string, Gesture, int)"/>
		protected AutoTapBase.LogItem Simulate(string layout, Gesture gesture, int number = 0)
		{
			return Owner.Simulate(layout, gesture, number);
		}

		/// <inheritdoc cref="AutoTapBase.Simulate(string, ControlSequence, int)"/>
		protected AutoTapBase.LogItem Simulate(string layout, ControlSequence sequence, int number = 0)
		{
			return Owner.Simulate(layout, sequence, number);
		}

		/// <inheritdoc cref="AutoTapBase.Simulate(Key, float, Key[])"/>
		protected AutoTapBase.LogItem Simulate(Key key, float duration = 0f, params Key[] modifiers)
		{
			return Owner.Simulate(key, duration, modifiers);
		}

		/// <summary>
		/// Calculates the screen point of the transform located under the canvas with <see cref="Canvas.worldCamera"/>.
		/// </summary>
		protected Vector2 GetScreenPoint(Camera camera, RectTransform rectTransform)
		{
			return Owner.GetScreenPoint(camera, rectTransform);
		}

		/// <summary>
		/// Calculates the screen point of the transform located under the canvas with <see cref="Canvas.worldCamera"/>.
		/// </summary>
		protected Vector2 GetScreenPoint(Camera camera, RectTransform rectTransform, float tx, float ty)
		{
			return Owner.GetScreenPoint(camera, rectTransform, tx, ty);
		}

		/// <summary>
		/// Calculates the screen point of the transform located under the canvas that doesn't have a <see cref="Canvas.worldCamera"/>.
		/// </summary>
		protected Vector2 GetScreenPoint(RectTransform rectTransform)
		{
			return Owner.GetScreenPoint(rectTransform);
		}

		/// <summary>
		/// Calculates the screen point of the transform located under the canvas that doesn't have a <see cref="Canvas.worldCamera"/>.
		/// </summary>
		protected Vector2 GetScreenPoint(RectTransform rectTransform, float tx, float ty)
		{
			return Owner.GetScreenPoint(rectTransform, tx, ty);
		}

		/// <summary>
		/// Taps the center of <paramref name="target"/> with the finger of slot <paramref name="index"/> if it is not
		/// destroyed: <c>Simulate(index, Gesture.PressAt(point).Hold(duration))</c>, where <c>point</c> is the screen
		/// point of the center of <paramref name="target"/>, and a check that the tap reaches it.
		/// </summary>
		/// <param name="index">The slot.</param>
		/// <param name="canvas">
		/// The canvas that <paramref name="target"/> is under; its <see cref="Canvas.worldCamera"/>, if any, converts the
		/// position to the screen. <c>null</c> for a canvas that has no camera.
		/// </param>
		/// <param name="target">The object to tap.</param>
		/// <param name="duration">The time to hold the finger down, in seconds.</param>
		/// <returns>
		/// <c>true</c> if the tap was started and the press is predicted to be delivered to
		/// <paramref name="target"/>, that is, <see cref="AutoTapBase.LogItem.Target"/> at the time of the call is
		/// the game object of <paramref name="target"/>. The prediction follows the rules of the input module; it is
		/// refreshed when the press is processed.
		/// </returns>
		protected bool TryTap(int index, Canvas canvas, RectTransform target, float duration)
		{
			if (target != null)
			{
				var p = canvas != null && canvas.worldCamera != null
					? GetScreenPoint(canvas.worldCamera, target)
					: GetScreenPoint(target);
				var started = Simulate(index, Gesture.PressAt(p).Hold(Mathf.Max(0f, duration)));
				return started != null && started.Target == target.gameObject;
			}

			return false;
		}

		protected bool TryTap(int index, Canvas canvas, Component target, float duration)
		{
			return TryTap(index, canvas, target != null ? target.transform as RectTransform : null, duration);
		}

		protected IEnumerable<T> GetComponents<T>(Component root, bool includeInactive = false) where T : Component
		{
			return AutoTapBase.ComponentFinder<T>.GetComponents(root, includeInactive);
		}

		protected T GetComponent<T>(Component root, string name, bool includeInactive = false) where T : Component
		{
			return AutoTapBase.ComponentFinder<T>.GetComponent(root, name, includeInactive);
		}
	}

	public abstract class Scenario<TConfig> : Scenario where TConfig : ScenarioConfig
	{
		protected TConfig Config;
		public override Condition RepeatCondition => Config.RepeatCondition;

		protected Scenario(AutoTapBase owner, TConfig config) : base(owner)
		{
			Config = config;
		}
	}
}
#endif