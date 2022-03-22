using System;
using AutoTap;
using UnityEngine;
using Base = AutoTap.AutoTap;

namespace Sample
{
	public abstract class AutoTap : Base
	{
		new class LogItem : Base.LogItem
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
			out Base.LogItem logItem)
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