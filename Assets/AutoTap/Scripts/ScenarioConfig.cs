#if !DISABLE_AUTOTAP
using System;
using System.Reflection;

namespace AutoTap
{
	[Serializable]
	public abstract class ScenarioConfig
	{
		public Condition RepeatWhile = new Once();
		public abstract Scenario Generate(AutoTap owner);
	}

	public class ScenarioConfig<TScenario> : ScenarioConfig where TScenario : Scenario<ScenarioConfig<TScenario>>
	{
		public override Scenario Generate(AutoTap owner)
		{
			return (TScenario)Activator.CreateInstance(
				typeof(TScenario), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
				new object[]{owner, this}, null);
		}
	}
}
#endif