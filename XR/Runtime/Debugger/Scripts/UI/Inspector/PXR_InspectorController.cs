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
using System;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace ByteDance.PICO.Debugger
{
    public class PXR_InspectorController : MonoBehaviour
    {
        public static PXR_InspectorController Instance;
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }
        private GameObject go;
        public GameObject panel;

        public TextMeshProUGUI targetName;
        public TextMeshProUGUI worldPositionX;
        public TextMeshProUGUI worldPositionY;
        public TextMeshProUGUI worldPositionZ;

        public TextMeshProUGUI localPositionX;
        public TextMeshProUGUI localPositionY;
        public TextMeshProUGUI localPositionZ;

        public TextMeshProUGUI worldEulerX;
        public TextMeshProUGUI worldEulerY;
        public TextMeshProUGUI worldEulerZ;

        public TextMeshProUGUI localEulerX;
        public TextMeshProUGUI localEulerY;
        public TextMeshProUGUI localEulerZ;

        public TextMeshProUGUI worldQuaternion;

        public TextMeshProUGUI localQuaternion;

        public Toggle toggle;

        private PXR_PicoDebuggerSO config;
        private bool isShowPanel = false;

        void Start()
        {
            config = Resources.Load<PXR_PicoDebuggerSO>("PXR_PicoDebuggerSO");
            TogglePanel(false);
        }
        void Update()
        {
            if (isShowPanel && go == null)
            {
                TogglePanel(false);
                PXR_InspectorManager.Instance.Refresh();
            }
            if (go != null && go.transform.hasChanged)
            {
                UpdatePanel();
                go.transform.hasChanged = false;
            }
        }
        private void TogglePanel(bool state)
        {
            isShowPanel = state;
            panel.SetActive(state);
        }
        public void SetGameObject(GameObject obj)
        {
            go = obj;
            targetName.text = $"{go.name}";
            toggle.isOn = go.activeSelf;
            TogglePanel(true);
            UpdatePanel();
        }
        public void RefreshTarget()
        {
            targetName.text = $"{go.name}";
            toggle.isOn = go.activeSelf;
            TogglePanel(true);
            UpdatePanel();
        }
        public void ToggleGO(Toggle toggle)
        {
            if (go != null)
            {
                go.SetActive(toggle.isOn);
            }
        }
        public void ChangeWorldPositionX(float v)
        {
            go.transform.position += config.worldPositionStep * v * Vector3.right;
        }
        public void ChangeWorldPositionY(float v)
        {
            go.transform.position += config.worldPositionStep * v * Vector3.up;
        }
        public void ChangeWorldPositionZ(float v)
        {
            go.transform.position += config.worldPositionStep * v * Vector3.forward;
        }
        public void ChangeLocalPositionX(float v)
        {
            go.transform.localPosition += config.localPositionStep * v * Vector3.right;
        }
        public void ChangeLocalPositionY(float v)
        {
            go.transform.localPosition += config.localPositionStep * v * Vector3.up;
        }
        public void ChangeLocalPositionZ(float v)
        {
            go.transform.localPosition += config.localPositionStep * v * Vector3.forward;
        }
        public void ChangeWorldEulerAngleX(float v)
        {
            go.transform.eulerAngles += config.worldRotationStep * v * Vector3.right;
        }
        public void ChangeWorldEulerAngleY(float v)
        {
            go.transform.eulerAngles += config.worldRotationStep * v * Vector3.up;
        }
        public void ChangeWorldEulerAngleZ(float v)
        {
            go.transform.eulerAngles += config.worldRotationStep * v * Vector3.forward;
        }
        public void ChangeLocalEulerAngleX(float v)
        {
            go.transform.localEulerAngles += config.localRotationStep * v * Vector3.right;
        }
        public void ChangeLocalEulerAngleY(float v)
        {
            go.transform.localEulerAngles += config.localRotationStep * v * Vector3.up;
        }
        public void ChangeLocalEulerAngleZ(float v)
        {
            go.transform.localEulerAngles += config.localRotationStep * v * Vector3.forward;
        }
        public void UpdatePanel()
        {
            worldPositionX.text = $"{go.transform.position.x}";
            worldPositionY.text = $"{go.transform.position.y}";
            worldPositionZ.text = $"{go.transform.position.z}";

            localPositionX.text = $"{go.transform.localPosition.x}";
            localPositionY.text = $"{go.transform.localPosition.y}";
            localPositionZ.text = $"{go.transform.localPosition.z}";

            worldEulerX.text = $"{go.transform.eulerAngles.x}";
            worldEulerY.text = $"{go.transform.eulerAngles.y}";
            worldEulerZ.text = $"{go.transform.eulerAngles.z}";

            localEulerX.text = $"{go.transform.localEulerAngles.x}";
            localEulerY.text = $"{go.transform.localEulerAngles.y}";
            localEulerZ.text = $"{go.transform.localEulerAngles.z}";

            worldQuaternion.text = $"{go.transform.rotation}";
            localQuaternion.text = $"{go.transform.localRotation}";
        }

    }
}

#endif