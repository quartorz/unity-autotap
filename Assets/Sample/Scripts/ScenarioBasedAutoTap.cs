using System;
using System.Linq;
using UnityAutoTap;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Base = UnityAutoTap.ScenarioBasedAutoTap;

namespace Sample
{
	public partial class ScenarioBasedAutoTap : Base
	{
		public override void Setup(Canvas canvasForMarker, Sprite markerSprite)
		{
			SetupInternal(canvasForMarker, markerSprite, 5);
		}

		protected override void OnStart()
		{
			UpdateSceneData();
			base.OnStart();
		}

		protected override void OnStop()
		{
			_mainCanvas = null;
		}

		protected override void OnPreUpdate(float deltaTime)
		{
			UpdateSceneData();
			base.OnPreUpdate(deltaTime);
		}

		protected override bool ToBeIgnored(Transform transform)
		{
			return base.ToBeIgnored(transform) || !transform.IsChildOf(_mainCanvas.transform);
		}

		public void SetJson(string json)
		{
			Scenario = Impl(JObject.Parse(json)).Generate(this);

			ScenarioConfig Impl(JObject obj)
			{
				var repeatCondition = obj.Value<JObject>("RepeatCondition");
				obj.Remove("RepeatCondition");

				ScenarioConfig config;
				var type = obj.Value<string>("Type");
				switch (type)
				{
					case null:
					case "":
						throw new Exception($"invalid type: {type}");
					case "Group":
					{
						var scenarios = obj.Value<JArray>("Scenarios").Values<JObject>().Select(Impl).ToArray();
						obj.Remove("Scenarios");
						var groupConfig = obj.ToObject<Group.Config>()!;
						groupConfig.Scenarios = scenarios;
						config = groupConfig;
						break;
					}
					default:
						if (Type.GetType($"Sample.ScenarioBasedAutoTap+{type}+Config") is { } t)
						{
							config = (ScenarioConfig)obj.ToObject(t)!;
						}
						else if (typeof(ScenarioConfig).Assembly.GetType($"AutoTap.{type}+Config") is { } builtin)
						{
							config = (ScenarioConfig)obj.ToObject(builtin)!;
						}
						else
						{
							throw new Exception($"invalid type: {type}");
						}

						break;
				}

				if (repeatCondition != null)
				{
					config.RepeatCondition = ParseCondition(repeatCondition);
				}

				return config;
			}

			Condition ParseCondition(JObject obj)
			{
				var type = obj.Value<string>("Type");
				switch (type)
				{
					case null:
					case "":
						throw new Exception($"invalid type: {type}");
					default:
						if (typeof(Condition).Assembly.GetType($"UnityAutoTap.{type}") is { } t)
						{
							return (Condition)obj.ToObject(t);
						}
						else
						{
							throw new Exception($"invalid type: {type}");
						}
				}
			}
		}
	}
}