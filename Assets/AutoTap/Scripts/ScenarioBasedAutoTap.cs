using UnityEngine;

#if !DISABLE_AUTOTAP
namespace AutoTap
{
	public abstract class ScenarioBasedAutoTap : AutoTap
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

			if (!Scenario.Active)
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
			if (Scenario.Active)
			{
				Scenario.RepeatWhile.Update(deltaTime);
				Scenario.UpdateActive();
				if (Scenario.Active)
				{
					Scenario.OnPreUpdate(deltaTime);
				}
			}

			if (!Scenario.Active && Enabled)
			{
				Stop();
			}
		}

		protected override void Update(int index)
		{
			if (Scenario.Active)
			{
				Scenario.OnUpdate(index);
			}

			if (!Scenario.Active && Enabled)
			{
				Stop();
			}
		}

		protected override void OnPostUpdate(float deltaTime)
		{
			if (Scenario.Active)
			{
				Scenario.OnPostUpdate(deltaTime);
			}

			if (!Scenario.Active && Enabled)
			{
				Stop();
			}
		}
	}
}
#endif