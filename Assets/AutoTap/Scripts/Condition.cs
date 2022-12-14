#if !DISABLE_AUTOTAP
namespace AutoTap
{
	public class Condition
	{
		public Scenario Owner;
		public bool Value { get; protected set; }
		public virtual string Status => "";
		public virtual void Prepare() => Value = true;

		public virtual void Update(float deltaTime)
		{
		}

		public virtual void OnLoop() => Value = true;
	}

	public class Once : Condition
	{
		public override void OnLoop()
		{
			Value = false;
		}
	}

	public class Repeat : Condition
	{
		public int Count;
		public int CurrentCount;

		public override void Prepare()
		{
			CurrentCount = 0;
			Value = CurrentCount < Count;
		}

		public override void Update(float deltaTime)
		{
			Value = CurrentCount < Count;
		}

		public override void OnLoop()
		{
			Value = ++CurrentCount < Count;
		}
	}
}
#endif