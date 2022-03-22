using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
    public class Form : MonoBehaviour
    {
        [SerializeField] InputField inputField;
        [SerializeField] Button button;

        HomeScreen _home;

        public RectTransform RectTransform =>
            _rectTransform != null ? _rectTransform : (_rectTransform = transform as RectTransform);

        RectTransform _rectTransform;

        void Start()
        {
            button.onClick.AddListener(() =>
            {
                _home.AddItem(inputField.text);
            });
        }

        public void Setup(HomeScreen home)
        {
            _home = home;
        }
    }
}
