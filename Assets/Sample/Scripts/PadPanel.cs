#if ENABLE_AUTOTAP
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
	/// <summary>
	/// A row of buttons that count what is submitted. Navigation between them is the automatic one of
	/// <see cref="Selectable"/>, so they have to be laid out in a row.
	/// </summary>
	public class PadPanel : MonoBehaviour
	{
		[SerializeField] Button[] buttons;
		[SerializeField] Text label;

		/// <summary>
		/// The buttons from left to right.
		/// </summary>
		public IReadOnlyList<Button> Buttons => buttons;

		/// <summary>
		/// How many times a button was submitted.
		/// </summary>
		public int SubmitCount { get; private set; }

		/// <summary>
		/// The index of the button that was submitted last, or -1 if none was.
		/// </summary>
		public int LastSubmitted { get; private set; } = -1;

		void Awake()
		{
			for (var i = 0; i < buttons.Length; ++i)
			{
				var index = i;
				buttons[i].onClick.AddListener(() =>
				{
					++SubmitCount;
					LastSubmitted = index;
					if (label != null)
					{
						label.text = $"Submitted: {index}";
					}
				});
			}
		}
	}
}
#endif
