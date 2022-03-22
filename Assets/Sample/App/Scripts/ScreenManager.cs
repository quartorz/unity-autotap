using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sample
{
	public class ScreenManager : MonoBehaviour
	{
		public class Shield : IDisposable
		{
			public Shield()
			{
				_instance.eventSystem.enabled = false;
			}

			void IDisposable.Dispose()
			{
				_instance.eventSystem.enabled = true;
			}
		}

		[SerializeField] Transform screenHolder;
		[SerializeField] Header header;
		[SerializeField] EventSystem eventSystem;

		static ScreenManager _instance;

		public static ScreenBase Current { get; private set; }
		public static Header Header => _instance.header;

		void Start()
		{
			_instance = this;
			Current = InstantiateScreen(Resources.Load<TitleScreen>("TitleScreen"));
		}

		public static void Change<TScreen>() where TScreen : ScreenBase
		{
			_instance.StartCoroutine(CoChange<TScreen>());
		}

		public static IEnumerator CoChange<TScreen>() where TScreen : ScreenBase
		{
			using (new Shield())
			{
				var screenName = typeof(TScreen).Name;
				var request = Resources.LoadAsync<TScreen>(screenName);
				while (!request.isDone)
				{
					yield return null;
				}

				if (request.asset == null)
				{
					throw new Exception($"screen {screenName} not found");
				}

				Destroy(Current.gameObject);
				Current = InstantiateScreen(request.asset as TScreen);
			}
		}

		static ScreenBase InstantiateScreen(ScreenBase resource)
		{
			var screen = Instantiate(resource, _instance.screenHolder, false);
			screen.gameObject.SetActive(true);
			return screen;
		}
	}
}