using System;
using System.Collections;
using System.Collections.Generic;
using UnityAutoTap;
using UnityEngine;
using UnityEngine.UI;

namespace Sample
{
    public class ConfigEditor : MonoBehaviour
    {
        [SerializeField] Slider slider;
        [SerializeField] Text text;

        Repeat _repeat;

        void Start()
        {
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(value =>
            {
                _repeat.Count = (int)value;
            });
        }

        public void Setup(Repeat repeat)
        {
            _repeat = repeat;
            slider.value = _repeat.Count;
        }

        void Update()
        {
            text.text = $"{_repeat.CurrentCount} / {_repeat.Count}";
        }
    }
}
