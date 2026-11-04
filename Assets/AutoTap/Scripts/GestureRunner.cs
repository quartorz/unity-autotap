#if ENABLE_AUTOTAP
using System;
using UnityEngine;

namespace UnityAutoTap
{
	/// <summary>
	/// What a <see cref="GestureRunner"/> operates: a finger, or a pointer device.
	/// </summary>
	internal interface IGestureTarget
	{
		/// <summary>
		/// The position that was queued last.
		/// </summary>
		Vector2 Position { get; }

		/// <summary>
		/// Queues one event that moves to <paramref name="position"/>.
		/// </summary>
		void MoveTo(Vector2 position);

		/// <summary>
		/// Reads the value that the control has in the state that was queued last.
		/// </summary>
		object ReadControl(string path);

		/// <summary>
		/// Queues one event that writes <paramref name="value"/> to the control.
		/// </summary>
		void SetControl(string path, object value);

		/// <summary>
		/// Queues one event that presses a button.
		/// </summary>
		void Press(string button);

		/// <summary>
		/// Queues one event that releases a button.
		/// </summary>
		void Release(string button);

		/// <summary>
		/// Queues one event that scrolls.
		/// </summary>
		void Scroll(Vector2 delta);
	}

	/// <summary>
	/// Carries out the steps of a <see cref="Gesture"/> after its first step, one <see cref="Advance"/> per frame.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A step that takes time queues one event per <see cref="Advance"/>. A step that takes no time queues one event
	/// and is followed at once by the next step, in the same <see cref="Advance"/>. The time that a step does not
	/// use is passed on to the next one.
	/// </para>
	/// <para>
	/// <see cref="AdvanceAfterFirstStep"/> carries out the steps that take no time and come right after the first
	/// step, which the caller queued itself, by the same rule and in the frame of the first step.
	/// </para>
	/// </remarks>
	internal sealed class GestureRunner
	{
		readonly Gesture _gesture;
		readonly int _stepCount;
		readonly IGestureTarget _target;
		readonly PlanClock _clock = new PlanClock();
		readonly int _lastMoveIndex;

		int _index = 1;
		float _elapsed;
		bool _started;
		Vector2 _from;
		Vector2 _to;
		object _fromValue;
		object _toValue;
		Vector2? _plannedEnd;

		public GestureRunner(Gesture gesture, int stepCount, IGestureTarget target)
		{
			_gesture = gesture;
			_stepCount = stepCount;
			_target = target;

			// The first step is carried out by the caller, and it is where the pointer is put first.
			for (var i = 0; i < stepCount; ++i)
			{
				var step = gesture.Steps[i];
				switch (step.Kind)
				{
					case Gesture.StepKind.Press:
					case Gesture.StepKind.Hover:
						if (step.Position.HasValue)
						{
							_lastMoveIndex = i;
						}

						break;
					case Gesture.StepKind.Move:
						_lastMoveIndex = i;
						_clock.AddSeconds(step.Seconds);
						break;
					case Gesture.StepKind.Set:
					case Gesture.StepKind.Hold:
						_clock.AddSeconds(step.Seconds);
						break;
					case Gesture.StepKind.HoldUntil:
						_clock.AddHoldUntil();
						break;
				}
			}

			_plannedEnd = gesture.Steps[_lastMoveIndex].Position;
		}

		/// <summary>
		/// The index of the step that the gesture is at, in <see cref="Gesture.Steps"/>: the first step, which the
		/// caller carries out, until <see cref="Advance"/> or <see cref="AdvanceAfterFirstStep"/> reaches the next one;
		/// the step that is being carried out; when the gesture is done, the step it was at, or the last step if
		/// every step was carried out.
		/// </summary>
		public int CurrentStep { get; private set; }

		/// <summary>
		/// The sum of the times that <see cref="Advance"/> was given, in seconds.
		/// </summary>
		public float Elapsed => _clock.Elapsed;

		/// <summary>
		/// The time that the plan takes, in seconds, or <c>null</c> while it is not known: the plan has a
		/// <c>HoldUntil</c> step that has not met its condition yet. See <see cref="PlanClock.Planned"/>.
		/// </summary>
		public float? PlannedDuration => _clock.Planned;

		/// <summary>
		/// Where the pointer is put last according to the plan, or <c>null</c> while it is not known: the last move
		/// takes its destination from a function, and the move has not started yet.
		/// </summary>
		public Vector2? PlannedEndPoint => _plannedEnd;

		/// <summary>
		/// <c>true</c> once every step was carried out, or a <c>HoldUntil</c> timed out.
		/// </summary>
		public bool Done { get; private set; }

		/// <summary>
		/// Why the gesture is done: <see cref="AutoTapBase.EndReason.Finished"/>,
		/// <see cref="AutoTapBase.EndReason.TimedOut"/> or <see cref="AutoTapBase.EndReason.Refused"/>.
		/// </summary>
		public AutoTapBase.EndReason Reason { get; private set; } = AutoTapBase.EndReason.Finished;

		/// <summary>
		/// <c>true</c> if the last <see cref="Advance"/> or <see cref="AdvanceAfterFirstStep"/> pressed a button, or
		/// the first step did. What is pressed is not released in the same input update, so the caller waits for the
		/// next frame before it ends the operation.
		/// </summary>
		public bool PressedInLastAdvance { get; private set; }

		/// <summary>
		/// Stops the gesture because a press was refused. The caller ends the operation with
		/// <see cref="AutoTapBase.EndReason.Refused"/>.
		/// </summary>
		public void Refuse()
		{
			Reason = AutoTapBase.EndReason.Refused;
			Done = true;
		}

		/// <summary>
		/// Carries out the steps that take no time and come right after the first step, which the caller queued in
		/// the same frame. A step that takes time is left to <see cref="Advance"/>.
		/// </summary>
		/// <param name="firstStepPressed"><c>true</c> if the first step pressed a button.</param>
		public void AdvanceAfterFirstStep(bool firstStepPressed)
		{
			Run(0f, firstStepPressed);
		}

		/// <summary>
		/// Carries out the steps for one frame.
		/// </summary>
		/// <remarks>
		/// A release step that comes after a press of the same call waits for the next call, so that a button is
		/// never pressed and released by one input update.
		/// </remarks>
		/// <param name="deltaTime">The time since the previous call, in seconds.</param>
		public void Advance(float deltaTime)
		{
			_clock.Advance(deltaTime);
			Run(deltaTime, false);
		}

		void Run(float deltaTime, bool pressedBefore)
		{
			PressedInLastAdvance = pressedBefore;
			var remaining = Mathf.Max(0f, deltaTime);
			while (!Done)
			{
				if (_index >= _stepCount)
				{
					CurrentStep = _gesture.Steps.Count - 1;
					Done = true;
					return;
				}

				CurrentStep = _index;
				var step = _gesture.Steps[_index];
				switch (step.Kind)
				{
					case Gesture.StepKind.Move:
					{
						if (!_started)
						{
							if (step.Seconds > 0 && remaining <= 0)
							{
								return;
							}

							_from = _target.Position;
							_to = step.PositionFunc != null ? step.PositionFunc() : step.Position.Value;
							if (_index == _lastMoveIndex)
							{
								_plannedEnd = _to;
							}

							_elapsed = 0;
							_started = true;
						}

						if (step.Seconds <= 0)
						{
							_target.MoveTo(_to);
							Next();
							break;
						}

						_elapsed += remaining;
						var t = Mathf.Clamp01(_elapsed / step.Seconds);
						_target.MoveTo(t >= 1f ? _to : Vector2.LerpUnclamped(_from, _to, t));
						if (t < 1f)
						{
							return;
						}

						remaining = _elapsed - step.Seconds;
						Next();
						break;
					}
					case Gesture.StepKind.Set:
					{
						if (!_started)
						{
							if (step.Seconds > 0 && remaining <= 0)
							{
								return;
							}

							_fromValue = _target.ReadControl(step.Path);
							_toValue = Interpolation.ConvertTo(step.Value, _fromValue.GetType());
							_elapsed = 0;
							_started = true;
						}

						if (step.Seconds <= 0)
						{
							_target.SetControl(step.Path, _toValue);
							Next();
							break;
						}

						_elapsed += remaining;
						var t = Mathf.Clamp01(_elapsed / step.Seconds);
						_target.SetControl(step.Path, t >= 1f ? _toValue : Interpolation.Lerp(_fromValue, _toValue, t));
						if (t < 1f)
						{
							return;
						}

						remaining = _elapsed - step.Seconds;
						Next();
						break;
					}
					case Gesture.StepKind.Hold:
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
					case Gesture.StepKind.HoldUntil:
					{
						if (!_started)
						{
							_started = true;
							_elapsed = 0;
						}

						if (step.Condition())
						{
							_clock.ResolveHoldUntil(_elapsed);
							Next();
							break;
						}

						_elapsed += remaining;
						if (_elapsed >= step.Seconds)
						{
							Reason = AutoTapBase.EndReason.TimedOut;
							Done = true;
							return;
						}

						return;
					}
					case Gesture.StepKind.Press:
						_target.Press(step.Button);
						if (!Done)
						{
							PressedInLastAdvance = true;
						}

						Next();
						break;
					case Gesture.StepKind.Release:
						if (PressedInLastAdvance)
						{
							return;
						}

						_target.Release(step.Button);
						Next();
						break;
					case Gesture.StepKind.Scroll:
						_target.Scroll(step.Delta);
						Next();
						break;
					default:
						throw new InvalidOperationException($"A step of kind {step.Kind} cannot follow the first step.");
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

	/// <summary>
	/// The time that a plan takes: what the plan says before it runs, and what was passed to it while it ran.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The time is counted in the seconds that <see cref="AutoTapBase.Update(float)"/> passes. A step that takes a given
	/// time (a move, a change of a value, a hold) adds it. A <c>HoldUntil</c> step takes as long as it waits, so it is
	/// not known before that: it adds nothing until it met its condition, and what it waited is added then.
	/// </para>
	/// </remarks>
	internal sealed class PlanClock
	{
		float _planned;
		int _unresolved;
		float _resolved;

		/// <summary>
		/// The sum of the times that <see cref="Advance"/> was given, in seconds.
		/// </summary>
		public float Elapsed { get; private set; }

		/// <summary>
		/// The time that the plan takes, in seconds: the times of the steps that take a given time, and the times that
		/// the <c>HoldUntil</c> steps that met their condition waited. <c>null</c> while a <c>HoldUntil</c> step has not
		/// met its condition.
		/// </summary>
		public float? Planned => _unresolved > 0 ? (float?)null : _planned + _resolved;

		/// <summary>
		/// Adds the time of a step that takes a given time.
		/// </summary>
		public void AddSeconds(float seconds)
		{
			_planned += seconds;
		}

		/// <summary>
		/// Adds a <c>HoldUntil</c> step, whose time is not known yet.
		/// </summary>
		public void AddHoldUntil()
		{
			++_unresolved;
		}

		/// <summary>
		/// Tells that a <c>HoldUntil</c> step met its condition after waiting <paramref name="seconds"/>.
		/// </summary>
		public void ResolveHoldUntil(float seconds)
		{
			--_unresolved;
			_resolved += seconds;
		}

		/// <summary>
		/// Counts the time that the plan was given for one frame.
		/// </summary>
		public void Advance(float deltaTime)
		{
			Elapsed += Mathf.Max(0f, deltaTime);
		}
	}

	/// <summary>
	/// Interpolation of the values that a <see cref="Gesture"/> or a <see cref="ControlSequence"/> changes over time.
	/// </summary>
	internal static class Interpolation
	{
		/// <summary>
		/// Converts a value to the type of a control.
		/// </summary>
		public static object ConvertTo(object value, Type type)
		{
			if (value.GetType() == type)
			{
				return value;
			}

			try
			{
				return Convert.ChangeType(value, type);
			}
			catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException)
			{
				throw new ArgumentException($"A {value.GetType().Name} cannot be written to a control of {type.Name}.", e);
			}
		}

		/// <summary>
		/// Gives the value at <paramref name="t"/> between two values. Values that cannot be interpolated stay at
		/// <paramref name="from"/> until the end.
		/// </summary>
		public static object Lerp(object from, object to, float t)
		{
			switch (from)
			{
				case float a:
					return Mathf.LerpUnclamped(a, (float)to, t);
				case Vector2 a:
					return Vector2.LerpUnclamped(a, (Vector2)to, t);
				case Vector3 a:
					return Vector3.LerpUnclamped(a, (Vector3)to, t);
				case Quaternion a:
					// The state of a rotation that nothing was written to is four zeros, which is no rotation.
					if (a.x == 0f && a.y == 0f && a.z == 0f && a.w == 0f)
					{
						a = Quaternion.identity;
					}

					return Quaternion.SlerpUnclamped(a, (Quaternion)to, t);
				default:
					return t >= 1f ? to : from;
			}
		}
	}
}
#endif
