#if ENABLE_AUTOTAP
using UnityAutoTap;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sample
{
	public partial class ScenarioBasedAutoTap
	{
		/// <summary>
		/// Opens the menu of the header by tapping its button, and closes it by pressing Escape on a virtual keyboard.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Needs in the scene:
		/// </para>
		/// <list type="bullet">
		/// <item><description>
		/// An <see cref="UnityEngine.InputSystem.UI.InputSystemUIInputModule"/> on the event system, whose cancel action
		/// is bound to the escape key (the default actions are).
		/// </description></item>
		/// <item><description>
		/// A <see cref="MenuCancelHandler"/> on the <c>MenuButton</c> of the header, which refers to the menu.
		/// </description></item>
		/// </list>
		/// <para>
		/// A key is not a pointer, so it does not use a slot: <c>FireKey</c> returns the record of the operation, and the
		/// scenario waits for <see cref="AutoTapBase.LogItem.Completed"/> by itself. The tap that opens the menu does use
		/// a slot, so it is made in slot 0 and the scenario works only there.
		/// </para>
		/// <para>
		/// The menu has to be closed when the scenario starts.
		/// </para>
		/// </remarks>
		public class CloseMenuWithEscape : ScenarioBase<ScenarioConfig<CloseMenuWithEscape>>
		{
			enum Phase
			{
				OpenMenu,
				WaitForOpened,
				WaitForClosed,
			}

			public override string Status => $"CloseMenuWithEscape ({_phase})";

			public new class Config : ScenarioConfig<CloseMenuWithEscape>
			{
			}

			Phase _phase;
			RectTransform _menuButton;
			AutoTapBase.LogItem _escape;

			CloseMenuWithEscape(AutoTapBase owner, Config config) : base(owner, config)
			{
			}

			public override void Dispose()
			{
				base.Dispose();
				_menuButton = null;
				_escape = null;
			}

			public override void UpdateActive()
			{
				Active = _mainCanvas != null && _screen is HomeScreen;
			}

			public override void Prepare()
			{
				_phase = Phase.OpenMenu;
				_escape = null;
			}

			public override void OnPreUpdate(float deltaTime)
			{
				if (_menuButton == null)
				{
					_menuButton = GetComponent<RectTransform>(ScreenManager.Header, "MenuButton");
				}
			}

			public override void OnUpdate(int index)
			{
				if (index != 0)
				{
					return;
				}

				var menu = ScreenManager.Header.Menu;
				switch (_phase)
				{
					case Phase.OpenMenu:
						// A tap selects the button, and the cancel that follows is delivered to what is selected.
						if (!menu.gameObject.activeSelf && TryTap(index, _mainCanvas, _menuButton, 0.1f))
						{
							_phase = Phase.WaitForOpened;
						}

						break;
					case Phase.WaitForOpened:
						// The menu disables the event system while it slides in, so the key waits until it ended.
						if (menu.Opened)
						{
							_escape = FireKey(Key.Escape, 0.1f);
							if (_escape != null)
							{
								_phase = Phase.WaitForClosed;
							}
						}

						break;
					case Phase.WaitForClosed:
						if (_escape.Completed && !menu.gameObject.activeSelf)
						{
							_phase = Phase.OpenMenu;
							RepeatCondition.OnLoop();
						}

						break;
				}
			}
		}
	}
}
#endif
