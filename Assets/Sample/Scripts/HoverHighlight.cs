#if ENABLE_AUTOTAP
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sample
{
	/// <summary>
	/// Changes the color of a graphic while the pointer is over it, and tells whether it is.
	/// </summary>
	public class HoverHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		[SerializeField] Graphic graphic;
		[SerializeField] Color normalColor = Color.white;
		[SerializeField] Color hoveredColor = Color.yellow;

		/// <summary>
		/// <c>true</c> while a pointer is over this object.
		/// </summary>
		public bool Hovered { get; private set; }

		void Reset()
		{
			graphic = GetComponent<Graphic>();
		}

		void OnEnable()
		{
			Apply(false);
		}

		void OnDisable()
		{
			Hovered = false;
		}

		/// <inheritdoc/>
		public void OnPointerEnter(PointerEventData eventData)
		{
			Apply(true);
		}

		/// <inheritdoc/>
		public void OnPointerExit(PointerEventData eventData)
		{
			Apply(false);
		}

		void Apply(bool hovered)
		{
			Hovered = hovered;
			if (graphic != null)
			{
				graphic.color = hovered ? hoveredColor : normalColor;
			}
		}
	}
}
#endif
