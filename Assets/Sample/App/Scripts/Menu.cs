using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
	public class Menu : MonoBehaviour
	{
		class Item
		{
			public string Text;
			public Action OnClick;
		}

		[SerializeField] Button background;
		[SerializeField] RectTransform content;
		[SerializeField] MenuItem menuItemPrefab;
		[SerializeField] float duration = 0.5f;

		bool _started;

		public bool Opened { get; private set; }

		void Start()
		{
			_started = true;
			background.onClick.AddListener(() => StartCoroutine(CoClose()));
			Setup();
			CloseImmediate();
		}

		void Setup()
		{
			var items = new Item[]
			{
				new Item {Text = "Return to Title", OnClick = ScreenManager.Change<TitleScreen>},
			};

			foreach (var item in items)
			{
				Instantiate(menuItemPrefab, content).Setup(item.Text, item.OnClick);
			}
		}

		public void CloseImmediate()
		{
			Opened = false;

			if (!_started)
			{
				return;
			}

			gameObject.SetActive(false);
			content.localPosition = Vector3.zero;
		}

		public IEnumerator CoOpen()
		{
			if (gameObject.activeSelf)
			{
				yield break;
			}

			using (new ScreenManager.Shield())
			{
				gameObject.SetActive(true);

				var destination = new Vector3(content.rect.width, 0);
				var time = 0f;
				while (time < duration)
				{
					content.localPosition = Vector3.Lerp(Vector3.zero, destination, time / duration);
					yield return null;
					time += Time.deltaTime;
				}

				content.localPosition = destination;
				Opened = true;
			}
		}

		public IEnumerator CoClose()
		{
			if (!gameObject.activeSelf)
			{
				yield break;
			}

			using (new ScreenManager.Shield())
			{
				Opened = false;

				var startPosition = content.localPosition;
				var time = 0f;
				while (time < duration)
				{
					content.localPosition = Vector3.Lerp(startPosition, Vector3.zero, time / duration);
					yield return null;
					time += Time.deltaTime;
				}

				content.localPosition = Vector3.zero;

				gameObject.SetActive(false);
			}
		}
	}
}