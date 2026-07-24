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
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using ByteDance.PICO.XR.Editor;
using UnityEngine.InputSystem;
using System.IO;


namespace ByteDance.PICO.Debugger
{
    // Generate a setting item in the editor project settings screen
    static class SettingToolEditor
    {
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            var config = PXR_PicoDebuggerSO.Instance;

            var provider = new SettingsProvider("Project/PICO Debugger", SettingsScope.Project)
            {
                label = "PICO Debugger",
                activateHandler = (obj, rootElement) =>
                {
                    var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{PXR_Utils.sdkPackageName}{PXR_DebuggerConst.uiPath}{PXR_DebuggerConst.debuggerXMLName}");
                    var rootVisualElement = visualTree.Instantiate();
                    rootElement.Add(rootVisualElement);

                    var isOpenToggle = rootVisualElement.Q<Toggle>("IsOpen");
                    var inputActionAsset = rootVisualElement.Q<ObjectField>("InputActionAsset");
                    var startPositionDropdown = rootVisualElement.Q<DropdownField>("StartPosition");
                    var maxInfoCountSlider = rootVisualElement.Q<SliderInt>("MaxInfoCount");

                    var worldPositionStepSlider = rootVisualElement.Q<Slider>("WorldPositionStep");
                    var localPositionStepSlider = rootVisualElement.Q<Slider>("LocalPositionStep");
                    var worldRotationStepSlider = rootVisualElement.Q<Slider>("WorldRotationStep");
                    var localRotationStepSlider = rootVisualElement.Q<Slider>("LocalRotationStep");

                    Debug.Assert(isOpenToggle != null, $"{isOpenToggle} is Null");
                    Debug.Assert(inputActionAsset != null, $"{inputActionAsset} is Null");
                    Debug.Assert(startPositionDropdown != null, $"{startPositionDropdown} is Null");
                    Debug.Assert(maxInfoCountSlider != null, $"{maxInfoCountSlider} is Null");
                    Debug.Assert(worldPositionStepSlider != null, $"{worldPositionStepSlider} is Null");
                    Debug.Assert(localPositionStepSlider != null, $"{localPositionStepSlider} is Null");
                    Debug.Assert(worldRotationStepSlider != null, $"{worldRotationStepSlider} is Null");
                    Debug.Assert(localRotationStepSlider != null, $"{localRotationStepSlider} is Null");

                    isOpenToggle.value = config.isOpen;
                    isOpenToggle.RegisterValueChangedCallback(evt =>
                    {
                        config.isOpen = evt.newValue;
                        EditorUtility.SetDirty(config); // Mark as dirty to save the changes
                        if (config.isOpen)
                        {
                            PXR_AppLog.PXR_OnEvent($"{PXR_AppLog.strPICODebugger}", PXR_AppLog.strPICODebugger_Enable, "enable");
                            if (!Directory.Exists("Assets/TextMesh Pro"))
                            {
                                bool userConfirmed = EditorUtility.DisplayDialog(
                                    "Import TextMesh Pro",                  // dialog title
                                    "The PICO Debugger depends on TextMesh Pro. Should TextMesh Pro be imported to ensure the normal operation of the debugger function?", // dialog content
                                    "Yes",                             // confirm button text
                                    "Cancel"                              // cancel button text
                                );
                                if (userConfirmed)
                                {
                                    // For Unity 2022+ and unity6+
                                    var path = "Window/TextMeshPro/Import TMP Essential Resources";
                                    try
                                    {
                                        EditorApplication.ExecuteMenuItem(path);
                                    }
                                    catch
                                    {
                                        Debug.LogError($"Failed to import TextMesh Pro Essential Resources. Please import them manually via {path}.");
                                    }
                                }
                                else
                                {
                                    Debug.Log("User canceled the import of TextMesh Pro Essential Resources.");
                                    isOpenToggle.value = false;
                                    config.isOpen = false;
                                    EditorUtility.SetDirty(config);
                                }
                            }
                        }
                    });

                    var inputActionPath = $"{PXR_Utils.sdkPackageName}{PXR_DebuggerConst.debuggerPath}{PXR_DebuggerConst.inputActionName}";
                    if (!string.IsNullOrEmpty(inputActionPath))
                    {
                        var loadedAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputActionPath);
                        // If the loading is successful, assign it to ObjectField
                        if (loadedAsset != null)
                        {
                            inputActionAsset.value = loadedAsset;
                            config.inputActionAsset = loadedAsset;
                        }
                        else
                        {
                            Debug.LogWarning($"The corresponding resource file cannot be found under the path {inputActionPath}.");
                        }

                    }

                    startPositionDropdown.choices = Enum.GetNames(typeof(StartPosiion)).ToList();
                    startPositionDropdown.index = (int)config.startPosition;
                    startPositionDropdown.RegisterValueChangedCallback(evt =>
                    {
                        config.startPosition = (StartPosiion)Enum.Parse(typeof(StartPosiion), evt.newValue);
                        EditorUtility.SetDirty(config);
                    });

                    maxInfoCountSlider.value = config.maxInfoCount;
                    maxInfoCountSlider.RegisterValueChangedCallback(evt =>
                    {
                        config.maxInfoCount = Mathf.RoundToInt(evt.newValue);
                        EditorUtility.SetDirty(config);
                    });

                    worldPositionStepSlider.value = config.worldPositionStep;
                    worldPositionStepSlider.RegisterValueChangedCallback(evt =>
                    {
                        config.worldPositionStep = evt.newValue;
                        EditorUtility.SetDirty(config);
                    });

                    localPositionStepSlider.value = config.localPositionStep;
                    localPositionStepSlider.RegisterValueChangedCallback(evt =>
                    {
                        config.localPositionStep = evt.newValue;
                        EditorUtility.SetDirty(config);
                    });

                    worldRotationStepSlider.value = config.worldRotationStep;
                    worldRotationStepSlider.RegisterValueChangedCallback(evt =>
                    {
                        config.worldRotationStep = evt.newValue;
                        EditorUtility.SetDirty(config);
                    });

                    localRotationStepSlider.value = config.localRotationStep;
                    localRotationStepSlider.RegisterValueChangedCallback(evt =>
                    {
                        config.localRotationStep = evt.newValue;
                        EditorUtility.SetDirty(config);
                    });

                    AssetDatabase.Refresh();
                },
                keywords = new HashSet<string>(new[] { "PICO", "Debugger Tool" })
            };
            return provider;
        }
    }
}
#endif