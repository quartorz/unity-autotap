using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
	public class MenuItem : MonoBehaviour
	{
		[SerializeField] Text text;
		[SerializeField] Button button;

		Action _onClick;

		void Start()
		{
			button.onClick.AddListener(() => _onClick?.Invoke());
		}

		public void Setup(string text, Action onClick)
		{
			this.text.text = text;
			this._onClick = onClick;
		}
	}
}