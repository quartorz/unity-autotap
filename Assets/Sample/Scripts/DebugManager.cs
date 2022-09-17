using System;
using System.Linq;
using AutoTap;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using static Sample.ScenarioBasedAutoTap;

namespace Sample
{
	public class DebugManager : MonoBehaviour
	{
		[SerializeField] Canvas canvas;

		AutoTapBase _autoTapBase;
		Texture2D _texture;
		Sprite _marker;

		void Start()
		{
			_texture = new Texture2D(10, 10);
			_texture.SetPixels(_texture.GetPixels().Select(_ => Color.red).ToArray());
			_texture.Apply();
			_marker = Sprite.Create(_texture, new Rect(0, 0, 10, 10), Vector2.zero);
		}

		void OnDestroy()
		{
			Destroy(_texture);
			Destroy(_marker);
			StopCurrentAutoTap();
		}

		public void StopCurrentAutoTap()
		{
			if (AutoTapBase.Current is { } autoTap)
			{
				autoTap.Stop();
				autoTap.Dispose();
			}

			_autoTapBase = null;
		}

		public void StartRandomTap()
		{
			StopCurrentAutoTap();
			_autoTapBase = new RandomTap();
			_autoTapBase.Setup(canvas, _marker);
			_autoTapBase.Start();
		}

		public void StartScenarioBasedAutoTap()
		{
			StopCurrentAutoTap();

			var autoTap = new ScenarioBasedAutoTap();
			_autoTapBase = autoTap;

			autoTap.Setup(canvas, _marker);
			autoTap.Scenario = new Group(autoTap, new Repeat {Count = 5}, new[]
			{
				new Relogin.Config().Generate(autoTap),
				new Tap.Config
				{
					RepeatWhile = new Repeat {Count = 1},
					Screen = "HomeScreen",
					Target = "AddItem"
				}.Generate(autoTap),
				new CreateItem.Config {RepeatWhile = new Repeat {Count = 20}}.Generate(autoTap),
				new DeleteItem.Config {RepeatWhile = new Repeat {Count = 3}}.Generate(autoTap),
			});
			autoTap.Start();
		}

		public void StartJsonBasedAutoTap(string json)
		{
			StopCurrentAutoTap();

			var autoTap = new ScenarioBasedAutoTap();
			_autoTapBase = autoTap;

			autoTap.Setup(canvas, _marker);
			autoTap.SetJson(json);
			autoTap.Start();
		}

		void Update()
		{
			if (_autoTapBase != null)
			{
				if (!_autoTapBase.Enabled)
				{
					_autoTapBase.Dispose();
					_autoTapBase = null;
					return;
				}

				_autoTapBase.Update(Time.deltaTime);
				foreach (var log in _autoTapBase.GetLatestLog())
				{
					Debug.Log(log);
				}
			}
		}

#if UNITY_EDITOR
		[CustomEditor(typeof(DebugManager))]
		class DebugManagerEditor : Editor
		{
			public override void OnInspectorGUI()
			{
				base.OnInspectorGUI();

				var self = (DebugManager)target;

				if (GUILayout.Button("Stop"))
				{
					self.StopCurrentAutoTap();
				}

				if (GUILayout.Button("Start Random Tap"))
				{
					self.StartRandomTap();
				}

				if (GUILayout.Button("Start Scenario Based Auto Tap"))
				{
					self.StartScenarioBasedAutoTap();
				}

				if (GUILayout.Button("Start JSON Based Auto Tap"))
				{
					self.StartJsonBasedAutoTap(@"
{
	'Type': 'Group',
	'Scenarios': [{
		'Type': 'ReturnToTitle'
	}, {
		'Type': 'Login'
	}, {
		'Type': 'CreateItem',
		'RepeatWhile': {
			'Type': 'Repeat',
			'Count': 20,
		}
	}, {
		'Type': 'DeleteItem',
		'RepeatWhile': {
			'Type': 'Repeat',
			'Count': 20,
		}
	}]
}");
				}
			}
		}
#endif
	}
}