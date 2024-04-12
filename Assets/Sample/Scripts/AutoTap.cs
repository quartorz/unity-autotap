using System;
using UnityAutoTap;
using UnityEngine;

namespace Sample
{
	public abstract class AutoTap : AutoTapBase
	{
		new class LogItem : AutoTapBase.LogItem
		{
		}

		LogItem[] _logItems;

		protected override void SetupInternal(Canvas canvasForMarker, Sprite markerSprite, int tapCount)
		{
			base.SetupInternal(canvasForMarker, markerSprite, tapCount);

			_logItems = new LogItem[tapCount];

			for (var i = 0; i < tapCount; ++i)
			{
				_logItems[i] = new LogItem();
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			_logItems = null;
		}

		protected override void CreateLog(
			int index, GameObject target, Vector2 startPosition, Vector2 endPosition, float duration,
			out AutoTapBase.LogItem logItem)
		{
			logItem = _logItems[index];
			logItem.Index = index;
			logItem.DateTime = DateTime.Now;
			logItem.Target = target;
			logItem.ScreenPoint = startPosition;
			logItem.DragTo = (startPosition == endPosition) ? null : (Vector2?) endPosition;
			logItem.Duration = duration;
			logItem.FrameCount = Time.frameCount;
		}
	}
}