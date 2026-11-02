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

		/// <inheritdoc cref="AutoTapBase.Fire(int, Vector2, Vector2, float)"/>
		protected AutoTapBase.LogItem Fire(int index, Vector2 from, Vector2 to, float duration)
		{
			return Owner.Fire(index, from, to, duration);
		}

		/// <inheritdoc cref="AutoTapBase.Fire(int, Vector2, float)"/>
		protected AutoTapBase.LogItem Fire(int index, Vector2 position, float duration)
		{
			return Owner.Fire(index, position, duration);
		}

		/// <inheritdoc cref="AutoTapBase.Fire(int, Gesture)"/>
		protected AutoTapBase.LogItem Fire(int index, Gesture gesture)
		{
			return Owner.Fire(index, gesture);
		}

		/// <inheritdoc cref="AutoTapBase.FirePointer"/>
		protected AutoTapBase.LogItem FirePointer(string layout, Gesture gesture, int number = 0)
		{
			return Owner.FirePointer(layout, gesture, number);
		}

		/// <inheritdoc cref="AutoTapBase.FireDevice"/>
		protected AutoTapBase.LogItem FireDevice(string layout, ControlSequence sequence, int number = 0)
		{
			return Owner.FireDevice(layout, sequence, number);
		}

		/// <inheritdoc cref="AutoTapBase.FireMouse"/>
		protected AutoTapBase.LogItem FireMouse(Gesture gesture, int number = 0)
		{
			return Owner.FireMouse(gesture, number);
		}

		/// <inheritdoc cref="AutoTapBase.FirePen"/>
		protected AutoTapBase.LogItem FirePen(Gesture gesture, int number = 0)
		{
			return Owner.FirePen(gesture, number);
		}

		/// <inheritdoc cref="AutoTapBase.FireScroll"/>
		protected AutoTapBase.LogItem FireScroll(Vector2 screenPosition, Vector2 delta)
		{
			return Owner.FireScroll(screenPosition, delta);
		}

		/// <inheritdoc cref="AutoTapBase.FireGamepad"/>
		protected AutoTapBase.LogItem FireGamepad(ControlSequence sequence, int number = 0)
		{
			return Owner.FireGamepad(sequence, number);
		}

		/// <inheritdoc cref="AutoTapBase.FireKey"/>
		protected AutoTapBase.LogItem FireKey(Key key, float duration = 0f, params Key[] modifiers)
		{
			return Owner.FireKey(key, duration, modifiers);
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
		/// Tap if <paramref name="target"/> is not destroyed.
		/// </summary>
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
				var fired = Fire(index, p, duration);
				return fired != null && fired.Target == target.gameObject;
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