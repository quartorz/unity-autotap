using System;
using System.Linq;
using AutoTap;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sample
{
	public partial class ScenarioBasedAutoTap
	{
		static Canvas _mainCanvas;
		static ScreenBase _screen;

		static void UpdateSceneData()
		{
			if (_screen == null)
			{
				_screen = ScreenManager.Current;
			}

			if (_mainCanvas == null)
			{
				var scene = SceneManager.GetSceneAt(0);

				if (scene.name != "SampleScene")
				{
					_mainCanvas = null;
					return;
				}

				foreach (var o in scene.GetRootGameObjects())
				{
					if (o.name == "MainCanvas")
					{
						_mainCanvas = o.GetComponent<Canvas>();
						break;
					}
				}
			}
		}

		public abstract class ScenarioBase<TConfig> : Scenario<TConfig> where TConfig : ScenarioConfig
		{
			public override bool Active { get; protected set; }

			protected ScenarioBase(AutoTapBase owner, TConfig config) : base(owner, config)
			{
			}
		}

		public class Login : ScenarioBase<ScenarioConfig<Login>>
		{
			public override string Status => "Login";

			public new class Config : ScenarioConfig<Login>
			{
			}

			TitleScreen _titleScreen;
			RectTransform _target;

			Login(AutoTapBase owner, Config config) : base(owner, config)
			{
			}

			public override void Dispose()
			{
				_target = null;
			}

			public override void UpdateActive()
			{
				_titleScreen = _screen as TitleScreen;
				Active = _mainCanvas != null && _titleScreen != null;
			}

			public override void OnPreUpdate(float deltaTime)
			{
				if (_target == null)
				{
					_target = GetComponent<RectTransform>(_titleScreen, "Button");
				}
			}

			public override void OnUpdate(int index)
			{
				TryTap(index, _mainCanvas, _target, 0.1f);
			}
		}

		public class ReturnToTitle : ScenarioBase<ScenarioConfig<ReturnToTitle>>
		{
			public override string Status => "ReturnToTitle";

			public new class Config : ScenarioConfig<ReturnToTitle>
			{
			}

			RectTransform _target;

			ReturnToTitle(AutoTapBase owner, Config config) : base(owner, config)
			{
			}

			public override void UpdateActive()
			{
				Active = _mainCanvas != null && _screen is HomeScreen;
			}

			public override void OnPreUpdate(float deltaTime)
			{
				if (ScreenManager.Header.Menu.Opened)
				{
					var button = GetComponents<Button>(ScreenManager.Header.Menu.transform).LastOrDefault();
					if (button != null)
					{
						_target = button.transform as RectTransform;
					}
				}
				else
				{
					_target = GetComponent<RectTransform>(ScreenManager.Header, "MenuButton");
				}
			}

			public override void OnUpdate(int index)
			{
				TryTap(index, _mainCanvas, _target, 0.1f);
			}
		}

		public class Tap : ScenarioBase<Tap.Config>
		{
			public new class Config : ScenarioConfig
			{
				public string Screen;
				public string Target;

				public override Scenario Generate(AutoTapBase owner)
				{
					return new Tap(owner, this);
				}
			}

			Type _screenType;
			RectTransform _target;

			Tap(AutoTapBase owner, Config config) : base(owner, config)
			{
				_screenType = Type.GetType($"Sample.{base.Config.Screen}");

				if (_screenType == null)
				{
					throw new Exception($"invalid screen name: {base.Config.Screen}");
				}
			}

			public override void Dispose()
			{
				base.Dispose();
				_screenType = null;
				_target = null;
			}

			public override void UpdateActive()
			{
				Active = _screen.GetType() == _screenType;
			}

			public override void OnPreUpdate(float deltaTime)
			{
				if (_target == null)
				{
					_target = GetComponent<RectTransform>(_screen, base.Config.Target);
				}

				if (_target == null)
				{
					Active = false;
				}
			}

			public override void OnUpdate(int index)
			{
				if (TryTap(index, _mainCanvas, _target, 0.1f))
				{
					RepeatWhile.OnLoop();
				}
			}
		}

		public class CreateItem : ScenarioBase<CreateItem.Config>
		{
			public new class Config : ScenarioConfig
			{
				public string Text;

				public override Scenario Generate(AutoTapBase owner)
				{
					return new CreateItem(owner, this);
				}
			}

			Form _form;

			InputField _inputField;
			RectTransform _target;

			int _count;

			CreateItem(AutoTapBase owner, Config config) : base(owner, config)
			{
			}

			public override void UpdateActive()
			{
				Active = _screen is HomeScreen;
			}

			public override void Prepare()
			{
				_count = 0;
			}

			public override void OnPreUpdate(float deltaTime)
			{
				if (_form == null)
				{
					_form = _screen.GetComponentInChildren<Form>(true);
				}

				if (!_form.gameObject.activeSelf)
				{
					_target = GetComponent<RectTransform>(_screen, "OpenForm");
				}
				else
				{
					if (_inputField == null)
					{
						_inputField = _form.GetComponentInChildren<InputField>();
					}

					_inputField.text = base.Config.Text ?? $"Item {_count}";
					_target = GetComponent<RectTransform>(_form, "Submit");
				}
			}

			public override void OnUpdate(int index)
			{
				if (index != 0)
				{
					return;
				}

				if (TryTap(index, _mainCanvas, _target, 0.1f) && _target.name == "Submit")
				{
					_inputField.text = base.Config.Text ?? $"Item {++_count}";
					RepeatWhile.OnLoop();
				}
			}
		}

		public class DeleteItem : ScenarioBase<ScenarioConfig<DeleteItem>>
		{
			public new class Config : ScenarioConfig<DeleteItem>
			{
			}

			RectTransform _button;
			RectTransform _scrollRect;

			float _timer;

			DeleteItem(AutoTapBase owner, ScenarioConfig<DeleteItem> config) : base(owner, config)
			{
			}

			public override void UpdateActive()
			{
				Active = _screen is HomeScreen;
			}

			public override void OnPreUpdate(float deltaTime)
			{
				if (_button == null)
				{
					_button = GetComponent<RectTransform>(_screen, "DeleteButton");
				}

				if (_scrollRect == null)
				{
					_scrollRect = GetComponent<RectTransform>(_screen, "Scroll");
				}

				_timer -= deltaTime;
			}

			public override void OnUpdate(int index)
			{
				if (index != 0)
				{
					return;
				}

				if (_timer > 0)
				{
					return;
				}

				if (TryTap(index, _mainCanvas, _button, 0.1f))
				{
					RepeatWhile.OnLoop();
					_timer = 0.4f;
				}
				else
				{
					Fire(index, GetScreenPoint(_mainCanvas.worldCamera, _scrollRect, 0.5f, 0.4f),
						GetScreenPoint(_mainCanvas.worldCamera, _scrollRect, 0.5f, 0.6f), 0.5f);
					_timer = 0.7f;
				}
			}
		}

		public class Relogin : Group
		{
			public new class Config : ScenarioConfig
			{
				public override Scenario Generate(AutoTapBase owner)
				{
					return new Relogin(owner, this);
				}
			}

			Relogin(AutoTapBase owner, Config config)
				: base(owner, config.RepeatWhile, new[]
				{
					new ReturnToTitle.Config().Generate(owner),
					new Login.Config().Generate(owner),
				})
			{
			}
		}
	}
}