using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
	public class TitleScreen : ScreenBase
	{
		[SerializeField] Button tapToStart;

		void Start()
		{
			ScreenManager.Header.Hide();
			tapToStart.onClick.AddListener(ScreenManager.Change<HomeScreen>);
		}
	}
}