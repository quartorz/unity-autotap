using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
	public class HomeScreen : ScreenBase
	{
		[SerializeField] Button toggle;
		[SerializeField] Form form;
		[SerializeField] Item itemPrefab;
		[SerializeField] RectTransform scrollRect;
		[SerializeField] RectTransform itemRoot;
		[SerializeField] RectTransform deleteButton;

		void Awake()
		{
			ScreenManager.Header.Show("Home");
		}

		void Start()
		{
			toggle.onClick.AddListener(() =>
			{
				var formGameObject = form.gameObject;
				formGameObject.SetActive(!formGameObject.activeSelf);

				scrollRect.sizeDelta = formGameObject.activeSelf
					? new Vector2(0, (transform as RectTransform)!.rect.height - 50 - form.RectTransform.rect.height)
					: new Vector2(0, (transform as RectTransform)!.rect.height - 50);
			});
			form.Setup(this);
			deleteButton.GetComponent<Button>().onClick.AddListener(() =>
			{
				var childCount = itemRoot.childCount;
				deleteButton.gameObject.SetActive(childCount != 2);
				Destroy(itemRoot.GetChild(childCount - 2).gameObject);
			});
		}

		public void AddItem(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return;
			}

			Instantiate(itemPrefab, itemRoot, false).Setup(text);
			deleteButton.gameObject.SetActive(true);
			deleteButton.SetSiblingIndex(itemRoot.childCount - 1);
			LayoutRebuilder.ForceRebuildLayoutImmediate(itemRoot);
		}
	}
}