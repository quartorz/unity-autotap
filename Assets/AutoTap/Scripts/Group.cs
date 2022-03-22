using System.Linq;
using UnityEngine;

#if !DISABLE_AUTOTAP
namespace AutoTap
{
	public class Group : Scenario<Group.Config>
	{
		public new class Config : ScenarioConfig
		{
			public ScenarioConfig[] Scenarios;

			public override Scenario Generate(AutoTap owner)
			{
				return new Group(owner, this);
			}
		}

		public override bool Active => base.Active && _active;
		public override string Status => Current?.Active == true ? Current.Status : null;

		readonly Scenario[] _scenarios;
		int _currentIndex;

		bool _active;

		public Scenario Current => _scenarios.Length != 0 && _currentIndex < _scenarios.Length
			? _scenarios[_currentIndex]
			: null;

		public Group(AutoTap owner, Config config) : base(owner, config)
		{
			_scenarios = config.Scenarios.Select(s => s.Generate(owner)).ToArray();
		}

		public Group(AutoTap owner, Condition repeatWhile, Scenario[] scenarios)
			: base(owner, new Config {RepeatWhile = repeatWhile})
		{
			_scenarios = scenarios;
		}

		public override bool ToBeIgnored(Transform transform)
		{
			return Current?.ToBeIgnored(transform) == true;
		}

		void MoveToNext()
		{
			var start = _currentIndex;
			while (RepeatWhile.Value && !_scenarios[_currentIndex].Active)
			{
				if (++_currentIndex >= _scenarios.Length)
				{
					RepeatWhile.OnLoop();
					_currentIndex = 0;
				}

				_scenarios[_currentIndex].RepeatWhile.Prepare();
				_scenarios[_currentIndex].UpdateActive();
				_scenarios[_currentIndex].Prepare();
				if (_currentIndex == start)
				{
					_active = false;
					break;
				}
			}
		}

		public override void UpdateActive()
		{
			if (!(Current is { } scenario))
			{
				_active = false;
				return;
			}

			_active = true;

			scenario.UpdateActive();
		}

		public override void Prepare()
		{
			if (!(Current is { } scenario))
			{
				_active = false;
				return;
			}

			scenario.RepeatWhile.Prepare();
			scenario.Prepare();
			if (!scenario.Active)
			{
				MoveToNext();
			}
		}

		public override void OnPreUpdate(float deltaTime)
		{
			RepeatWhile.Update(deltaTime);

			if (!RepeatWhile.Value)
			{
				return;
			}

			Current.RepeatWhile.Update(deltaTime);
			Current.UpdateActive();

			if (Current.Active)
			{
				Current.OnPreUpdate(deltaTime);
			}

			if (!Current.Active)
			{
				MoveToNext();
				if (RepeatWhile.Value && Current.Active)
				{
					Current.OnPreUpdate(deltaTime);
				}
			}
		}

		public override void OnUpdate(int index)
		{
			if (Current.Active)
			{
				Current.OnUpdate(index);
			}
		}

		public override void OnPostUpdate(float deltaTime)
		{
			if (Current.Active)
			{
				Current.OnPostUpdate(deltaTime);
			}
		}
	}
}
#endif