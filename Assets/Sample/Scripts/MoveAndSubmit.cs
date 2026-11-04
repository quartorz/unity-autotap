#if ENABLE_AUTOTAP
using System.Collections.Generic;
using UnityAutoTap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Sample
{
	public partial class ScenarioBasedAutoTap
	{
		/// <summary>
		/// Moves the selection along a <see cref="PadPanel"/> with the left stick of a virtual gamepad, one flick for each
		/// step, and decides with the south button. It goes to the last button, or back to the first one if it starts at
		/// the first.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Needs in the scene:
		/// </para>
		/// <list type="bullet">
		/// <item><description>
		/// An <see cref="UnityEngine.InputSystem.UI.InputSystemUIInputModule"/> on the event system, with the default
		/// actions: the stick navigates and the south button submits.
		/// </description></item>
		/// <item><description>
		/// A <see cref="PadPanel"/>, and the first button of it as <see cref="EventSystem.firstSelectedGameObject"/>,
		/// which is what the module selects when nothing is.
		/// </description></item>
		/// </list>
		/// <para>
		/// A click selects what it hits, so when the selection is not on the panel the scenario puts it on the first
		/// button, which is where <see cref="EventSystem.firstSelectedGameObject"/> starts it. Everything else goes
		/// through the gamepad.
		/// </para>
		/// </remarks>
		public class MoveAndSubmit : ScenarioBase<ScenarioConfig<MoveAndSubmit>>
		{
			public override string Status => "MoveAndSubmit";

			public new class Config : ScenarioConfig<MoveAndSubmit>
			{
			}

			PadPanel _pad;
			AutoTapBase.LogItem _item;

			MoveAndSubmit(AutoTapBase owner, Config config) : base(owner, config)
			{
			}

			public override void Dispose()
			{
				base.Dispose();
				_pad = null;
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
				if (_pad == null)
				{
					_pad = Object.FindAnyObjectByType<PadPanel>();
				}

				if (_pad == null)
				{
					Active = false;
				}
			}

			public override void OnUpdate(int index)
			{
				if (index != 0 || _pad == null)
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
						Debug.LogWarning($"MoveAndSubmit: the operation ended as {_item.EndReason}");
					}

					_item = null;
					RepeatCondition.OnLoop();
					return;
				}

				// The event system is disabled while the menu slides.
				var eventSystem = EventSystem.current;
				if (eventSystem == null)
				{
					return;
				}

				var buttons = _pad.Buttons;
				var from = IndexOf(buttons, eventSystem.currentSelectedGameObject);
				if (from < 0)
				{
					eventSystem.SetSelectedGameObject(buttons[0].gameObject);
					return;
				}

				var goal = from == 0 ? buttons.Count - 1 : 0;
				var direction = goal > from ? Vector2.right : Vector2.left;

				// The navigation repeats while the stick is held, so each step is a flick: tilt, then back to the center.
				var sequence = new ControlSequence();
				for (var i = 0; i < Mathf.Abs(goal - from); ++i)
				{
					sequence.Set("leftStick", direction).Hold(0.1f).Set("leftStick", Vector2.zero).Hold(0.1f);
				}

				var submitted = _pad.SubmitCount;
				sequence
					.Press(GamepadButton.South)
					.HoldUntil(() => _pad.SubmitCount > submitted, 1f)
					.Release(GamepadButton.South);
				_item = Simulate<Gamepad>(sequence);
			}

			static int IndexOf(IReadOnlyList<Button> buttons, GameObject selected)
			{
				for (var i = 0; i < buttons.Count; ++i)
				{
					if (buttons[i].gameObject == selected)
					{
						return i;
					}
				}

				return -1;
			}
		}
	}
}
#endif
