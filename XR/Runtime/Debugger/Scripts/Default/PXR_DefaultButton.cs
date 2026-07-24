/*******************************************************************************
Copyright © 2015-2022 PICO Technology Co., Ltd.All rights reserved.  

NOTICE：All information contained herein is, and remains the property of 
PICO Technology Co., Ltd. The intellectual and technical concepts 
contained herein are proprietary to PICO Technology Co., Ltd. and may be 
covered by patents, patents in process, and are protected by trade secret or 
copyright law. Dissemination of this information or reproduction of this 
material is strictly forbidden unless prior written permission is obtained from
PICO Technology Co., Ltd. 
*******************************************************************************/
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace ByteDance.PICO.Debugger
{
    public class PXR_DefaultButton : MonoBehaviour
    {
        public RectTransform panel;
        static readonly List<RectTransform> activePanelList = new();
        private const float panelRotateAngle = 25f;
        private float mainPanelWidth = 0f;
        private float halfW = 0f;
        private float offset = 0f;
        public void TogglePanel(Toggle toggle)
        {
            if (toggle.isOn)
            {
                ShowPanel(panel);
            }
            else
            {
                HidePanel(panel);
            }
        }

        private void ShowPanel(RectTransform panel)
        {
            if (PXR_DefaultButton.activePanelList.Contains(panel)) return;
            PXR_DefaultButton.activePanelList.Add(panel);
            panel.gameObject.SetActive(true);
            RebuildLayout();
        }
        private void HidePanel(RectTransform panel)
        {
            if (PXR_DefaultButton.activePanelList.Contains(panel))
            {
                PXR_DefaultButton.activePanelList.Remove(panel);
                panel.gameObject.SetActive(false);
                RebuildLayout();
            }
        }
        private void RebuildLayout()
        {
            if (activePanelList.Count == 0) return;
            var centerIndex = activePanelList.Count / 2;
            mainPanelWidth = activePanelList[centerIndex].rect.width;
            for (var i = 0; i < activePanelList.Count; i++)
            {
                if (i == centerIndex)
                {
                    activePanelList[i].localPosition = Vector3.zero;
                    activePanelList[i].localEulerAngles = Vector3.zero;
                }
                else
                {
                    halfW = activePanelList[i].rect.width * 0.5f;
                    offset = Mathf.Sin(panelRotateAngle * Mathf.Deg2Rad) * halfW;
                    var sign = Mathf.Sign(i - centerIndex);
                    activePanelList[i].localPosition = new Vector3(sign * (mainPanelWidth * 0.5f + halfW), 0, -offset);
                    activePanelList[i].localEulerAngles = new Vector3(0, sign * panelRotateAngle, 0);
                }
            }
        }
    }
}
#endif