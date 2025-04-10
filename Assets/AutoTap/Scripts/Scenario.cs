#if ENABLE_AUTOTAP
using System;
using System.Collections.Generic;
using UnityEngine;

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

		public virtual bool ToBeIgnored(Transform transform) => false;
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

		protected GameObject Fire(int index, Vector2 from, Vector2 to, float duration)
		{
			return Owner.Fire(index, from, to, duration);
		}

		protected GameObject Fire(int index, Vector2 position, float duration)
		{
			return Owner.Fire(index, position, position, duration);
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
		protected bool TryTap(int index, Canvas canvas, RectTransform target, float duration)
		{
			if (target != null)
			{
				var p = canvas != null && canvas.worldCamera != null
					? GetScreenPoint(canvas.worldCamera, target)
					: GetScreenPoint(target);
				var fired = Fire(index, p, duration);
				return fired != null && fired == target.gameObject;
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