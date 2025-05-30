using System;
using UnityEngine;
using UnityEngine.PBD;
using UnityEngine.UI;

namespace Reduction.InteractionLeaves.Script.Test
{
    public class FramRateSetToogle : MonoBehaviour
    {
        private Toggle toggle;
        private void Start()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(SetFrameRate);
        }

        void SetFrameRate(bool isOn)
        {
            if (isOn)
            {
                Application.targetFrameRate = 30;
            }
            else
            {
                Application.targetFrameRate = 60;
            }
            
        }
    }
}