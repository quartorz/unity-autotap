using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
	public class Header : MonoBehaviour
	{
		[SerializeField] Menu menu;
		[SerializeField] Button menuButton;
		[SerializeField] Text text;

		public Menu Menu => menu;

		void Start()
		{
			menuButton.onClick.AddListener(() =>
			{
				StartCoroutine(menu.gameObject.activeSelf ? menu.CoClose() : menu.CoOpen());
			});
		}

		public void Show(string content)
		{
			gameObject.SetActive(true);
			text.text = content;
		}

		public void Hide()
		{
			gameObject.SetActive(false);
			menu.CloseImmediate();
		}
	}
}