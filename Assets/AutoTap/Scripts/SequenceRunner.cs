#if ENABLE_AUTOTAP
using System;
using UnityEngine;

namespace UnityAutoTap
{
	/// <summary>
	/// What a <see cref="SequenceRunner"/> operates: a device that has no position.
	/// </summary>
	internal interface ISequenceTarget
	{
		/// <summary>
		/// Reads the value that the control has in the state that was queued last.
		/// </summary>
		object ReadControl(string path);

		/// <summary>
		/// Queues one event that writes <paramref name="value"/> to the control.
		/// </summary>
		void SetControl(string path, object value);

		/// <summary>
		/// Queues one event that presses the control of a press step.
		/// </summary>
		void Press(ControlSequence.Step step);

		/// <summary>
		/// Queues one event that releases the control of a release step.
		/// </summary>
		void Release(ControlSequence.Step step);

		/// <summary>
		/// Queues the text events of a text step.
		/// </summary>
		void Text(string text);

		/// <summary>
		/// Queues the event of a composition step.
		/// </summary>
		void Composition(string text);
	}

	/// <summary>
	/// Carries out the steps of a <see cref="ControlSequence"/>, one <see cref="Advance"/> per frame.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It works as <see cref="GestureRunner"/> does: a step that takes time queues one event per
	/// <see cref="Advance"/>, a step that takes no time is followed at once by the next one, the time that a step does
	/// not use is passed on, and a release that follows a press of the same call waits for the next call.
	/// </para>
	/// <para>
	/// <c>Simulate</c> calls <see cref="Advance"/> with no time, so that the steps at the start of the sequence that
	/// take no time are queued in the frame of <c>Simulate</c>.
	/// </para>
	/// </remarks>
	internal sealed class SequenceRunner
	{
		readonly ControlSequence _sequence;
		readonly ISequenceTarget _target;

		int _index;
		float _elapsed;
		bool _started;
		object _fromValue;
		object _toValue;

		public SequenceRunner(ControlSequence sequence, ISequenceTarget target)
		{
			_sequence = sequence;
			_target = target;
		}

		/// <summary>
		/// <c>true</c> once every step was carried out, or a <c>HoldUntil</c> timed out.
		/// </summary>
		public bool Done { get; private set; }

		/// <summary>
		/// Why the sequence is done: <see cref="AutoTapBase.EndReason.Finished"/> or
		/// <see cref="AutoTapBase.EndReason.TimedOut"/>.
		/// </summary>
		public AutoTapBase.EndReason Reason { get; private set; } = AutoTapBase.EndReason.Finished;

		/// <summary>
		/// <c>true</c> if the last <see cref="Advance"/> pressed a button.
		/// </summary>
		public bool PressedInLastAdvance { get; private set; }

		/// <summary>
		/// Carries out the steps for one frame.
		/// </summary>
		/// <param name="deltaTime">The time since the previous call, in seconds.</param>
		public void Advance(float deltaTime)
		{
			PressedInLastAdvance = false;
			var remaining = Mathf.Max(0f, deltaTime);
			while (!Done)
			{
				if (_index >= _sequence.Steps.Count)
				{
					Done = true;
					return;
				}

				var step = _sequence.Steps[_index];
				switch (step.Kind)
				{
					case ControlSequence.StepKind.Set:
					{
						var path = (string)step.Control;
						if (!_started)
						{
							if (step.Seconds > 0 && remaining <= 0)
							{
								return;
							}

							_fromValue = _target.ReadControl(path);
							_toValue = Interpolation.ConvertTo(step.Value, _fromValue.GetType());
							_elapsed = 0;
							_started = true;
						}

						if (step.Seconds <= 0)
						{
							_target.SetControl(path, _toValue);
							Next();
							break;
						}

						_elapsed += remaining;
						var t = Mathf.Clamp01(_elapsed / step.Seconds);
						_target.SetControl(path, t >= 1f ? _toValue : Interpolation.Lerp(_fromValue, _toValue, t));
						if (t < 1f)
						{
							return;
						}

						remaining = _elapsed - step.Seconds;
						Next();
						break;
					}
					case ControlSequence.StepKind.Hold:
					{
						if (!_started)
						{
							if (step.Seconds > 0 && remaining <= 0)
							{
								return;
							}

							_started = true;
							_elapsed = 0;
						}

						_elapsed += remaining;
						if (_elapsed < step.Seconds)
						{
							return;
						}

						remaining = _elapsed - step.Seconds;
						Next();
						break;
					}
					case ControlSequence.StepKind.HoldUntil:
					{
						if (!_started)
						{
							_started = true;
							_elapsed = 0;
						}

						if (step.Condition())
						{
							Next();
							break;
						}

						_elapsed += remaining;
						if (_elapsed >= step.Seconds)
						{
							Reason = AutoTapBase.EndReason.TimedOut;
							Done = true;
						}

						return;
					}
					case ControlSequence.StepKind.Press:
						_target.Press(step);
						PressedInLastAdvance = true;
						Next();
						break;
					case ControlSequence.StepKind.Release:
						if (PressedInLastAdvance)
						{
							return;
						}

						_target.Release(step);
						Next();
						break;
					case ControlSequence.StepKind.Text:
						_target.Text(step.Text);
						Next();
						break;
					case ControlSequence.StepKind.Composition:
						_target.Composition(step.Text);
						Next();
						break;
					default:
						throw new InvalidOperationException($"A step of kind {step.Kind} cannot be carried out.");
				}
			}
		}

		void Next()
		{
			++_index;
			_started = false;
			_elapsed = 0;
		}
	}
}
#endif
