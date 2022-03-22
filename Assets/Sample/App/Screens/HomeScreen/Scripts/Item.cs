using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
    public class Item : MonoBehaviour
    {
        [SerializeField] Text text;

        public void Setup(string text)
        {
            this.text.text = text;
        }
    }
}
