#if !DISABLE_AUTOTAP
using UnityEngine;

namespace UnityAutoTap
{
	public abstract class ScenarioBasedAutoTap : AutoTapBase
	{
		public Scenario Scenario;

		public string Status => Enabled && Scenario?.Active == true ? Scenario.Status : null;

		public override void Dispose()
		{
			base.Dispose();
			Scenario?.Dispose();
			Scenario = null;
		}

		protected override void OnStart()
		{
			Scenario.RepeatWhile.Prepare();
			Scenario.UpdateActive();
			Scenario.Prepare();

			if (!Scenario.IsActiveAndRepeatable)
			{
				Stop();
			}
		}

		protected override bool ToBeIgnored(Transform transform)
		{
			return Scenario.ToBeIgnored(transform) || base.ToBeIgnored(transform);
		}

		protected override void OnPreUpdate(float deltaTime)
		{
			if (Scenario.IsActiveAndRepeatable)
			{
				Scenario.RepeatWhile.Update(deltaTime);
				Scenario.UpdateActive();
				if (Scenario.IsActiveAndRepeatable)
				{
					Scenario.OnPreUpdate(deltaTime);
				}
			}

			if (!Scenario.IsActiveAndRepeatable && Enabled)
			{
				Stop();
			}
		}

		protected override void Update(int index)
		{
			if (Scenario.IsActiveAndRepeatable)
			{
				Scenario.OnUpdate(index);
			}

			if (!Scenario.IsActiveAndRepeatable && Enabled)
			{
				Stop();
			}
		}

		protected override void OnPostUpdate(float deltaTime)
		{
			if (Scenario.IsActiveAndRepeatable)
			{
				Scenario.OnPostUpdate(deltaTime);
			}

			if (!Scenario.IsActiveAndRepeatable && Enabled)
			{
				Stop();
			}
		}
	}
}
#endif