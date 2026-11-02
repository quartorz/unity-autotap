#if ENABLE_AUTOTAP
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sample
{
	/// <summary>
	/// Closes the menu of the header when the cancel action of the UI is performed: Escape of a keyboard, or the east
	/// button of a gamepad. The cancel is delivered to the selected object, so this goes on the object that is selected
	/// while the menu is open, which is the menu button of the header: a click selects it.
	/// </summary>
	public class MenuCancelHandler : MonoBehaviour, ICancelHandler
	{
		[SerializeField] Menu menu;

		/// <inheritdoc/>
		public void OnCancel(BaseEventData eventData)
		{
			// Opened is false while the menu slides in or out.
			if (menu.Opened)
			{
				menu.StartCoroutine(menu.CoClose());
			}
		}
	}
}
#endif
