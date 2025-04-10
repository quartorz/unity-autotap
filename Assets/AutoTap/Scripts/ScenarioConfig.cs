#if ENABLE_AUTOTAP
using System;
using System.Reflection;

namespace UnityAutoTap
{
	[Serializable]
	public abstract class ScenarioConfig
	{
		public Condition RepeatCondition = new Once();
		public abstract Scenario Generate(AutoTapBase owner);
	}

	public class ScenarioConfig<TScenario> : ScenarioConfig where TScenario : Scenario<ScenarioConfig<TScenario>>
	{
		public override Scenario Generate(AutoTapBase owner)
		{
			return (TScenario)Activator.CreateInstance(
				typeof(TScenario), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
				new object[]{owner, this}, null);
		}
	}
}
#endif