using AutoTap;
using UnityEngine;

namespace Sample
{
	public class RandomTap : AutoTap
	{
		public override void Setup(Canvas canvasForMarker, Sprite markerSprite)
		{
			SetupInternal(canvasForMarker, markerSprite, 5);
		}

		protected override void Update(int index)
		{
			var width = Screen.width;
			var height = Screen.height;
			if (Random.Range(0, 100) < 50)
			{
				Fire(index, new Vector2(width * Random.Range(0f, 1f), height * Random.Range(0f, 1f)),
					Random.Range(0.1f, 1f));
			}
			else
			{
				Fire(
					index,
					new Vector2(width * Random.Range(0f, 1f), height * Random.Range(0f, 1f)),
					new Vector2(width * Random.Range(0f, 1f), height * Random.Range(0f, 1f)),
					Random.Range(0.1f, 1f));
			}
		}
	}
}