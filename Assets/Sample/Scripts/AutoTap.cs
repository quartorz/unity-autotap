using UnityAutoTap;

namespace Sample
{
	public abstract class AutoTap : AutoTapBase
	{
		protected AutoTap(int slotCount) : base(slotCount)
		{
		}

		new class LogItem : AutoTapBase.LogItem
		{
		}

		protected override AutoTapBase.LogItem NewLogItem(int index)
		{
			return new LogItem();
		}
	}
}