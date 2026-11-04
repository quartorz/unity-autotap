#if ENABLE_AUTOTAP
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using LogEventKind = UnityAutoTap.AutoTapBase.LogEventKind;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// <c>GetLatestLogs</c> over real frames: the sequence of the latest Dynamic input update, built again by each one.
	/// </summary>
	/// <remarks>
	/// The frame arithmetic is that of <see cref="TouchTests"/>: <c>Simulate</c> in frame N queues the press, frame N + 1
	/// processes it and is the first one in which <see cref="AutoTapBase.Update(float)"/> advances the plan, and the
	/// release of a plan that is over in frame M is processed in frame M + 1. <see cref="AutoTapFixture.Dt"/> is 0.1 s.
	/// </remarks>
	public class LatestLogsTests : AutoTapFixture
	{
		[UnityTest]
		public IEnumerator GetLatestLogs_IsBuiltAgainByEveryFrame_AndAnOldSequenceThrows()
		{
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 0.5f));
			Assert.That(Auto.GetLatestLogs().Count, Is.Zero, "nothing was processed yet");

			yield return Step();
			var started = Auto.GetLatestLogs();
			Assert.That(started.Select(l => l.Kind), Is.EqualTo(new[] { LogEventKind.Started }));
			Assert.That(started[0].Item, Is.SameAs(item));

			yield return Step();

			Assert.That(Auto.GetLatestLogs().Count, Is.Zero, "a frame in which nothing happened");
			Assert.Throws<InvalidOperationException>(() => { var _ = started.Count; });
			Assert.Throws<InvalidOperationException>(() => { foreach (var _ in started) { } });
			Assert.Throws<InvalidOperationException>(() => { var _ = started[0]; });
		}

		[UnityTest]
		public IEnumerator GetLatestLogs_AfterStopAndTheEndOfTheVirtualInput_IsEmptyInEveryFrame()
		{
			// The auto tap acquired the virtual input itself, so Stop gives it up: the release is processed, and a few
			// input updates later the devices are removed. The sequence of the last update that had something in it must
			// not be left over in the frames that follow.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto(useSession: false);

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 5f));
			yield return Until(() => item.Order.HasValue, "the press to be processed");
			Assert.That(Auto.GetLatestLogs().Count, Is.EqualTo(1));

			Auto.Stop();
			yield return Step();

			var ends = LatestLogs();
			Assert.That(ends.Select(l => (l.Item, l.Kind)), Is.EqualTo(new[] { (item, LogEventKind.Completed) }), Describe(ends));
			yield return Until(() => !AnyVirtualDevice(), "the virtual devices to be removed");
			for (var i = 0; i < 5; ++i)
			{
				yield return Step();
				Assert.That(Auto.GetLatestLogs().Count, Is.Zero, $"frame {i} after the devices were removed");
			}
		}

		[UnityTest]
		public IEnumerator GetLatestLogs_EnumeratingItAllocatesNothing()
		{
			yield return BuildRig();
			var a = Rig.AddButton("A", Rig.Cell(0, 1));
			var b = Rig.AddButton("B", Rig.Cell(1, 1));
			var c = Rig.AddButton("C", Rig.Cell(2, 1));
			yield return Settle();
			StartAuto();
			yield return Step(() =>
			{
				Auto.SimulateTap(0, Rig.Center(a.gameObject), 5f);
				Auto.SimulateTap(1, Rig.Center(b.gameObject), 5f);
				Auto.SimulateTap(2, Rig.Center(c.gameObject), 5f);
			});
			yield return Step();
			var logs = Auto.GetLatestLogs();
			Assert.That(logs.Count, Is.EqualTo(3));
			const int rounds = 4000;

			long Measure(Func<int> work)
			{
				work();
				var results = new List<long>();
				for (var i = 0; i < 5; ++i)
				{
					var before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
					work();
					results.Add(UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before);
				}

				results.Sort();
				return results[results.Count / 2];
			}

			var boxed = Measure(() =>
			{
				var count = 0;
				for (var i = 0; i < rounds; ++i)
				{
					foreach (var log in (IEnumerable<AutoTapBase.LogEvent>)logs)
					{
						count += log.Item.Index;
					}
				}

				return count;
			});
			var plain = Measure(() =>
			{
				var count = 0;
				for (var i = 0; i < rounds; ++i)
				{
					foreach (var log in logs)
					{
						count += log.Item.Index;
					}
				}

				return count;
			});

			Assert.That(boxed, Is.GreaterThan(rounds * 16), "the measurement sees allocations");
			Assert.That(plain, Is.LessThan(16 * 1024));
		}
	}
}
#endif
