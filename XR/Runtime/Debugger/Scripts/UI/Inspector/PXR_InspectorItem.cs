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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace ByteDance.PICO.Debugger
{
    public class PXR_InspectorItem : MonoBehaviour
    {
        public TextMeshProUGUI text;
        public GameObject folderIcon;
        private bool hasChild = false;
        private string nodeName;
        private GameObject target;
        public GameObject Target => target;
        public Transform group;
        public void SetTitle()
        {
            text.text = nodeName;
        }
        public void Init(Transform item)
        {
            target = item.gameObject;
            nodeName = item.name;
            if (item.childCount > 0)
            {
                hasChild = true;
                folderIcon.SetActive(true);
                TraverseChild(item);
            }
            SetTitle();
        }
        public void AddItem(Transform item)
        {
            var go = Instantiate(PXR_InspectorManager.Instance.inspectItem, group);
            if (go.TryGetComponent(out PXR_InspectorItem inspectItem))
            {
                inspectItem.Init(item);
            }
        }
        public void TraverseChild(Transform current)
        {
            for (int i = 0; i < current.childCount; i++)
            {
                AddItem(current.GetChild(i));
            }
        }
    }
}
#endif