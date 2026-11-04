#if ENABLE_AUTOTAP
using UnityEngine.EventSystems;

namespace UnityAutoTap
{
	public abstract class ScenarioBasedAutoTap : AutoTapBase
	{
		/// <summary>
		/// Creates an auto tap that runs a scenario, and acquires the virtual input itself at <see cref="AutoTapBase.Start"/>.
		/// </summary>
		/// <param name="slotCount">
		/// The number of slots, from 1 to 10, the number of touches that a touchscreen has.
		/// </param>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="slotCount"/> is not between 1 and 10.</exception>
		protected ScenarioBasedAutoTap(int slotCount) : base(slotCount)
		{
		}

		/// <summary>
		/// Creates an auto tap that runs a scenario on the devices of <paramref name="input"/>, a session that the
		/// caller owns (see <see cref="AutoTapBase(int, IVirtualInput)"/>).
		/// </summary>
		/// <param name="slotCount">
		/// The number of slots, from 1 to 10, the number of touches that a touchscreen has.
		/// </param>
		/// <param name="input">
		/// The session that supplies the virtual devices. <c>null</c> makes the auto tap acquire its own session.
		/// </param>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="slotCount"/> is not between 1 and 10.</exception>
		protected ScenarioBasedAutoTap(int slotCount, IVirtualInput input) : base(slotCount, input)
		{
		}

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