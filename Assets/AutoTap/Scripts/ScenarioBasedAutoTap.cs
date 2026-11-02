#if ENABLE_AUTOTAP
using UnityEngine.EventSystems;

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
			Scenario.RepeatCondition.Prepare();
			Scenario.UpdateActive();
			Scenario.Prepare();

			if (!Scenario.IsActiveAndRepeatable)
			{
				Stop();
			}
		}

		protected override bool ShouldNotPressOn(RaycastResult topmost)
		{
			return Scenario.ShouldNotPressOn(topmost) || base.ShouldNotPressOn(topmost);
		}

		protected override void OnPreUpdate(float deltaTime)
		{
			if (Scenario.IsActiveAndRepeatable)
			{
				Scenario.RepeatCondition.Update(deltaTime);
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

		protected override void OnTapCompleted(int index, LogItem logItem)
		{
			Scenario?.OnTapCompleted(index, logItem);
		}
	}
}
#endif