#if ENABLE_AUTOTAP
using UnityAutoTap;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sample
{
	public partial class ScenarioBasedAutoTap
	{
		/// <summary>
		/// Puts the virtual mouse on a <see cref="HoverHighlight"/>, waits until it is hovered, moves the mouse away and
		/// waits until it is not hovered any more.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Needs in the scene:
		/// </para>
		/// <list type="bullet">
		/// <item><description>
		/// An <see cref="UnityEngine.InputSystem.UI.InputSystemUIInputModule"/> on the event system.
		/// </description></item>
		/// <item><description>
		/// A <see cref="HoverHighlight"/> on a graphic that receives raycasts, under a canvas with a
		/// <see cref="UnityEngine.UI.GraphicRaycaster"/>, near the bottom of the screen where nothing else is: the mouse is
		/// moved to a point above it. It can be under another canvas than the main one: the <c>ShouldNotPressOn</c> of the
		/// sample's <c>ScenarioBasedAutoTap</c> keeps the other scenarios from pressing outside the main canvas.
		/// </description></item>
		/// </list>
		/// <para>
		/// Hovering presses nothing, so <c>ShouldNotPressOn</c> is not asked. The mouse stays where the operation leaves
		/// it, and what is under it stays hovered.
		/// </para>
		/// </remarks>
		public class HoverAndLeave : ScenarioBase<ScenarioConfig<HoverAndLeave>>
		{
			public override string Status => "HoverAndLeave";

			public new class Config : ScenarioConfig<HoverAndLeave>
			{
			}

			HoverHighlight _target;
			AutoTapBase.LogItem _item;

			HoverAndLeave(AutoTapBase owner, Config config) : base(owner, config)
			{
			}

			public override void Dispose()
			{
				base.Dispose();
				_target = null;
				_item = null;
			}

			public override void UpdateActive()
			{
				Active = _mainCanvas != null;
			}

			public override void Prepare()
			{
				_item = null;
			}

			public override void OnPreUpdate(float deltaTime)
			{
				if (_target == null)
				{
					_target = Object.FindAnyObjectByType<HoverHighlight>();
				}

				if (_target == null)
				{
					Active = false;
				}
			}

			public override void OnUpdate(int index)
			{
				if (index != 0 || _target == null)
				{
					return;
				}

				if (_item != null)
				{
					if (!_item.Completed)
					{
						return;
					}

					if (_item.EndReason != AutoTapBase.EndReason.Finished)
					{
						Debug.LogWarning($"HoverAndLeave: the operation ended as {_item.EndReason}");
					}

					_item = null;
					RepeatCondition.OnLoop();
					return;
				}

				var rect = (RectTransform)_target.transform;
				var camera = _target.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
				var on = camera != null ? GetScreenPoint(camera, rect) : GetScreenPoint(rect);
				var away = new Vector2(on.x, Mathf.Min(on.y + 200f, Screen.height - 1f));

				// HoldUntil waits for the UI to react to the input; its condition is evaluated once per Update.
				_item = Simulate<Mouse>(Gesture
					.HoverAt(on)
					.HoldUntil(() => _target != null && _target.Hovered, 1f)
					.Hold(0.2f)
					.MoveTo(away, 0.2f)
					.HoldUntil(() => _target != null && !_target.Hovered, 1f));
			}
		}
	}
}
#endif
