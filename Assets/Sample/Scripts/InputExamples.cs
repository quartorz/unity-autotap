#if ENABLE_AUTOTAP
namespace Sample
{
	/// <summary>
	/// The scenarios of <c>CloseMenuWithEscape.cs</c>, <c>HoverAndLeave.cs</c> and <c>MoveAndSubmit.cs</c> as the JSON
	/// that <see cref="ScenarioBasedAutoTap.SetJson"/> reads.
	/// </summary>
	public static class InputExamples
	{
		/// <summary>
		/// Logs in if the title screen is shown, then runs each example three times. The names of the scenarios are the
		/// names of their classes, which is how <see cref="ScenarioBasedAutoTap.SetJson"/> finds them.
		/// </summary>
		public const string Json = @"
{
	'Type': 'Group',
	'Scenarios': [{
		'Type': 'Login'
	}, {
		'Type': 'CloseMenuWithEscape',
		'RepeatCondition': {
			'Type': 'Repeat',
			'Count': 3,
		}
	}, {
		'Type': 'HoverAndLeave',
		'RepeatCondition': {
			'Type': 'Repeat',
			'Count': 3,
		}
	}, {
		'Type': 'MoveAndSubmit',
		'RepeatCondition': {
			'Type': 'Repeat',
			'Count': 3,
		}
	}]
}";
	}
}
#endif
