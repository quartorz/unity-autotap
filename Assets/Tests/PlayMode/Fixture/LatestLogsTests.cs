#if ENABLE_AUTOTAP
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using LogEventKind = UnityAutoTap.AutoTapBase.LogEventKind;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// <c>GetLatestLogs</c> over real frames: the sequence of the latest <c>Update</c>, built again by each one that
	/// runs. What the input update of a frame processed, and what happened between two updates, is in the sequence of the
	/// next <c>Update</c>; what happens inside <c>Update</c> is in the sequence of that call.
	/// </summary>
	/// <remarks>
	/// The frame arithmetic is that of <see cref="TouchTests"/>: <c>Simulate</c> in frame N queues the press, frame N + 1
	/// processes it and is the first one in which <see cref="AutoTapBase.Update(float)"/> advances the plan, and the
	/// release of a plan that is over in frame M is processed in frame M + 1. <see cref="AutoTapFixture.Dt"/> is 0.1 s.
	/// </remarks>
	public class LatestLogsTests : AutoTapFixture
	{
		[UnityTest]
		public IEnumerator GetLatestLogs_IsBuiltAgainByEveryUpdate_AndAnOldSequenceThrows()
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
		public IEnumerator GetLatestLogs_AfterStopOutsideUpdate_StaysTheSequenceOfTheLastUpdate_AndTheEndIsInNone()
		{
			// The auto tap acquired the virtual input itself, so Stop gives it up: the release is processed, and a few
			// input updates later the devices are removed. Stop was called outside Update, and no Update builds a sequence
			// afterwards, so the end of what Stop ended is in none, and the sequence of the last Update stays as it was.
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto(useSession: false);

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 5f));
			yield return Until(() => item.Order.HasValue, "the press to be processed");
			var before = LatestLogs();
			Assert.That(before.Select(l => (l.Item, l.Kind)), Is.EqualTo(new[] { (item, LogEventKind.Started) }), Describe(before));

			Auto.Stop();
			yield return Step();

			Assert.That(item.Completed, Is.True);
			Assert.That(LatestLogs(), Is.EqualTo(before), "the end that Stop caused is not in it");
			yield return Until(() => !AnyVirtualDevice(), "the virtual devices to be removed");
			for (var i = 0; i < 5; ++i)
			{
				yield return Step();
				Assert.That(LatestLogs(), Is.EqualTo(before), $"frame {i} after the devices were removed");
			}
		}

		[UnityTest]
		public IEnumerator GetLatestLogs_StopInsideUpdate_ListsTheEndInThatUpdate_AndInNoOther()
		{
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto(useSession: false);
			CollectLogs = true;

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 5f));
			yield return Until(() => item.Order.HasValue, "the press to be processed");

			yield return Step(() => Auto.Stop());

			var ends = LatestLogs();
			Assert.That(ends.Select(l => (l.Item, l.Kind)), Is.EqualTo(new[] { (item, LogEventKind.Completed) }), Describe(ends));
			Assert.That(ends[0].Order, Is.Null);
			yield return Until(() => !AnyVirtualDevice(), "the virtual devices to be removed");
			yield return Steps(3);

			Assert.That(LatestLogs(), Is.EqualTo(ends), "no Update builds the sequence again");
			Assert.That(LogTrace.Count(t => t.Event.Item == item && t.Event.Kind == LogEventKind.Completed), Is.EqualTo(1));
			Assert.That(Rig.Log.CountOf("pointerUp", button.gameObject), Is.EqualTo(1), "and the release was delivered");
		}

		[UnityTest]
		public IEnumerator GetLatestLogs_AResetBetweenTwoUpdates_IsInTheSequenceOfTheNextUpdate()
		{
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 5f));
			yield return Until(() => item.Order.HasValue, "the press to be processed");
			var before = LatestLogs();
			Assert.That(before.Select(l => l.Kind), Is.EqualTo(new[] { LogEventKind.Started }), Describe(before));

			// After the Update of this frame, before the one of the next.
			InputSystem.ResetDevice(Touchscreen.current);

			Assert.That(item.Completed, Is.True);
			Assert.That(LatestLogs(), Is.EqualTo(before), "the sequence is still the one of the last Update");

			yield return Step();

			var ends = LatestLogs();
			Assert.That(ends.Select(l => (l.Item, l.Kind)), Is.EqualTo(new[] { (item, LogEventKind.Completed) }), Describe(ends));
			Assert.That(ends[0].Order, Is.EqualTo(item.CompletedOrder));
			yield return Step();
			Assert.That(LatestLogs(), Is.Empty, "and in no other");
		}

		[UnityTest]
		public IEnumerator GetLatestLogs_AResetInsideUpdate_IsInThatUpdate_AndInNoOther()
		{
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, Rig.Center(button.gameObject), 5f));
			yield return Until(() => item.Order.HasValue, "the press to be processed");

			yield return Step(() => InputSystem.ResetDevice(Touchscreen.current));

			Assert.That(item.Completed, Is.True);
			var ends = LatestLogs();
			Assert.That(ends.Select(l => (l.Item, l.Kind)), Is.EqualTo(new[] { (item, LogEventKind.Completed) }), Describe(ends));
			Assert.That(ends[0].Order, Is.EqualTo(item.CompletedOrder));
			yield return Step();
			Assert.That(LatestLogs(), Is.Empty, "not in the sequence of the next Update");
		}

		[UnityTest]
		public IEnumerator GetLatestLogs_StopOutsideUpdate_IsNotInTheSequencesOfTheNextRun()
		{
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();
			CollectLogs = true;

			LogItem first = null;
			yield return Step(() => first = Auto.SimulateTap(0, Rig.Center(button.gameObject), 5f));
			yield return Until(() => first.Order.HasValue, "the press to be processed");

			Auto.Stop();
			yield return Steps(2);
			Assert.That(Auto.GetLatestLogs().Select(l => (l.Item, l.Kind)), Is.EqualTo(new[] { (first, LogEventKind.Started) }),
				"Stop leaves the sequence as it was");
			Auto.Start();
			Assert.That(Auto.GetLatestLogs(), Is.Empty, "Start empties it, until the first Update of the run builds it");
			LogItem second = null;
			yield return Step(() => second = Auto.SimulateTap(1, Rig.Center(button.gameObject), 0.2f));
			yield return Until(() => second.Completed, "the tap of the second run to end");
			yield return Steps(2);

			Assert.That(LogTrace.Any(t => t.Event.Item == first && t.Event.Kind == LogEventKind.Completed), Is.False,
				"the end of what Stop ended outside Update is in no sequence of the next run");
			Assert.That(LogTrace.Where(t => t.Event.Item == second).Select(t => t.Event.Kind),
				Is.EqualTo(new[] { LogEventKind.Started, LogEventKind.Completed }));
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
