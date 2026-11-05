#if ENABLE_AUTOTAP
using NUnit.Framework;
using UnityEngine;

namespace UnityAutoTap.Tests
{
	/// <summary>
	/// <see cref="PointerPredictor"/> used on its own, told what happened to a pointer in each input update.
	/// </summary>
	public class PointerPredictorTests
	{
		TestScene _scene;
		PointerPredictor _predictor;
		long _order;
		int _frame;

		[SetUp]
		public void Setup()
		{
			_scene = new TestScene();
			_predictor = new PointerPredictor();
			_order = 0;
			_frame = 0;
		}

		[TearDown]
		public void TearDown()
		{
			_scene.Dispose();
		}

		/// <summary>
		/// One input update in which the pointer ended at <paramref name="position"/>.
		/// </summary>
		void Update(Vector2 position, bool pressed = false, bool released = false)
		{
			++_frame;
			var beyond = !pressed && _predictor.IsBeyondDragThreshold(position) ? _order : -1;
			_predictor.Process(position, pressed, released, _order++, _frame, beyond, beyond >= 0 ? _frame : 0);
		}

		[Test]
		public void APressIsPredictedForWhatIsUnderThePointer()
		{
			var button = _scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);

			Update(TestScene.At(0.2f, 0.2f), pressed: true);

			Assert.That(_predictor.Active, Is.True);
			Assert.That(_predictor.Prediction.PointerPress, Is.SameAs(button.gameObject));
			Assert.That(_predictor.Prediction.PointerClick, Is.SameAs(button.gameObject));
			Assert.That(_predictor.Prediction.PointerDrag, Is.Null);
			Assert.That(_predictor.Prediction.PressHits.Count, Is.EqualTo(1));
			Assert.That(_predictor.Prediction.PressHits[0].gameObject, Is.SameAs(button.gameObject));
		}

		[Test]
		public void AMoveBeyondTheThresholdOnADragHandlerIsADrag()
		{
			var draggable = _scene.Add<DragHandler>("Draggable", 0f, 0f, 1f, 1f);
			Update(TestScene.At(0.2f, 0.2f), pressed: true);
			Assert.That(_predictor.Prediction.PointerDrag, Is.SameAs(draggable.gameObject));
			Assert.That(_predictor.Prediction.Dragging, Is.False);

			Assert.That(_predictor.IsBeyondDragThreshold(TestScene.At(0.8f, 0.8f)), Is.True);
			Update(TestScene.At(0.8f, 0.8f));

			Assert.That(_predictor.Prediction.Dragging, Is.True);
			Assert.That(_predictor.IsBeyondDragThreshold(TestScene.At(0.9f, 0.9f)), Is.False, "it is dragging already");

			Update(TestScene.At(0.8f, 0.8f), released: true);

			Assert.That(_predictor.Active, Is.False);
			Assert.That(_predictor.Prediction.Dragging, Is.True);
			Assert.That(_predictor.Prediction.ClickTarget, Is.Null);
		}

		[Test]
		public void AMoveWithinTheThresholdIsNotADragAndTheReleaseClicks()
		{
			var handler = _scene.Add<ClickDragHandler>("Handler", 0f, 0f, 1f, 1f);
			var start = TestScene.At(0.5f, 0.5f);
			Update(start, pressed: true);

			Assert.That(_predictor.IsBeyondDragThreshold(start + Vector2.one), Is.False);
			Update(start + Vector2.one);
			Update(start + Vector2.one, released: true);

			Assert.That(_predictor.Prediction.Dragging, Is.False);
			Assert.That(_predictor.Prediction.ClickTarget, Is.SameAs(handler.gameObject));
		}

		[Test]
		public void WithoutADragHandlerAMoveIsNotADrag()
		{
			_scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);
			Update(TestScene.At(0.2f, 0.2f), pressed: true);

			Assert.That(_predictor.IsBeyondDragThreshold(TestScene.At(0.8f, 0.8f)), Is.False);
			Update(TestScene.At(0.8f, 0.8f));
			Update(TestScene.At(0.8f, 0.8f), released: true);

			Assert.That(_predictor.Prediction.Dragging, Is.False);
			Assert.That(_predictor.Prediction.ClickTarget, Is.Null, "released outside the button");
		}

		[Test]
		public void ADropIsPredictedForADragThatIsReleasedOnADropHandler()
		{
			_scene.Add<DragHandler>("Draggable", 0.1f, 0.1f, 0.3f, 0.3f);
			var drop = _scene.Add<DropHandler>("Drop", 0.6f, 0.6f, 0.9f, 0.9f);
			Update(TestScene.At(0.2f, 0.2f), pressed: true);
			Update(TestScene.At(0.8f, 0.8f));

			Update(TestScene.At(0.8f, 0.8f), released: true);

			Assert.That(_predictor.Prediction.Dragging, Is.True);
			Assert.That(_predictor.Prediction.DropTarget, Is.SameAs(drop.gameObject));
		}

		[Test]
		public void APressAndAReleaseInOneUpdateAreHandledInThisOrder()
		{
			var button = _scene.Add<ClickHandler>("Button", 0.1f, 0.1f, 0.3f, 0.3f);

			Update(TestScene.At(0.2f, 0.2f), pressed: true, released: true);

			Assert.That(_predictor.Active, Is.False);
			Assert.That(_predictor.Prediction.PointerPress, Is.SameAs(button.gameObject));
			Assert.That(_predictor.Prediction.ClickTarget, Is.SameAs(button.gameObject));
		}
	}
}
#endif
