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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace ByteDance.PICO.Debugger
{
    public class PXR_InspectorManager : MonoBehaviour
    {
        public static PXR_InspectorManager Instance;
        private void Awake(){
            if(Instance == null){
                Instance = this;
            }
        }
        public GameObject inspectItem;
        public Transform content;
        public GameObject transformInfoNode;
        private void ClearAllChildren(){
            int childCount = content.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(content.GetChild(i).gameObject);
            }
        }
        public void CreateInspector()
        {
            GenerateInspectorTree();
        }
        private void Start() {
            CreateInspector();
        }
        public void Reset(){
            for (int i = 0; i < content.childCount; i++)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content.GetChild(i).GetComponent<RectTransform>());
            }
        }
        public void Refresh(){
            ClearAllChildren();
            GenerateInspectorTree();
            PXR_InspectorController.Instance.RefreshTarget();
        } 
        private void GenerateInspectorTree(){
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            // Iterate over all root GameObjects
            foreach (GameObject obj in rootObjects)
            {
                if(!obj.TryGetComponent<PXR_UIController>(out _) && !obj.TryGetComponent<PXR_UIManager>(out _)){
                    var go = Instantiate(inspectItem, content);
                    if(go.TryGetComponent(out PXR_InspectorItem item)){
                        item.Init(obj.transform);
                    }
                }
            }
            Reset();       
        }
    }
}
#endif