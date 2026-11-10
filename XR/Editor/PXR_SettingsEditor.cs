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
#if !ENABLE_PICO_OPENXR_SDK
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace ByteDance.PICO.XR.Editor
{
    [CustomEditor(typeof(PXR_Settings))]
    public class PXR_SettingsEditor : UnityEditor.Editor
    {
        private const string StereoRenderingModeAndroid = "stereoRenderingModeAndroid";
        private const string AppMode = "appMode";
        private const string SystemDisplayFrequency = "systemDisplayFrequency";
        private const string OptimizeBufferDiscards = "optimizeBufferDiscards";
        private const string ReprojectionMode = "reprojectionMode";
        private const string SystemSplashScreen = "systemSplashScreen";
        private const float ReprojectionToggleSize = 16.0f;
        private const float ReprojectionToggleSpacing = 4.0f;
        public const string PtwEnabledInfoMessage =
            "When enabled, application depth is captured and submitted automatically.";

        static GUIContent guiStereoRenderingMode = EditorGUIUtility.TrTextContent("Stereo Rendering Mode");
        static GUIContent guiDisplayFrequency = EditorGUIUtility.TrTextContent("Display Refresh Rates");
        private static GUIContent guiOptimizeBuffer = EditorGUIUtility.TrTextContent("Optimize Buffer Discards(Vulkan)");
        static GUIContent guiSystemSplashScreen = EditorGUIUtility.TrTextContent("System Splash Screen");
        static GUIContent guiAppMode = EditorGUIUtility.TrTextContent("App Mode");
        private static readonly GUIContent GuiPositionalTimeWarp =
            EditorGUIUtility.TrTextContent("Positional TimeWarp (Recommended)");
        private static readonly GUIContent GuiApplicationSpaceWarp =
            EditorGUIUtility.TrTextContent("Application SpaceWarp (Advanced)");

        private SerializedProperty appMode;
        private SerializedProperty stereoRenderingModeAndroid;
        private SerializedProperty systemDisplayFrequency;
        private SerializedProperty optimizeBufferDiscards;
        private SerializedProperty reprojectionMode;
        private SerializedProperty appLog;
        private SerializedProperty systemSplashScreen;
        
        
        private static PXR_EditorStyles _styles;
        private static bool hasSpatialAdapterAllInstalled = true;
        private static bool hasSpatialAdapterAllUninstalled = true;

        public readonly struct ReprojectionToggleLayout
        {
            public ReprojectionToggleLayout(Rect labelRect, Rect toggleRect, float height)
            {
                LabelRect = labelRect;
                ToggleRect = toggleRect;
                Height = height;
            }

            public Rect LabelRect { get; }
            public Rect ToggleRect { get; }
            public float Height { get; }
        }

        public readonly struct ReprojectionInspectorState
        {
            public ReprojectionInspectorState(
                PxrReprojectionMode reprojectionMode,
                bool optimizeBufferDiscards,
                bool disableAppSpaceWarp,
                bool disableOptimizeBufferDiscards)
            {
                ReprojectionMode = reprojectionMode;
                OptimizeBufferDiscards = optimizeBufferDiscards;
                DisableAppSpaceWarp = disableAppSpaceWarp;
                DisableOptimizeBufferDiscards = disableOptimizeBufferDiscards;
            }

            public PxrReprojectionMode ReprojectionMode { get; }
            public bool OptimizeBufferDiscards { get; }
            public bool DisableAppSpaceWarp { get; }
            public bool DisableOptimizeBufferDiscards { get; }
        }

        public static ReprojectionToggleLayout CalculateReprojectionToggleLayout(
            Rect rowRect,
            float preferredLabelWidth,
            float labelHeight)
        {
            float availableLabelWidth =
                Mathf.Max(1.0f, rowRect.width - ReprojectionToggleSize - ReprojectionToggleSpacing);
            float labelWidth = Mathf.Min(preferredLabelWidth, availableLabelWidth);
            float height = Mathf.Max(rowRect.height, labelHeight);
            var labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, height);
            var toggleRect = new Rect(
                labelRect.xMax + ReprojectionToggleSpacing,
                rowRect.y + (rowRect.height - ReprojectionToggleSize) * 0.5f,
                ReprojectionToggleSize,
                ReprojectionToggleSize);

            return new ReprojectionToggleLayout(labelRect, toggleRect, height);
        }

        public static ReprojectionInspectorState GetReprojectionInspectorState(
            bool usesVulkan,
            bool appSpaceWarpUnavailable,
            PxrReprojectionMode reprojectionMode,
            bool optimizeBufferDiscards)
        {
            bool disableAppSpaceWarp =
                reprojectionMode == PxrReprojectionMode.PTW || appSpaceWarpUnavailable;
            bool disableOptimizeBufferDiscards =
                usesVulkan && reprojectionMode == PxrReprojectionMode.PTW;

            return new ReprojectionInspectorState(
                reprojectionMode,
                optimizeBufferDiscards,
                disableAppSpaceWarp,
                disableOptimizeBufferDiscards);
        }

        public static ReprojectionInspectorState GetReprojectionInspectorStateAfterPtwToggle(
            bool usesVulkan,
            bool appSpaceWarpUnavailable,
            PxrReprojectionMode currentMode,
            bool requestedPtw,
            bool optimizeBufferDiscards)
        {
            PxrReprojectionMode nextMode = requestedPtw
                ? PxrReprojectionMode.PTW
                : currentMode == PxrReprojectionMode.PTW
                    ? PxrReprojectionMode.None
                    : currentMode;
            bool nextOptimizeBufferDiscards =
                requestedPtw && usesVulkan
                    ? false
                    : optimizeBufferDiscards;

            return GetReprojectionInspectorState(
                usesVulkan,
                appSpaceWarpUnavailable,
                nextMode,
                nextOptimizeBufferDiscards);
        }

        private static bool DrawReprojectionToggle(GUIContent label, bool value)
        {
            GUIStyle labelStyle = EditorStyles.wordWrappedLabel;
            Rect rowRect =
                EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            float preferredLabelWidth = Mathf.Ceil(labelStyle.CalcSize(label).x);
            float availableLabelWidth = Mathf.Max(
                1.0f,
                rowRect.width - ReprojectionToggleSize - ReprojectionToggleSpacing);
            float labelWidth = Mathf.Min(preferredLabelWidth, availableLabelWidth);
            float labelHeight = Mathf.Ceil(labelStyle.CalcHeight(label, labelWidth));
            ReprojectionToggleLayout layout = CalculateReprojectionToggleLayout(
                rowRect,
                preferredLabelWidth,
                labelHeight);

            EditorGUI.LabelField(layout.LabelRect, label, labelStyle);
            bool result = EditorGUI.Toggle(layout.ToggleRect, value);

            float additionalHeight = layout.Height - rowRect.height;
            if (additionalHeight > 0.0f)
            {
                GUILayout.Space(additionalHeight);
            }

            return result;
        }

        void OnEnable()
        {
            if (appMode == null)
                appMode = serializedObject.FindProperty(AppMode);
            if (stereoRenderingModeAndroid == null)
                stereoRenderingModeAndroid = serializedObject.FindProperty(StereoRenderingModeAndroid);
            if (systemDisplayFrequency == null)
                systemDisplayFrequency = serializedObject.FindProperty(SystemDisplayFrequency);
            if (optimizeBufferDiscards == null)
                optimizeBufferDiscards = serializedObject.FindProperty(OptimizeBufferDiscards);
            if (reprojectionMode == null)
                reprojectionMode = serializedObject.FindProperty(ReprojectionMode);
            if (systemSplashScreen == null)
                systemSplashScreen = serializedObject.FindProperty(SystemSplashScreen);
            
            
            _styles ??= new PXR_EditorStyles();
        }

        public override void OnInspectorGUI()
        {
            if (serializedObject == null || serializedObject.targetObject == null)
                return;

            serializedObject.Update();
            EditorGUIUtility.labelWidth = 200.0f;
            BuildTargetGroup selectedBuildTargetGroup = EditorGUILayout.BeginBuildTargetSelectionGrouping();
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("PICO settings cannot be changed when the editor is in play mode.", MessageType.Info);
                EditorGUILayout.Space();
            }
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlayingOrWillChangePlaymode);
            if (selectedBuildTargetGroup == BuildTargetGroup.Android)
            {
                PXR_ProjectSetting projectConfig = PXR_ProjectSetting.GetProjectConfig();

                EditorGUILayout.PropertyField(appMode, guiAppMode);
                // GetProjectConfig and SDK symbols must see the newly selected mode.
                serializedObject.ApplyModifiedProperties();
                PXR_Settings.AppMode currentMode = (PXR_Settings.AppMode)appMode.enumValueIndex;
                bool appModeChanged = projectConfig.isSpatialAdapter != (currentMode == PXR_Settings.AppMode.Spatial);
                EditorGUILayout.Space(10);

                if (PXR_Settings.AppMode.XR == currentMode)
                {
                    projectConfig.isSpatialAdapter = false;
                    EditorGUILayout.PropertyField(stereoRenderingModeAndroid, guiStereoRenderingMode);
                    EditorGUILayout.PropertyField(systemDisplayFrequency, guiDisplayFrequency);

                    bool aswDisabled = false;
#if !UNITY_2021_1_OR_NEWER
                    aswDisabled = true;
#endif
                    GraphicsDeviceType[] graphicsApis =
                        PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget);
                    bool supportsAppSpaceWarp =
                        PXR_Settings.IsAppSpaceWarpGraphicsApiSupported(graphicsApis);
                    bool usesVulkan =
                        graphicsApis.Length > 0 && graphicsApis[0] == GraphicsDeviceType.Vulkan;
                    bool usesMultiview =
                        stereoRenderingModeAndroid.intValue ==
                        (int)PXR_Settings.StereoRenderingModeAndroid.Multiview;
                    bool appSpaceWarpUnavailable =
                        !supportsAppSpaceWarp || aswDisabled || !usesMultiview;
                    PxrReprojectionMode currentReprojectionMode =
                        (PxrReprojectionMode)reprojectionMode.intValue;
                    ReprojectionInspectorState inspectorState =
                        GetReprojectionInspectorState(
                            usesVulkan,
                            appSpaceWarpUnavailable,
                            currentReprojectionMode,
                            optimizeBufferDiscards.boolValue);

                    EditorGUI.BeginDisabledGroup(currentReprojectionMode == PxrReprojectionMode.AppSW);
                    bool ptwEnabled = inspectorState.ReprojectionMode == PxrReprojectionMode.PTW;
                    bool requestedPtw = DrawReprojectionToggle(
                        GuiPositionalTimeWarp,
                        ptwEnabled);
                    EditorGUI.EndDisabledGroup();
                    if (requestedPtw != ptwEnabled)
                    {
                        inspectorState = GetReprojectionInspectorStateAfterPtwToggle(
                            usesVulkan,
                            appSpaceWarpUnavailable,
                            inspectorState.ReprojectionMode,
                            requestedPtw,
                            inspectorState.OptimizeBufferDiscards);
                        currentReprojectionMode = inspectorState.ReprojectionMode;
                        reprojectionMode.intValue = (int)currentReprojectionMode;
                        optimizeBufferDiscards.boolValue = inspectorState.OptimizeBufferDiscards;
                    }

                    if (currentReprojectionMode == PxrReprojectionMode.PTW)
                    {
                        EditorGUILayout.HelpBox(
                            PtwEnabledInfoMessage,
                            MessageType.Info);
                    }

                    EditorGUI.BeginDisabledGroup(
                        inspectorState.DisableAppSpaceWarp);
                    bool appSpaceWarpEnabled = currentReprojectionMode == PxrReprojectionMode.AppSW;
                    bool requestedAppSpaceWarp = DrawReprojectionToggle(
                        GuiApplicationSpaceWarp,
                        appSpaceWarpEnabled);
                    EditorGUI.EndDisabledGroup();
                    if (requestedAppSpaceWarp != appSpaceWarpEnabled)
                    {
                        currentReprojectionMode = requestedAppSpaceWarp
                            ? PxrReprojectionMode.AppSW
                            : PxrReprojectionMode.None;
                        reprojectionMode.intValue = (int)currentReprojectionMode;
                        inspectorState = GetReprojectionInspectorState(
                            usesVulkan,
                            appSpaceWarpUnavailable,
                            currentReprojectionMode,
                            optimizeBufferDiscards.boolValue);
                    }

                    if (currentReprojectionMode == PxrReprojectionMode.AppSW &&
                        appSpaceWarpUnavailable)
                    {
                        string message = !supportsAppSpaceWarp
                            ? "Application SpaceWarp requires Vulkan."
                            : aswDisabled
                                ? "Application SpaceWarp requires Unity 2021.1 or later."
                                : "Application SpaceWarp requires Multiview.";
                        EditorGUILayout.HelpBox(message, MessageType.Error);
                    }

                    EditorGUI.BeginDisabledGroup(inspectorState.DisableOptimizeBufferDiscards);
                    EditorGUILayout.PropertyField(optimizeBufferDiscards, guiOptimizeBuffer);
                    EditorGUI.EndDisabledGroup();
                    if (inspectorState.DisableOptimizeBufferDiscards && optimizeBufferDiscards.boolValue)
                    {
                        EditorGUILayout.HelpBox(
                            "Positional TimeWarp requires Optimize Buffer Discards to be disabled.",
                            MessageType.Warning);
                    }

                    EditorGUILayout.PropertyField(systemSplashScreen, guiSystemSplashScreen);
                }
                else
                {
                    projectConfig.isSpatialAdapter = true;
                    string note = "Note: If the packages don't install correctly, you'll need to manually add the 3 packages in this order:";
                    EditorGUILayout.LabelField(note);
                    GUILayout.Space(2);

                    for (int i = 0; i < PXR_Utils.spatialAdapterPackagesLocalPath.Length; i++)
                    {
                        EditorConfigurations(i);
                    }
                }


#if AR_FOUNDATION_5 || AR_FOUNDATION_6
                GUILayout.Space(2);

                var guiContent = new GUIContent();
                guiContent.text = "AR Foundation";
                projectConfig.arFoundation = EditorGUILayout.Toggle(guiContent, projectConfig.arFoundation);
                if (projectConfig.arFoundation)
                {
                    EditorGUI.indentLevel++;
                    // body tracking
                    guiContent.text = "Body Tracking";
                    projectConfig.bodyTracking = EditorGUILayout.Toggle(guiContent, projectConfig.bodyTracking);

                    // face tracking
                    guiContent.text = "Face Tracking";
                    projectConfig.faceTracking = EditorGUILayout.Toggle(guiContent, projectConfig.faceTracking);

                    // anchor
                    guiContent.text = "Anchor";
                    projectConfig.spatialAnchor = EditorGUILayout.Toggle(guiContent, projectConfig.spatialAnchor);

                    // mesh
                    guiContent.text = "Meshing";
                    projectConfig.spatialMesh = EditorGUILayout.Toggle(guiContent, projectConfig.spatialMesh);
                    
                    guiContent.text = "Plane Detection";
                    projectConfig.planeDetection = EditorGUILayout.Toggle(guiContent, projectConfig.planeDetection);

                    guiContent.text = "Image Tracking";
                    projectConfig.imageTracking = EditorGUILayout.Toggle(guiContent, projectConfig.imageTracking);

#if ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK
                    List<Component> components = FindComponentsInScene<Component>()
                        .Where(component => IsEnabledARCameraManagerOnMainCamera(component))
                        .ToList();
                    bool cameraEffect = false;
                    for (int i = 0; i < components.Count; i++)
                    {
                        Component arCamera = components[i];
                        if (arCamera.gameObject.GetComponent<PXR_ARCameraEffectManager>())
                        {
                            cameraEffect = true;
                        }
                        Camera camera = arCamera.gameObject.GetComponent<Camera>();
                        if (camera)
                        {
                            camera.clearFlags = CameraClearFlags.SolidColor;
                            camera.backgroundColor = new Color(0, 0, 0, 0);
                        }
                    }

                    if (!cameraEffect && components.Count > 0)
                    {
                        Component arCamera = components[0];
                        if (!arCamera.gameObject.GetComponent<PXR_ARCameraEffectManager>())
                        {
                            arCamera.gameObject.AddComponent<PXR_ARCameraEffectManager>();
                        }
                        cameraEffect = true;
                    }
#endif
                    EditorGUI.indentLevel--;
                }
#endif
                if (GUI.changed || appModeChanged)
                {
                    EditorUtility.SetDirty(projectConfig);
                    AssetDatabase.SaveAssetIfDirty(projectConfig);
                }
                if (PXR_Utils.UpdateSDKSymbols())
                {
                    if (projectConfig.isSpatialAdapter)
                    {
                        if (!hasSpatialAdapterAllInstalled)
                        {
                            int result = EditorUtility.DisplayDialogComplex(PXR_Utils.appModelCheckInstallMSDialogTitle, PXR_Utils.appModelCheckInstallMSDialogMessage, "OK", "Cancel", "");
                            if (result == 0)
                            {
                                EditorApplication.update += PXR_Utils.CheckAndLoadNextPackage;
                            }
                        }
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndBuildTargetSelectionGrouping();

            serializedObject.ApplyModifiedProperties();
            EditorGUIUtility.labelWidth = 0f;
        }

        void EditorConfigurations(int i)
        {
            string packageSource = PXR_Utils.spatialAdapterPackagesLocalPath[i];
            string packageName = PXR_Utils.spatialAdapterPackagesName[i];
            string packageDisplayName = i < PXR_Utils.spatialAdapterPackagesDisplayName.Length
                ? PXR_Utils.spatialAdapterPackagesDisplayName[i]
                : packageName;
            bool enable = PXR_Utils.IsLocalPackageInstalled(packageName);
            string strinfo = enable ?
                $"{i + 1}. Installed: {packageDisplayName}" :
                $"{i + 1}. Required: Please install {packageDisplayName}";
            string tooltip = $"Source: {packageSource}";

            EditorGUILayout.BeginHorizontal();

            var iconStyle = new GUIStyle
            {
                fixedWidth = 20,
                fixedHeight = 20,
                alignment = TextAnchor.MiddleCenter,
            };
            if (enable)
            {
                GUI.color = Color.green;
                GUILayout.Label(EditorGUIUtility.IconContent("FilterSelectedOnly"), iconStyle);
                hasSpatialAdapterAllUninstalled = false;
            }
            else
            {
                GUI.color = Color.yellow;
                GUILayout.Label(EditorGUIUtility.IconContent("console.warnicon"), iconStyle);
                hasSpatialAdapterAllInstalled = false;
            }
            GUI.color = Color.white;
            GUIStyle titleStyle = new GUIStyle()
            {
                fontStyle = FontStyle.Normal,
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = new GUIStyleState() { textColor = Color.white },
                wordWrap = true,
                stretchWidth = true
            };

            GUILayout.Label(new GUIContent(strinfo, tooltip), titleStyle, GUILayout.ExpandWidth(true));

            titleStyle.alignment = TextAnchor.MiddleCenter;
            if (enable)
            {
                titleStyle.normal.textColor = Color.green;
                GUILayout.Label("Installed", titleStyle,
                    GUILayout.Width(70),
                    GUILayout.ExpandWidth(false));
            }
            else
            {
                if (GUILayout.Button(new GUIContent("Install", tooltip),
                    GUILayout.Width(60),
                    GUILayout.Height(20),
                    GUILayout.ExpandWidth(false)))
                {
                    PXR_Utils.LoadLocalPackage(packageName, packageSource);
                }

                var buttonRectToApply = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.Repaint)
                {
                    EditorGUIUtility.AddCursorRect(buttonRectToApply, MouseCursor.Link);
                }
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        private static bool IsEnabledARCameraManagerOnMainCamera(Component component)
        {
            return component != null
                && component.GetType().FullName == "UnityEngine.XR.ARFoundation.ARCameraManager"
                && component is Behaviour behaviour
                && behaviour.enabled
                && component.gameObject.CompareTag("MainCamera");
        }
        public static List<T> FindComponentsInScene<T>() where T : Component
        {
            var activeScene = SceneManager.GetActiveScene();
            var foundComponents = new List<T>();

            var rootObjects = activeScene.GetRootGameObjects();
            foreach (var rootObject in rootObjects)
            {
                var components = rootObject.GetComponentsInChildren<T>(true);
                foundComponents.AddRange(components);
            }

            return foundComponents;
        }
        
    }
}
#endif
