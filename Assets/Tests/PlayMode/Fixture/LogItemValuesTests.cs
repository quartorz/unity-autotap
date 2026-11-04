#if ENABLE_AUTOTAP
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using EndReason = UnityAutoTap.AutoTapBase.EndReason;
using LogItem = UnityAutoTap.AutoTapBase.LogItem;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// The values of a <see cref="LogItem"/> that are known so far over real frames: <c>EndScreenPoint</c>,
	/// <c>Duration</c> and <c>CurrentStep</c>, and what <c>ToString</c> writes of them.
	/// </summary>
	/// <remarks>
	/// The frame arithmetic is that of <see cref="TouchTests"/>: <c>Simulate</c> in frame N queues the press, frame N + 1
	/// processes it and is the first one in which <see cref="AutoTapBase.Update(float)"/> advances the plan, and the
	/// release of a plan that is over in frame M is processed in frame M + 1. <see cref="AutoTapFixture.Dt"/> is 0.1 s.
	/// </remarks>
	public class LogItemValuesTests : AutoTapFixture
	{
		[UnityTest]
		public IEnumerator LogItem_OfATap_ShowsThePlanUntilItEndsAndTheCountedValuesAfterwards()
		{
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();
			var center = Rig.Center(button.gameObject);

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, center, 0.35f));
			var points = new List<Vector2?> { item.EndScreenPoint };
			var durations = new List<float?> { item.Duration };
			var steps = new List<int> { item.CurrentStep };
			while (!item.Completed)
			{
				yield return Step();
				points.Add(item.EndScreenPoint);
				durations.Add(item.Duration);
				steps.Add(item.CurrentStep);
				Assert.That(steps.Count, Is.LessThan(30), "the tap does not end");
			}

			Assert.That(points, Is.All.EqualTo((Vector2?)center));
			Assert.That(durations.Take(durations.Count - 1), Is.All.EqualTo((float?)0.35f).Within(0.001f), "the plan, until the end");
			Assert.That(durations.Last().Value, Is.EqualTo(0.4f).Within(0.001f), "four frames of 0.1 s used up the hold");
			Assert.That(steps.First(), Is.EqualTo(-1), "the press was not processed yet");
			Assert.That(steps.Skip(1).Take(steps.Count - 2), Is.All.EqualTo(1), "under way, in the hold");
			Assert.That(steps.Last(), Is.EqualTo(1), "the last step is the hold");
			Assert.That(item.EndReason, Is.EqualTo(EndReason.Finished));
		}

		[UnityTest]
		public IEnumerator LogItem_OfADragToAFunction_KnowsItsEndOnceTheMoveStarted()
		{
			yield return BuildRig();
			var source = Rig.Add("Source", Rig.Cell(0, 0));
			var zone = Rig.Add("Zone", Rig.Cell(3, 0));
			yield return Settle();
			StartAuto();
			var destination = Rig.Center(source);

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateGesture(
				0, Gesture.PressAt(Rig.Center(source)).Hold(0.25f).MoveTo(() => destination, 0.2f)));
			Assert.That(item.EndScreenPoint, Is.Null);
			Assert.That(item.Duration, Is.EqualTo(0.45f).Within(0.001f));

			destination = Rig.Center(zone);
			yield return Step();
			yield return Step();
			Assert.That(item.EndScreenPoint, Is.Null, "the move did not start");

			yield return Until(() => item.EndScreenPoint.HasValue, "the move to start");
			Assert.That(item.EndScreenPoint, Is.EqualTo((Vector2?)Rig.Center(zone)), "the function was called when the move started");
			Assert.That(item.Completed, Is.False);
			yield return Until(() => item.Completed, "the drag to end");
			Assert.That(item.EndScreenPoint.Value.x, Is.EqualTo(Rig.Center(zone).x).Within(0.5f));
			Assert.That(item.CurrentStep, Is.EqualTo(2));
		}

		[UnityTest]
		public IEnumerator LogItem_OfAKeyPress_HasNoEndPointAndItsDurationIsCounted()
		{
			yield return BuildRig();
			yield return Settle();
			StartAuto();

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateKeyPress(Key.Space, 0.25f));
			Assert.That(item.EndScreenPoint, Is.Null);
			Assert.That(item.Duration, Is.EqualTo(0.25f).Within(0.001f));
			Assert.That(item.CurrentStep, Is.EqualTo(-1), "what Simulate queued was not processed yet");

			yield return Until(() => item.Completed, "the key to be released");

			Assert.That(item.EndScreenPoint, Is.Null);
			Assert.That(item.Duration, Is.EqualTo(0.3f).Within(0.001f), "three frames of 0.1 s used up the hold");
			Assert.That(item.CurrentStep, Is.EqualTo(item.Sequence.Steps.Count - 1));
		}

		[UnityTest]
		public IEnumerator LogItem_ToString_WritesWhatIsKnown()
		{
			yield return BuildRig();
			var button = Rig.AddButton("Button", Rig.Cell(1, 1));
			yield return Settle();
			StartAuto();
			var center = Rig.Center(button.gameObject);

			LogItem item = null;
			yield return Step(() => item = Auto.SimulateTap(0, center, 0.25f));

			Assert.That(item.Target, Is.Not.Null, "the tap hits the button");
			Assert.That(item.ToString(), Does.Contain(", Duration: 0.25, DateTime: "));
			Assert.That(item.ToString(), Does.Contain($"\nScreenPoint: {center} → {center} GameObject: {item.Target.name}"));
			yield return Until(() => item.Completed, "the tap to end");
			Assert.That(item.ToString(), Does.Contain(", Duration: 0.30, DateTime: "));
		}
	}
}
#endif
