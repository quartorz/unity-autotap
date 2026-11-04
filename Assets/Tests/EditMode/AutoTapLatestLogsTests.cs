#if ENABLE_AUTOTAP
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// What <see cref="AutoTapBase.GetLatestLogs"/> returns: the log of the latest Dynamic input update, built again
	/// by each one, and a sequence that allocates nothing and is valid only until the next one.
	/// </summary>
	public class AutoTapLatestLogsTests : InputTestFixture
	{
		const float k_Dt = 0.04f;

		TestAutoTap _autoTap;
		TestScene _scene;

		public override void Setup()
		{
			base.Setup();
			_scene = new TestScene();
			_autoTap = new TestAutoTap();
			_autoTap.SetupMarkers(_scene.Canvas, null);
			_autoTap.Start();
		}

		public override void TearDown()
		{
			_autoTap.Dispose();
			_scene.Dispose();
			base.TearDown();
		}

		void Frame(float dt = k_Dt)
		{
			InputSystem.Update();
			_autoTap.Update(dt);
		}

		/// <summary>
		/// Runs an input update of a type other than the Dynamic one, which <c>InputSystem.Update()</c> runs and which the
		/// Input System does not expose in another way.
		/// </summary>
		static void UpdateOfType(InputUpdateType updateType)
		{
			typeof(InputSystem)
				.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(InputUpdateType) }, null)
				.Invoke(null, new object[] { updateType });
		}

		static List<AutoTapBase.LogEventKind> Kinds(AutoTapBase.LogEventSequence logs)
		{
			var kinds = new List<AutoTapBase.LogEventKind>();
			foreach (var log in logs)
			{
				kinds.Add(log.Kind);
			}

			return kinds;
		}

		[Test]
		public void TheSequenceIsBuiltAgainByEachInputUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0.1f);
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "nothing was processed yet");

			InputSystem.Update();
			var started = _autoTap.GetLatestLogs();
			Assert.That(Kinds(started), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }));
			Assert.That(started[0].Item, Is.SameAs(item));

			InputSystem.Update();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "the second update processed nothing");
			Assert.That(_autoTap.GetLatestLogs(), Is.Empty);
		}

		[Test]
		public void TheSequenceStaysTheSameUntilTheNextInputUpdate()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.Update();

			var before = _autoTap.GetLatestLogs().ToList();
			_autoTap.Update(k_Dt);
			_autoTap.Update(k_Dt);
			var after = _autoTap.GetLatestLogs().ToList();

			Assert.That(before, Has.Count.EqualTo(1));
			Assert.That(after, Is.EqualTo(before));
		}

		[Test]
		public void TheSequenceListsDragStartedAndCompletedToo()
		{
			_scene.Add<DragHandler>("Source", 0.1f, 0.1f, 0.3f, 0.3f);
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).MoveTo(TestScene.At(0.6f, 0.2f), 0.1f));
			var kinds = new List<AutoTapBase.LogEventKind>();
			for (var i = 0; i < 20 && !item.Completed; ++i)
			{
				InputSystem.Update();
				kinds.AddRange(Kinds(_autoTap.GetLatestLogs()));
				_autoTap.Update(k_Dt);
			}

			InputSystem.Update();
			kinds.AddRange(Kinds(_autoTap.GetLatestLogs()));

			Assert.That(kinds, Is.EqualTo(new[]
			{
				AutoTapBase.LogEventKind.Started,
				AutoTapBase.LogEventKind.DragStarted,
				AutoTapBase.LogEventKind.Completed,
			}));
		}

		[Test]
		public void TheSequenceIsCountedAndReadByIndex()
		{
			var first = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			var second = _autoTap.Tap(1, TestScene.At(0.4f, 0.2f), 1f);
			InputSystem.Update();

			var logs = _autoTap.GetLatestLogs();

			Assert.That(logs.Count, Is.EqualTo(2));
			Assert.That(logs[0].Item, Is.SameAs(first));
			Assert.That(logs[1].Item, Is.SameAs(second));
			Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = logs[-1]; });
			Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = logs[2]; });

			IReadOnlyList<AutoTapBase.LogEvent> list = logs;
			Assert.That(list.Count, Is.EqualTo(2));
			Assert.That(list[1].Item, Is.SameAs(second));
			Assert.That(((IEnumerable)logs).Cast<AutoTapBase.LogEvent>().Count(), Is.EqualTo(2));
			Assert.That(logs.Select(l => l.Item), Is.EqualTo(new[] { first, second }), "LINQ works on it");

			var enumerator = logs.GetEnumerator();
			Assert.That(enumerator.MoveNext(), Is.True);
			Assert.That(enumerator.Current.Item, Is.SameAs(first));
			Assert.That(enumerator.MoveNext(), Is.True);
			Assert.That(enumerator.MoveNext(), Is.False);
			enumerator.Reset();
			Assert.That(enumerator.MoveNext(), Is.True);
			Assert.That(enumerator.Current.Item, Is.SameAs(first));
		}

		[Test]
		public void ADefaultSequenceIsEmpty()
		{
			var logs = default(AutoTapBase.LogEventSequence);

			Assert.That(logs.Count, Is.Zero);
			Assert.That(logs, Is.Empty);
			Assert.Throws<ArgumentOutOfRangeException>(() => { var _ = logs[0]; });
		}

		[Test]
		public void ASequenceOfAnEarlierInputUpdateThrowsWhenItIsUsed()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.Update();
			var old = _autoTap.GetLatestLogs();
			var enumerator = old.GetEnumerator();
			Assert.That(enumerator.MoveNext(), Is.True);
			Assert.That(old.Count, Is.EqualTo(1));

			InputSystem.Update();

			Assert.Throws<InvalidOperationException>(() => { foreach (var _ in old) { } });
			Assert.Throws<InvalidOperationException>(() => old.GetEnumerator());
			Assert.Throws<InvalidOperationException>(() => { var _ = old.Count; });
			Assert.Throws<InvalidOperationException>(() => { var _ = old[0]; });
			Assert.Throws<InvalidOperationException>(() => old.ToList());
			Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext(), "an enumerator that was running too");
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "the new one is fine");
		}

		[Test]
		public void ASequenceThrowsEvenWhenTheNewOneIsEmptyAndTheOldOneWasToo()
		{
			// The version tells that the log was built again, not that it holds something else.
			InputSystem.Update();
			var empty = _autoTap.GetLatestLogs();
			Assert.That(empty.Count, Is.Zero);

			InputSystem.Update();

			Assert.Throws<InvalidOperationException>(() => { var _ = empty.Count; });
		}

		[Test]
		public void ACopyOutlivesTheInputUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.Update();

			var copy = _autoTap.GetLatestLogs().ToArray();
			InputSystem.Update();

			Assert.That(copy, Has.Length.EqualTo(1));
			Assert.That(copy[0].Item, Is.SameAs(item));
			Assert.That(copy[0].Kind, Is.EqualTo(AutoTapBase.LogEventKind.Started));
		}

		[Test]
		public void AnEditorInputUpdateDoesNotBuildTheSequenceAgain()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.Update();
			var logs = _autoTap.GetLatestLogs();

			UpdateOfType(InputUpdateType.Editor);

			Assert.That(logs.Count, Is.EqualTo(1));
		}

		[Test]
		public void TheSequenceIsRenewedWhileTheAutoTapIsNotStarted()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.Update();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.EqualTo(1));
			_autoTap.Stop();

			// The release that Stop queued is processed and ends the item; nothing is left of the earlier update.
			InputSystem.Update();
			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
			for (var i = 0; i < 10; ++i)
			{
				InputSystem.Update();
				Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, $"update {i} after the end");
			}
		}

		[Test]
		public void NoOldSequenceRemainsAfterTheVirtualInputWasGivenUp()
		{
			// The virtual devices are removed a few input updates after Stop released what was pressed. From then on
			// the virtual input raises nothing any more, and the sequence is still renewed.
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.Update();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.EqualTo(1));
			_autoTap.Stop();

			for (var i = 0; i < 20 && InputSystem.devices.Any(VirtualInput.IsVirtual); ++i)
			{
				InputSystem.Update();
			}

			Assert.That(InputSystem.devices.Any(VirtualInput.IsVirtual), Is.False, "the virtual devices are gone");
			InputSystem.Update();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero);
			InputSystem.Update();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero);
		}

		[Test]
		public void TheSequenceOfAnInstanceThatHoldsNoSessionIsBuiltAgainToo()
		{
			// An instance that was created but never started has no virtual input to hear from.
			using (var idle = new TestAutoTap())
			{
				Assert.That(idle.GetLatestLogs().Count, Is.Zero);
				var logs = idle.GetLatestLogs();

				InputSystem.Update();

				Assert.Throws<InvalidOperationException>(() => { var _ = logs.Count; });
				Assert.That(idle.GetLatestLogs().Count, Is.Zero);
			}
		}

		[Test]
		public void AResetBetweenTwoInputUpdatesIsInTheSequenceOfTheNextOne()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.Update();
			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }));

			InputSystem.ResetDevice(Touchscreen.current);

			Assert.That(item.Completed, Is.True);
			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }),
				"the sequence is still that of the update before");

			InputSystem.Update();

			var logs = _autoTap.GetLatestLogs();
			Assert.That(Kinds(logs), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
			Assert.That(logs[0].Item, Is.SameAs(item));
			Assert.That(logs[0].Order, Is.EqualTo(item.CompletedOrder));
			InputSystem.Update();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "and in no other");
		}

		[Test]
		public void DisposeEmptiesTheSequenceAndOutdatesTheOnesThatWereReturned()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.Update();
			var logs = _autoTap.GetLatestLogs();
			Assert.That(logs.Count, Is.EqualTo(1));

			_autoTap.Dispose();

			Assert.Throws<InvalidOperationException>(() => { var _ = logs.Count; });
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero);
			InputSystem.Update();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero);
		}

		[Test]
		public void ADisposedInstanceNoLongerHearsTheInputUpdates()
		{
			var other = new TestAutoTap();
			other.Dispose();
			var logs = other.GetLatestLogs();

			InputSystem.Update();

			Assert.That(logs.Count, Is.Zero, "the version of a disposed instance does not change any more");
		}

		[Test]
		public void EnumeratingTheSequenceAllocatesNothing()
		{
			for (var i = 0; i < 3; ++i)
			{
				_autoTap.Tap(i, TestScene.At(0.1f + 0.2f * i, 0.2f), 1f);
			}

			InputSystem.Update();
			var logs = _autoTap.GetLatestLogs();
			Assert.That(logs.Count, Is.EqualTo(3));
			const int rounds = 4000;

			// Allocation of the Mono heap is what is measured. The enumeration through IEnumerable boxes the enumerator,
			// which makes sure that the measurement sees an allocation at all.
			var boxedBytes = MeasureAllocation(() =>
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
			Assert.That(boxedBytes, Is.GreaterThan(rounds * 16), "the measurement sees allocations");

			var foreachBytes = MeasureAllocation(() =>
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
			var indexBytes = MeasureAllocation(() =>
			{
				var count = 0;
				for (var i = 0; i < rounds; ++i)
				{
					var sequence = _autoTap.GetLatestLogs();
					for (var j = 0; j < sequence.Count; ++j)
					{
						count += sequence[j].Item.Index;
					}
				}

				return count;
			});

			// The Mono heap is measured in pages of 4 KB, and the smallest object that would be allocated in each of the
			// rounds is 24 bytes, which is 96 KB over all of them.
			Assert.That(foreachBytes, Is.LessThan(16 * 1024), "foreach");
			Assert.That(indexBytes, Is.LessThan(16 * 1024), "Count and the indexer, and GetLatestLogs itself");
		}

		/// <summary>
		/// The number of bytes that the Mono heap grew by while <paramref name="work"/> ran: the median of several runs,
		/// so that a collection that shrinks the heap in one of them, or an allocation of another thread, does not
		/// decide the result.
		/// </summary>
		static long MeasureAllocation(Func<int> work)
		{
			work(); // warm up
			var results = new List<long>();
			for (var i = 0; i < 5; ++i)
			{
				var before = Profiler.GetMonoUsedSizeLong();
				var result = work();
				var after = Profiler.GetMonoUsedSizeLong();
				Assert.That(result, Is.Not.Negative);
				results.Add(after - before);
			}

			results.Sort();
			return results[results.Count / 2];
		}
	}
}
#endif
