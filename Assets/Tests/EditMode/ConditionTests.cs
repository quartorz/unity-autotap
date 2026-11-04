#if ENABLE_AUTOTAP
using NUnit.Framework;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// What <see cref="Condition.Status"/> of <see cref="Once"/> and <see cref="Repeat"/> reports.
	/// </summary>
	public class ConditionTests
	{
		[Test]
		public void OnceReportsWhetherItWasConsumed()
		{
			var once = new Once();
			once.Prepare();

			Assert.That(once.Value, Is.True);
			Assert.That(once.Status, Is.EqualTo("0 / 1"));

			once.OnLoop();

			Assert.That(once.Value, Is.False);
			Assert.That(once.Status, Is.EqualTo("1 / 1"));
		}

		[Test]
		public void RepeatReportsTheCurrentCountOutOfTheCount()
		{
			var repeat = new Repeat {Count = 3};
			repeat.Prepare();

			Assert.That(repeat.Status, Is.EqualTo("0 / 3"));

			repeat.OnLoop();
			repeat.OnLoop();

			Assert.That(repeat.Value, Is.True);
			Assert.That(repeat.Status, Is.EqualTo("2 / 3"));

			repeat.OnLoop();

			Assert.That(repeat.Value, Is.False);
			Assert.That(repeat.Status, Is.EqualTo("3 / 3"));

			repeat.Prepare();

			Assert.That(repeat.Value, Is.True);
			Assert.That(repeat.Status, Is.EqualTo("0 / 3"), "Prepare starts counting again");
		}
	}
}
#endif
