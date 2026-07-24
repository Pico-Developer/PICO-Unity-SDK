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
using TMPro;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace ByteDance.PICO.Debugger
{
    [RequireComponent(typeof(RectTransform))]
    public class PXR_LogMessageController : MonoBehaviour
    {
        // Start is called before the first frame update
        public TextMeshProUGUI title;
        public TextMeshProUGUI content;
        public RectTransform icon;
        private bool isShowAll = false;
        public void Init(string title, string content)
        {
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string finalTitle = $"{timestamp}: {title}";

            this.title.text = finalTitle;
            this.content.text = $"{title}\n{content}";
            LayoutRebuild();
        }
        public void ToggleContent()
        {
            isShowAll = !isShowAll;
            icon.eulerAngles = isShowAll? new Vector3(0,0,180):Vector3.zero;
            content.enableWordWrapping = isShowAll;
            LayoutRebuild();
        }
        private void LayoutRebuild()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent.GetComponent<RectTransform>());
        }
    }
}
#endif