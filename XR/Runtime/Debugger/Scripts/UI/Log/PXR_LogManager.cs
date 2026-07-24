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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace ByteDance.PICO.Debugger
{
    public class PXR_LogManager : MonoBehaviour, IPXR_PanelManager
    {
        public List<GameObject> infoList = new();
        public List<GameObject> warningList = new();
        public List<GameObject> errorList = new();
        public PXR_LogMessageController errorMessage;
        public PXR_LogMessageController warningMessage;
        public PXR_LogMessageController infoMessage;
        public PXR_TabBarVisualController errorIcon;
        public PXR_TabBarVisualController warningIcon;
        public PXR_TabBarVisualController infoIcon;
        public Transform messageContainer;
        private int ListCount => infoList.Count + warningList.Count + errorList.Count;
        private void AddMessage(string title, string content, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                    CreateMessage(title, content, type, errorList, errorMessage);
                    errorIcon.AddMessage();
                    break;
                case LogType.Assert:
                    break;
                case LogType.Warning:
                    CreateMessage(title, content, type, warningList, warningMessage);
                    warningIcon.AddMessage();
                    break;
                case LogType.Log:
                    CreateMessage(title, content, type, infoList, infoMessage);
                    infoIcon.AddMessage();
                    break;
                case LogType.Exception:
                    break;
            }
        }
        private void CreateMessage(string title, string content, in LogType type, in List<GameObject> list, in PXR_LogMessageController template)
        {
            var msg = Instantiate(template, messageContainer).GetComponent<PXR_LogMessageController>();
            msg.Init(title, content);
            list.Add(msg.gameObject);
        }
        public void FilterLogs(LogType type, bool isFilter)
        {
            switch (type)
            {
                case LogType.Error:
                    ToggleLogs(errorList, isFilter);
                    break;
                case LogType.Assert:
                    break;
                case LogType.Warning:
                    ToggleLogs(warningList, isFilter);
                    break;
                case LogType.Log:
                    ToggleLogs(infoList, isFilter);
                    break;
                case LogType.Exception:
                    break;
            }
        }
        private void ToggleLogs(in List<GameObject> list, bool status)
        {
            foreach (var item in list)
            {
                item.SetActive(status);
            }
        }
        void Start()
        {
            Application.logMessageReceived += OnLogMessageReceived;
        }
        private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            if (PXR_UIController.Instance.config.maxInfoCount > ListCount)
            {
                AddMessage(logString, stackTrace, type);
                LayoutRebuild();
            }
        }
        public void DeleteAllMessages()
        {
            DeleteMessage(infoList, infoIcon);
            DeleteMessage(warningList, warningIcon);
            DeleteMessage(errorList, errorIcon);
        }
        private void DeleteMessage(in List<GameObject> list,in PXR_TabBarVisualController icon){
            for (var i = list.Count-1; i >=0; i--)
            {
                Destroy(list[i], 0.1f);
                infoList.Remove(list[i]);
            }
            icon.ChangeMessageCount(0);
            if(icon.transform.TryGetComponent(out Toggle toggle)){
                toggle.isOn = true;
            }
        }
        void OnEnable()
        {
            LayoutRebuild();
        }
        private void LayoutRebuild()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(messageContainer.GetComponent<RectTransform>());
            // LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent.GetComponent<RectTransform>());
        }
        public void Init(){
            
        }
        void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
        }
    }
}
#endif