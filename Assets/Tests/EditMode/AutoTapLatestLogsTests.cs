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
	/// What <see cref="AutoTapBase.GetLatestLogs"/> returns: the log of the latest <c>Update</c>, built again by each
	/// one that runs, and a sequence that allocates nothing and is valid only until the next one. What happens inside
	/// <c>Update</c> is in the log of that call, and what happens outside it is in the log of the next.
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
		public void TheSequenceIsBuiltAgainByEveryUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0.1f);
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "nothing was processed yet");

			InputSystem.Update();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "the input update does not build the sequence");

			_autoTap.Update(k_Dt);
			var started = _autoTap.GetLatestLogs();
			Assert.That(Kinds(started), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }));
			Assert.That(started[0].Item, Is.SameAs(item));

			Frame();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "nothing happened since the update before");
			Assert.That(_autoTap.GetLatestLogs(), Is.Empty);
		}

		[Test]
		public void AnUpdateThatFollowsAnotherWithoutAnInputUpdateHasAnEmptySequence()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			InputSystem.Update();
			_autoTap.Update(k_Dt);
			Assert.That(_autoTap.GetLatestLogs().Count, Is.EqualTo(1));

			_autoTap.Update(k_Dt);

			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "what happened is in the sequence of the first one only");
		}

		[Test]
		public void TheSequenceStaysTheSameUntilTheNextUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), k_Dt);
			Frame();
			Assert.That(item.Completed, Is.False, "the release is queued");

			var before = _autoTap.GetLatestLogs().ToList();
			InputSystem.Update();
			InputSystem.Update();
			var after = _autoTap.GetLatestLogs().ToList();

			Assert.That(item.Completed, Is.True, "the input update processed the release");
			Assert.That(before.Select(e => e.Kind), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }));
			Assert.That(after, Is.EqualTo(before), "and it is not in the sequence yet");

			_autoTap.Update(k_Dt);

			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
		}

		[Test]
		public void WhatHappensWhileUpdateIsNotCalledIsInTheNextSequence()
		{
			var first = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);
			var second = _autoTap.Tap(1, TestScene.At(0.4f, 0.2f), 0f);
			Frame();
			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[]
			{
				AutoTapBase.LogEventKind.Started,
				AutoTapBase.LogEventKind.Started,
			}));

			// Two frames without a call of Update: the releases are processed, and nothing else happens.
			InputSystem.Update();
			InputSystem.Update();
			var third = _autoTap.Tap(2, TestScene.At(0.6f, 0.2f), 5f);
			InputSystem.Update();
			_autoTap.Update(k_Dt);

			var logs = _autoTap.GetLatestLogs().ToList();
			Assert.That(logs.Select(e => (e.Item, e.Kind)), Is.EqualTo(new[]
			{
				(first, AutoTapBase.LogEventKind.Completed),
				(second, AutoTapBase.LogEventKind.Completed),
				(third, AutoTapBase.LogEventKind.Started),
			}), "everything that happened since the previous Update, once each");
		}

		[Test]
		public void TheSequenceListsDragStartedAndCompletedToo()
		{
			_scene.Add<DragHandler>("Source", 0.1f, 0.1f, 0.3f, 0.3f);
			var item = _autoTap.Do(0, Gesture.PressAt(TestScene.At(0.2f, 0.2f)).MoveTo(TestScene.At(0.6f, 0.2f), 0.1f));
			var kinds = new List<AutoTapBase.LogEventKind>();
			for (var i = 0; i < 20 && !item.Completed; ++i)
			{
				Frame();
				kinds.AddRange(Kinds(_autoTap.GetLatestLogs()));
			}

			Frame();
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
			Frame();

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
		public void ASequenceOfAnEarlierUpdateThrowsWhenItIsUsed()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			var old = _autoTap.GetLatestLogs();
			var enumerator = old.GetEnumerator();
			Assert.That(enumerator.MoveNext(), Is.True);
			Assert.That(old.Count, Is.EqualTo(1));

			InputSystem.Update();
			Assert.That(old.Count, Is.EqualTo(1), "an input update does not make it out of date");

			_autoTap.Update(k_Dt);

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
			Frame();
			var empty = _autoTap.GetLatestLogs();
			Assert.That(empty.Count, Is.Zero);

			Frame();

			Assert.Throws<InvalidOperationException>(() => { var _ = empty.Count; });
		}

		[Test]
		public void ACopyOutlivesTheUpdate()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();

			var copy = _autoTap.GetLatestLogs().ToArray();
			Frame();

			Assert.That(copy, Has.Length.EqualTo(1));
			Assert.That(copy[0].Item, Is.SameAs(item));
			Assert.That(copy[0].Kind, Is.EqualTo(AutoTapBase.LogEventKind.Started));
		}

		[Test]
		public void NoInputUpdateBuildsTheSequenceAgain()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			var logs = _autoTap.GetLatestLogs();

			InputSystem.Update();
			UpdateOfType(InputUpdateType.Editor);
			InputSystem.Update();

			Assert.That(logs.Count, Is.EqualTo(1));
		}

		[Test]
		public void TheSequenceIsNotBuiltAgainWhileTheAutoTapIsNotEnabled()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			var logs = _autoTap.GetLatestLogs();
			Assert.That(logs.Count, Is.EqualTo(1));
			_autoTap.Stop();

			// Update does nothing, so there is no sequence that could take the end of what Stop ended.
			for (var i = 0; i < 5; ++i)
			{
				Frame();
			}

			Assert.That(logs.Count, Is.EqualTo(1), "it was not built again, so the sequence is still valid");
			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }));
		}

		[Test]
		public void TheSequenceStaysWhenTheVirtualInputWasGivenUp()
		{
			// The virtual devices are removed a few input updates after Stop released what was pressed. The sequence of
			// the last Update is the latest one all the same: nothing renews it, and nothing is added to it.
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			var logs = _autoTap.GetLatestLogs();
			Assert.That(logs.Count, Is.EqualTo(1));
			_autoTap.Stop();

			for (var i = 0; i < 20 && InputSystem.devices.Any(VirtualInput.IsVirtual); ++i)
			{
				Frame();
			}

			Assert.That(InputSystem.devices.Any(VirtualInput.IsVirtual), Is.False, "the virtual devices are gone");
			Frame();
			Assert.That(logs.Count, Is.EqualTo(1));
			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }));
		}

		[Test]
		public void AnInstanceThatWasNeverStartedBuildsNoSequence()
		{
			// Update does nothing for an instance that is not enabled, so there is nothing to build.
			using (var idle = new TestAutoTap())
			{
				Assert.That(idle.GetLatestLogs().Count, Is.Zero);
				var logs = idle.GetLatestLogs();

				InputSystem.Update();
				idle.Update(k_Dt);

				Assert.That(logs.Count, Is.Zero, "still valid");
				Assert.That(idle.GetLatestLogs().Count, Is.Zero);
			}
		}

		[Test]
		public void AResetBetweenTwoUpdatesIsInTheSequenceOfTheNextOne()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }));

			InputSystem.ResetDevice(Touchscreen.current);

			Assert.That(item.Completed, Is.True);
			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }),
				"the sequence is still that of the update before");

			InputSystem.Update();
			Assert.That(Kinds(_autoTap.GetLatestLogs()), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Started }),
				"and an input update does not change it");

			_autoTap.Update(k_Dt);

			var logs = _autoTap.GetLatestLogs();
			Assert.That(Kinds(logs), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
			Assert.That(logs[0].Item, Is.SameAs(item));
			Assert.That(logs[0].Order, Is.EqualTo(item.CompletedOrder));
			Frame();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "and in no other");
		}

		[Test]
		public void AResetInsideUpdateIsInThatSequenceAndInNoOther()
		{
			var item = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			_autoTap.PreUpdate = dt =>
			{
				_autoTap.PreUpdate = null;
				InputSystem.ResetDevice(Touchscreen.current);
			};

			Frame();

			Assert.That(item.Completed, Is.True);
			var logs = _autoTap.GetLatestLogs();
			Assert.That(Kinds(logs), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
			Assert.That(logs[0].Item, Is.SameAs(item));
			Assert.That(logs[0].Order, Is.EqualTo(item.CompletedOrder));
			Frame();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "not in the sequence of the next update");
		}

		[Test]
		public void AResetOfAKeyboardInsideUpdateIsInThatSequenceAndInNoOther()
		{
			var item = _autoTap.Key(Key.A, 5f);
			Frame();
			Assert.That(Keyboard.current.aKey.isPressed, Is.True);
			_autoTap.PreUpdate = dt =>
			{
				_autoTap.PreUpdate = null;
				InputSystem.ResetDevice(Keyboard.current);
			};

			Frame();

			Assert.That(item.Completed, Is.True);
			Assert.That(item.EndReason, Is.EqualTo(AutoTapBase.EndReason.Interrupted));
			var logs = _autoTap.GetLatestLogs();
			Assert.That(Kinds(logs), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
			Assert.That(logs[0].Item, Is.SameAs(item));
			Frame();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "not in the sequence of the next update");
		}

		[Test]
		public void DisablingADeviceInsideUpdateEndsTheOperationsInThatSequence()
		{
			var item = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Hold(5f));
			Frame();
			Assert.That(Mouse.current.leftButton.isPressed, Is.True);
			_autoTap.PreUpdate = dt =>
			{
				_autoTap.PreUpdate = null;
				InputSystem.DisableDevice(Mouse.current);
			};

			Frame();

			Assert.That(item.Completed, Is.True);
			var logs = _autoTap.GetLatestLogs();
			Assert.That(Kinds(logs), Is.EqualTo(new[] { AutoTapBase.LogEventKind.Completed }));
			Assert.That(logs[0].Item, Is.SameAs(item));
			Frame();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "not in the sequence of the next update");
		}

		[Test]
		public void StopInsideUpdateIsInThatSequenceInTheOrderTheOperationsWereStartedAndInNoOther()
		{
			var finger = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 5f);
			var key = _autoTap.Key(Key.A, 5f);
			var mouse = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Hold(5f));
			Frame();
			_autoTap.PreUpdate = dt => _autoTap.Stop();

			Frame();

			var logs = _autoTap.GetLatestLogs();
			Assert.That(logs.Select(e => e.Item), Is.EqualTo(new[] { finger, key, mouse }));
			Assert.That(logs.Select(e => e.Kind), Is.All.EqualTo(AutoTapBase.LogEventKind.Completed));
			Assert.That(logs.Select(e => e.Order), Is.All.Null);

			// What Stop released is processed by the next input update; no update builds a sequence after it.
			InputSystem.Update();
			Frame();
			Assert.That(_autoTap.GetLatestLogs().ToList(), Is.EqualTo(logs.ToList()));

			// The next run does not list them either.
			_autoTap.Start();
			Frame();
			Frame();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero, "not in the sequence of a later Update");
		}

		[Test]
		public void StopOutsideUpdateIsInNoSequenceAndNotInTheOnesOfTheNextRun()
		{
			var finger = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 5f);
			var key = _autoTap.Key(Key.A, 5f);
			Frame();

			_autoTap.Stop();
			Assert.That(finger.Completed, Is.True);
			Assert.That(key.Completed, Is.True);
			Frame();
			_autoTap.Start();
			var all = new List<AutoTapBase.LogEvent>();
			var next = _autoTap.Tap(1, TestScene.At(0.4f, 0.2f), 5f);
			for (var i = 0; i < 5; ++i)
			{
				Frame();
				all.AddRange(_autoTap.GetLatestLogs());
			}

			Assert.That(all.Any(e => e.Item == finger || e.Item == key), Is.False, "nothing of the earlier run");
			Assert.That(all.Select(e => (e.Item, e.Kind)), Is.EqualTo(new[] { (next, AutoTapBase.LogEventKind.Started) }));
		}

		[Test]
		public void EveryEndIsInOneSequenceOnly()
		{
			var all = new List<AutoTapBase.LogEvent>();

			void Collect()
			{
				// An Update that does nothing builds no sequence, and the one that is there was collected already.
				var builds = _autoTap.Enabled;
				Frame();
				if (builds)
				{
					all.AddRange(_autoTap.GetLatestLogs());
				}
			}

			var finished = _autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 0f);
			var resetBetween = _autoTap.Key(Key.A, 5f);
			var resetInside = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Hold(5f));
			var stopped = _autoTap.Tap(2, TestScene.At(0.6f, 0.2f), 5f);
			Collect();
			Collect();

			// Between two updates.
			InputSystem.ResetDevice(Keyboard.current);
			var dropped = _autoTap.Gamepad(new ControlSequence().Press(GamepadButton.South).Hold(5f));
			InputSystem.DisableDevice(Gamepad.current);
			Collect();

			// Inside an update.
			_autoTap.PreUpdate = dt =>
			{
				_autoTap.PreUpdate = null;
				InputSystem.ResetDevice(Mouse.current);
			};
			Collect();

			// By Stop inside an update.
			_autoTap.PreUpdate = dt => _autoTap.Stop();
			Collect();
			InputSystem.Update();
			Collect();

			foreach (var item in new[] { finished, resetBetween, resetInside, stopped, dropped })
			{
				Assert.That(item.Completed, Is.True, item.ToString());
				var ends = all.Count(e => e.Item == item && e.Kind == AutoTapBase.LogEventKind.Completed);
				Assert.That(ends, Is.EqualTo(1), $"{item.Kind} {item.Index} {item.EndReason}");
				Assert.That(all.Count(e => e.Item == item && e.Kind == AutoTapBase.LogEventKind.Started), Is.LessThanOrEqualTo(1));
			}
		}

		[Test]
		public void AnEndThatIsAddedInsideUpdateTakesItsPlaceByTheOrder()
		{
			var running = _autoTap.Key(Key.A, 5f);
			Frame();

			// Dropped by the input update of the next frame, which is outside Update: it has no number, and is kept.
			var dropped = _autoTap.Mouse(Gesture.PressAt(TestScene.At(0.5f, 0.5f)).Hold(5f));
			InputSystem.DisableDevice(Mouse.current);
			_autoTap.PreUpdate = dt =>
			{
				_autoTap.PreUpdate = null;
				InputSystem.ResetDevice(Keyboard.current);
			};

			Frame();

			var logs = _autoTap.GetLatestLogs().ToList();
			Assert.That(dropped.EndReason, Is.EqualTo(AutoTapBase.EndReason.Dropped));
			Assert.That(logs.Select(e => e.Item), Is.EqualTo(new[] { running, dropped }),
				"the end that has a number comes before the one that has none, although it was added later");
			Assert.That(logs[0].Order, Is.Not.Null);
			Assert.That(logs[1].Order, Is.Null);
		}

		[Test]
		public void WhatIsAddedInsideUpdateDoesNotMakeASequenceThatWasReceivedOutOfDate()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 5f);
			_autoTap.Tap(1, TestScene.At(0.4f, 0.2f), 5f);
			Frame();
			var third = _autoTap.Tap(2, TestScene.At(0.6f, 0.2f), 5f);
			var visited = new List<(AutoTapBase.LogItem Item, AutoTapBase.LogEventKind Kind)>();
			var counts = new List<int>();
			_autoTap.PreUpdate = dt =>
			{
				_autoTap.PreUpdate = null;
				var logs = _autoTap.GetLatestLogs();
				counts.Add(logs.Count);
				var enumerator = logs.GetEnumerator();
				Assert.That(enumerator.MoveNext(), Is.True);
				visited.Add((enumerator.Current.Item, enumerator.Current.Kind));

				// The reset ends the three operations, which are added to the sequence that was received.
				InputSystem.ResetDevice(Touchscreen.current);

				counts.Add(logs.Count);
				while (enumerator.MoveNext())
				{
					visited.Add((enumerator.Current.Item, enumerator.Current.Kind));
				}

				Assert.That(logs[logs.Count - 1].Kind, Is.EqualTo(AutoTapBase.LogEventKind.Completed));
			};

			Frame();

			Assert.That(counts, Is.EqualTo(new[] { 1, 4 }), "the count is that of the time it is read");
			Assert.That(visited.Select(v => v.Kind), Is.EqualTo(new[]
			{
				AutoTapBase.LogEventKind.Started,
				AutoTapBase.LogEventKind.Completed,
				AutoTapBase.LogEventKind.Completed,
				AutoTapBase.LogEventKind.Completed,
			}), "the enumeration that was running visits what was added");
			Assert.That(visited[0].Item, Is.SameAs(third));
			var after = _autoTap.GetLatestLogs();
			Assert.That(after.Count, Is.EqualTo(4), "and it is the sequence that is returned afterwards");
		}

		[Test]
		public void DisposeEmptiesTheSequenceAndOutdatesTheOnesThatWereReturned()
		{
			_autoTap.Tap(0, TestScene.At(0.2f, 0.2f), 1f);
			Frame();
			var logs = _autoTap.GetLatestLogs();
			Assert.That(logs.Count, Is.EqualTo(1));

			_autoTap.Dispose();

			Assert.Throws<InvalidOperationException>(() => { var _ = logs.Count; });
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero);
			Frame();
			Assert.That(_autoTap.GetLatestLogs().Count, Is.Zero);
		}

		[Test]
		public void ADisposedInstanceBuildsNoSequence()
		{
			var other = new TestAutoTap();
			other.Dispose();
			var logs = other.GetLatestLogs();

			InputSystem.Update();
			other.Update(k_Dt);

			Assert.That(logs.Count, Is.Zero, "the version of a disposed instance does not change any more");
		}

		[Test]
		public void EnumeratingTheSequenceAllocatesNothing()
		{
			for (var i = 0; i < 3; ++i)
			{
				_autoTap.Tap(i, TestScene.At(0.1f + 0.2f * i, 0.2f), 1f);
			}

			Frame();
			var logs = _autoTap.GetLatestLogs();
			Assert.That(logs.Count, Is.EqualTo(3));
			const int rounds = 4000;

			// Allocation of the Mono heap is what is measured. The enumeration through IEnumerable boxes the enumerator,
			// which makes sure that the measurement sees an allocation at all. A collection that runs in the middle of a
			// measurement hides what was allocated before it, so it is the largest of the runs that must show it.
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
			}, useLargest: true);
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
		/// decide the result. <paramref name="useLargest"/> takes the largest of the runs instead, for what has to be seen
		/// in at least one of them.
		/// </summary>
		static long MeasureAllocation(Func<int> work, bool useLargest = false)
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
			return useLargest ? results[results.Count - 1] : results[results.Count / 2];
		}
	}
}
#endif
