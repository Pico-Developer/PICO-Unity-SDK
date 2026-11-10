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

using System;
using System.Collections.Generic;
using System.Xml;
using System.Text;

using UnityEngine;
using UnityEngine.Rendering;
#if AR_FOUNDATION_5 || AR_FOUNDATION_6
using UnityEngine.XR.ARSubsystems;
#endif
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine.XR.Management;
#if UNITY_OPENXR
#if XR_HAND
using UnityEngine.XR.Hands.OpenXR;
#endif
#if ENABLE_PICO_OPENXR_SDK
//using UnityEngine.XR.Hands.OpenXR;
using ByteDance.PICO.OpenXR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
#endif
#endif

namespace ByteDance.PICO.XR.Editor
{
    public class PXR_BuildProcessor : XRBuildHelper<PXR_Settings>
    {
        public override string BuildSettingsKey { get { return "ByteDance.PICO.XR.Settings"; } }
       
        public static bool RequiresSpatialDataPermission(
            PXR_ProjectSetting projectConfig)
        {
            return projectConfig.sceneCapture
                   || projectConfig.spatialMesh
                   || projectConfig.planeDetection
                   || projectConfig.environmentDepth
                   || projectConfig.lightEstimation
                   || projectConfig.objectTracking
                   || projectConfig.keyboardPassthrough
                   || projectConfig.imageTracking;
        }

        public static bool IsLoaderExists(bool isPlatform = false)
        {
            XRGeneralSettings generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings == null) return false;
            var assignedSettings = generalSettings.AssignedSettings;
            if (assignedSettings == null) return false;
            
             bool isPxrOpenXRFeatureEnabled = false;
             bool isPxrOpenXRExtensionsEnabled = false;
#if UNITY_2021_1_OR_NEWER
            foreach (XRLoader loader in assignedSettings.activeLoaders)
            {
                if (loader is PXR_Loader) return true;
#if ENABLE_PICO_OPENXR_SDK
                if (loader is OpenXRLoader)
                {
                    var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                    foreach (var feature in settings.GetFeatures<OpenXRFeature>())
                    {
                        if (feature is PICOFeature)
                        {
                            isPxrOpenXRFeatureEnabled = feature.enabled;
                            if (!isPlatform)
                            {
                                return isPxrOpenXRFeatureEnabled;
                            }
                        }

                        if (feature is OpenXRExtensions)
                        {
                            isPxrOpenXRExtensionsEnabled = feature.enabled;
                            if (isPlatform)
                            {
                                return isPxrOpenXRExtensionsEnabled;
                            }
                        }
                    }
                }
#endif
            }
#else
            foreach (XRLoader loader in assignedSettings.loaders)
            {
                if (loader is PXR_Loader) return true;
#if ENABLE_PICO_OPENXR_SDK
                if (loader is OpenXRLoader)
                {
                    Debug.Log("PXRLog [Build Check]OpenXR is enabled");
                    var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                    foreach (var feature in settings.GetFeatures<OpenXRFeature>())
                    {
                        if (feature is PXR_OpenXRFeature)
                        {
                            return true;
                        }
                        if (feature is PXR_OpenXRFeature)
                        {
                            isPxrOpenXRFeatureEnabled = feature.enabled;
                            if (!isPlatform)
                            {
                                return isPxrOpenXRFeatureEnabled;
                            }
                        }

                        if (feature is PXR_OpenXRExtensions)
                        {
                            isPxrOpenXRExtensionsEnabled = feature.enabled;
                            if (isPlatform)
                            {
                                return isPxrOpenXRExtensionsEnabled;
                            }
                        }
                    }
                }
#endif
            }
#endif

            return false;
        }
        private PXR_Settings PxrSettings
        {
            get
            {
                return SettingsForBuildTargetGroup(BuildTargetGroup.Android) as PXR_Settings;
            }
        }
        private bool SetRequiredPluginInBuild()
        {
            PluginImporter[] plugins = PluginImporter.GetAllImporters();
            foreach (PluginImporter plugin in plugins)
            {
                if (plugin.assetPath.Contains("PxrPlatform.aar"))
                {
                    plugin.SetIncludeInBuildDelegate((path) =>
                    {
#if PICO_MS_SDK
                        return false;
#else
                        return IsLoaderExists(true);
#endif
                      
                    });
                }
                if (plugin.assetPath.Contains(".ForUnitySDK.aar"))
                {
                    Debug.Log("PXRLog [Build Check]OpenXR is enabled");
                    plugin.SetIncludeInBuildDelegate((path) =>
                    {
#if ENABLE_PICO_OPENXR_SDK
                        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                        foreach (var feature in settings.GetFeatures<OpenXRFeature>())
                        {
                            if (feature is PICOFeature)
                            {
                                return feature.enabled;
                            }
                        }

                        return false;
#else
#if PICO_MS_SDK
                        return false;
#else
                        return  IsLoaderExists(true);
#endif
#endif
                    });
                }
                if (plugin.assetPath.Contains("StagePlatform.aar"))
                {
                    plugin.SetIncludeInBuildDelegate((path) =>
                    {
#if PICO_MS_SDK
                        return true;
#else
                        return false;
#endif
                    });
                }
            }
#if PICO_MS_SDK
                        return false;
#else
            return  IsLoaderExists(true);
#endif
        }

        public override void OnPreprocessBuild(BuildReport report)
        {
            bool ret=SetRequiredPluginInBuild();
            if (report.summary.platformGroup != BuildTargetGroup.Android)
                return;
            if (!ret)
                return;
            GraphicsDeviceType firstGfxType =
                PlayerSettings.GetGraphicsAPIs(EditorUserBuildSettings.activeBuildTarget)[0];
            if (firstGfxType != GraphicsDeviceType.OpenGLES3 && firstGfxType != GraphicsDeviceType.Vulkan && firstGfxType != GraphicsDeviceType.OpenGLES2)
                throw new BuildFailedException($"PICO Plugin on mobile platforms nonsupport the {firstGfxType}");
            if (PxrSettings.stereoRenderingModeAndroid == PXR_Settings.StereoRenderingModeAndroid.Multiview && firstGfxType == GraphicsDeviceType.OpenGLES2)
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES3 });
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29)
                throw new BuildFailedException("Android Minimum API must be set to 29 or higher for PICO Plugin.");
            base.OnPreprocessBuild(report);
        }
    }

#if UNITY_2021_3_OR_NEWER
    internal class PXR_BuildHooks : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder { get; }

        private static readonly Dictionary<string, string> AndroidBootConfigVars = new Dictionary<string, string>()
        {
            { "xr-usable-core-mask-enabled", "1"},
            { "xr-require-backbuffer-textures", "0" },
            { "xr-hide-memoryless-render-texture", "1" },
            { "xr-vulkan-extension-fragment-density-map-enabled", "1"}
        };

        public void OnPreprocessBuild(BuildReport report)
        {
#if PICO_MS_SDK
            
#else
            if (report.summary.platformGroup == BuildTargetGroup.Android)
            {

                var bootConfig = new BootConfig(report);
                bootConfig.ReadBootConfig();

                foreach (var entry in AndroidBootConfigVars)
                {
                    bootConfig.SetValueForKey(entry.Key, entry.Value);
                }

                bootConfig.WriteBootConfig();

#if ENABLE_PICO_XR_SDK || ENABLE_PICO_OPENXR_SDK
                var issues = PXR_ProjectValidationRequired.GetValidationIssues();
                foreach (var issue in issues)
                {
                    if (issue.error)
                    {
                        Debug.LogError($"PXR SDK validation failed: {issue.description}");
                        throw new BuildFailedException($"There are unresolved PXR configuration errors");
                    }
                }
#endif
#if AR_FOUNDATION_6_IMAGE_TRACKING
                PXR_ImageTrackingBuildValidator.ValidateAllProjectLibrariesOrThrow(
                    PXR_ProjectSetting.GetProjectConfig().imageTracking);
#endif
            }
#endif
            }

            public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platformGroup == BuildTargetGroup.Android)
            {
                BootConfig bootConfig = new BootConfig(report);
                bootConfig.ReadBootConfig();

                foreach (KeyValuePair<string, string> entry in AndroidBootConfigVars)
                {
                    bootConfig.ClearEntryForKeyAndValue(entry.Key, entry.Value);
                }

                bootConfig.WriteBootConfig();

            }

        }
    }

    /// <summary>
    /// Small utility class for reading, updating and writing boot config.
    /// </summary>
    internal class BootConfig
    {
        private const string XrBootSettingsKey = "xr-boot-settings";

        private readonly Dictionary<string, string> bootConfigSettings;
        private readonly string buildTargetName;

        public BootConfig(BuildReport report)
        {
            bootConfigSettings = new Dictionary<string, string>();
            buildTargetName = BuildPipeline.GetBuildTargetName(report.summary.platform);
        }

        public void ReadBootConfig()
        {
            bootConfigSettings.Clear();

            string xrBootSettings = EditorUserBuildSettings.GetPlatformSettings(buildTargetName, XrBootSettingsKey);
            if (!string.IsNullOrEmpty(xrBootSettings))
            {
                var bootSettings = xrBootSettings.Split(';');
                foreach (var bootSetting in bootSettings)
                {
                    var setting = bootSetting.Split(':');
                    if (setting.Length == 2 && !string.IsNullOrEmpty(setting[0]) && !string.IsNullOrEmpty(setting[1]))
                    {
                        bootConfigSettings.Add(setting[0], setting[1]);
                    }
                }
            }
        }

        public void SetValueForKey(string key, string value) => bootConfigSettings[key] = value;

        public bool TryGetValue(string key, out string value) => bootConfigSettings.TryGetValue(key, out value);

        public void ClearEntryForKeyAndValue(string key, string value)
        {
            if (bootConfigSettings.TryGetValue(key, out string dictValue) && dictValue == value)
            {
                bootConfigSettings.Remove(key);
            }
        }

        public void WriteBootConfig()
        {
            bool firstEntry = true;
            var sb = new System.Text.StringBuilder();
            foreach (var kvp in bootConfigSettings)
            {
                if (!firstEntry)
                {
                    sb.Append(";");
                }

                sb.Append($"{kvp.Key}:{kvp.Value}");
                firstEntry = false;
            }

            EditorUserBuildSettings.SetPlatformSettings(buildTargetName, XrBootSettingsKey, sb.ToString());
        }
    }

#endif

#if UNITY_ANDROID
    internal class PXR_Manifest : IPostGenerateGradleAndroidProject
    {
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (!PXR_BuildProcessor.IsLoaderExists())
                return;
            string originManifestPath = path + "/src/main/AndroidManifest.xml";
            XmlDocument doc = new XmlDocument();
            doc.Load(originManifestPath);
            string manifestTagPath = "/manifest";
            string applicationTagPath = manifestTagPath + "/application";
            string metaDataTagPath = applicationTagPath + "/meta-data";
            string usesPermissionTagName = "uses-permission";
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","pxr.sdk.generation"}},new Dictionary<string, string>{{"value","4"}});
#if PICO_MS_SDK
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","pvr.sdk.version"}},new Dictionary<string, string>{{"value","Unity_Spatial_"+PXR_Plugin.PXR_SDK_Version}});
            doc.Save(originManifestPath);
            return;
#endif
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","pvr.app.type"}},new Dictionary<string, string>{{"value","vr"}});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","pxr.sdk.version_code"}},new Dictionary<string, string>{{"value", "6020"}});
            doc.InsertAttributeInTargetTag(applicationTagPath,null, new Dictionary<string, string>() {{"requestLegacyExternalStorage", "true"}});
#if ENABLE_PICO_OPENXR_SDK
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","use.pxr.sdk"}},new Dictionary<string, string>{{"value", "2"}});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","pvr.sdk.version"}},new Dictionary<string, string>{{"value","Unity_OpenXR_"+PXR_Plugin.PXR_SDK_Version}});
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            bool mrPermission = false;
            doc.ApplyImageTrackingMetadata(false);

            foreach (var feature in settings.GetFeatures<OpenXRFeature>())
            {
#if AR_FOUNDATION_6_IMAGE_TRACKING
                if (feature is PICOSpatialImageTracking)
                {
                    doc.ApplyImageTrackingMetadata(feature.enabled);
                    mrPermission |= feature.enabled;
                }
#endif

                if (feature is PICOSceneCapture)
                {
                    if (feature.enabled)
                    {
                        doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_scene_anchor" } },
                            new Dictionary<string, string> { { "value", "1" } });
                        mrPermission = true;
                    }
                    else
                    {
                        doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_scene_anchor" } });
                    }
                }

                if (feature is PICOSpatialAnchor)
                {
                    if (feature.enabled)
                    {
                        doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_spatial_anchor" } },
                            new Dictionary<string, string> { { "value", "1" } });
                    }
                    else
                    {
                        doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_spatial_anchor" } });
                    }
                }

                if (feature is PICOSpatialMesh)
                {
                    if (feature.enabled)
                    {
                        doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_mesh_anchor" } },
                            new Dictionary<string, string> { { "value", "1" } });
                        mrPermission = true;
                    }
                    else
                    {
                        doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_mesh_anchor" } });
                    }
                }

                if (feature is FacialSimulationFeature targetFacialSimulationFeature)
                {
                    if (targetFacialSimulationFeature.enabled)
                    {
                    
                        bool enableFaceTrackingPermission = targetFacialSimulationFeature.enableFaceTrackingPermission;
                        bool enableRecordAudioPermission = targetFacialSimulationFeature.enableRecordAudioPermission;

                        if (enableFaceTrackingPermission)
                        {
                            doc.CreateElementInTag(manifestTagPath, usesPermissionTagName, new Dictionary<string, string> { { "name", "com.picovr.permission.FACE_TRACKING" } });
                            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "picovr.software.face_tracking" } },
                            new Dictionary<string, string> { { "value", "false/true" } });
                        }
                        else
                        {
                            doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName, "name", "com.picovr.permission.FACE_TRACKING");
                            doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "picovr.software.face_tracking" } });
                        }
                        if (enableRecordAudioPermission)
                        {
                            doc.CreateElementInTag(manifestTagPath, usesPermissionTagName, new Dictionary<string, string> { { "name", "android.permission.RECORD_AUDIO" } });
                        }
                        else
                        {
                            doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName, "name", "android.permission.RECORD_AUDIO");
                        }
                        
                    }
                    else
                    {
                        doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName, "name", "com.picovr.permission.FACE_TRACKING");
                        doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName, "name", "android.permission.RECORD_AUDIO");
                        doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "picovr.software.face_tracking" } });
                    }
                }
#if XR_HAND
                if (feature is HandTracking)
                {
                    if (feature.enabled)
                    {
                        if (PXR_OpenXRProjectSetting.GetProjectConfig().isHandTracking)
                        {
                            doc.CreateElementInTag(manifestTagPath, usesPermissionTagName,
                                new Dictionary<string, string> { { "name", "com.picovr.permission.HAND_TRACKING" } });
                            
                            if (PXR_OpenXRProjectSetting.GetProjectConfig().handTrackingSupportType == HandTrackingSupport.HandsOnly)
                            {
                                doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "handtracking" } },
                                    new Dictionary<string, string> { { "value", "1" } });
                                doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "controller" } });
                            }
                            else
                            {
                                doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "handtracking" } },
                                    new Dictionary<string, string> { { "value", "1" } });
                                doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "controller" } },
                                    new Dictionary<string, string> { { "value", "1" } });
                            }
                            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "Hand_Tracking_HighFrequency" } },
                                new Dictionary<string, string> { { "value", PXR_OpenXRProjectSetting.GetProjectConfig().highFrequencyHand ? "1" : "0" } });
                        }
                        else
                        {
                            doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName,
                                "android:name", "com.picovr.permission.HAND_TRACKING");
                            doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "handtracking" } });
                            doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "Hand_Tracking_HighFrequency" } });
                            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "controller" } },
                                new Dictionary<string, string> { { "value", "1" } });
                        }
                    }
                    else
                    {
                        doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName,
                            "android:name", "com.picovr.permission.HAND_TRACKING");
                        doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "handtracking" } });
                        doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "Hand_Tracking_HighFrequency" } });
                        doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "controller" } },
                            new Dictionary<string, string> { { "value", "1" } });
                    }
                }
            }
#else
            }

            doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName,
                "android:name", "com.picovr.permission.HAND_TRACKING");
            doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "handtracking" } });
            doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "Hand_Tracking_HighFrequency" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "controller" } },
                new Dictionary<string, string> { { "value", "1" } });
            
#endif
            if (PXR_OpenXRProjectSetting.GetProjectConfig().isEyeTracking)
            {
                doc.CreateElementInTag(manifestTagPath, usesPermissionTagName,
                    new Dictionary<string, string> { { "name", "com.picovr.permission.EYE_TRACKING" } });
                doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "picovr.software.eye_tracking" } },
                    new Dictionary<string, string> { { "value", "1" } });
                doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "eyetracking_calibration" } },
                    new Dictionary<string, string> { { "value", PXR_OpenXRProjectSetting.GetProjectConfig().isEyeTrackingCalibration ? "true" : "false" } });
            }
            else
            {
                doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName,
                    "android:name", "com.picovr.permission.EYE_TRACKING");
                doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "picovr.software.eye_tracking" } });
                doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "eyetracking_calibration" } });
            }

            if (PXR_OpenXRProjectSetting.GetProjectConfig().MRSafeguard)
            {
                doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_mr_safeguard" } },
                    new Dictionary<string, string> { { "value", PXR_OpenXRProjectSetting.GetProjectConfig().MRSafeguard ? "1" : "0" } });
            }
            else
            {
                doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_mr_safeguard" } });
            }

            if (mrPermission)
            {
                doc.CreateElementInTag(manifestTagPath, usesPermissionTagName,
                    new Dictionary<string, string> { { "name", "com.picovr.permission.SPATIAL_DATA" } });
            }
            else
            {
                doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName,
                    "android:name", "com.picovr.permission.SPATIAL_DATA");
            }


            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "pvr.app.splash" } },
                new Dictionary<string, string> { { "value", PXR_OpenXRProjectSetting.GetProjectConfig().GetSystemSplashScreen(path) } });
                
#else
            var settings = PXR_XmlTools.GetSettings();
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","use.pxr.sdk"}},new Dictionary<string, string>{{"value", "1"}});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","pvr.sdk.version"}},new Dictionary<string, string>{{"value","Unity_XR_"+PXR_Plugin.PXR_SDK_Version}});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","enable_cpt"}},new Dictionary<string, string>{{"value",PXR_ProjectSetting.GetProjectConfig().useContentProtect ? "1" : "0"}});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","Enable_AdaptiveHandModel"}},new Dictionary<string, string> {{"value",PXR_ProjectSetting.GetProjectConfig().adaptiveHand ? "1" : "0" }});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","Hand_Tracking_HighFrequency"}},new Dictionary<string, string> {{"value",PXR_ProjectSetting.GetProjectConfig().highFrequencyHand ? "1" : "0" }});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","rendering_mode"}},new Dictionary<string, string>{{"value",((int)settings.stereoRenderingModeAndroid).ToString()}});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","display_rate"}},new Dictionary<string, string>{{"value",((int)settings.systemDisplayFrequency).ToString()}});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","color_Space"}},new Dictionary<string, string>{{"value",QualitySettings.activeColorSpace == ColorSpace.Linear ? "1" : "0"}});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","MRCsupport"}},new Dictionary<string, string>{{"value",PXR_ProjectSetting.GetProjectConfig().openMRC ? "1" : "0" }});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","pvr.LateLatching"}}, new Dictionary<string, string> {{"value",PXR_ProjectSetting.GetProjectConfig().latelatching ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","pvr.LateLatchingDebug"}}, new Dictionary<string, string> {{"value", PXR_ProjectSetting.GetProjectConfig().latelatching && PXR_ProjectSetting.GetProjectConfig().latelatchingDebug ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","pvr.app.splash"} },new Dictionary<string, string>{{"value",settings.GetSystemSplashScreen(path)}});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","PICO.swift.feature"}},new Dictionary<string, string>{{"value",PXR_ProjectSetting.GetProjectConfig().bodyTracking ? "1" : "0" }});
            doc.InsertAttributeInTargetTag(metaDataTagPath,new Dictionary<string, string>{{"name","adaptive_resolution"}},new Dictionary<string, string>{{"value",PXR_ProjectSetting.GetProjectConfig().adaptiveResolution ? "1" : "0" }});
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_mr_safeguard" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().mrSafeguard ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_vst" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().videoSeeThrough ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_anchor" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().spatialAnchor ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "mr_map_mgr_auto_start" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().spatialAnchor ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_spatial_anchor" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().spatialAnchor ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_cloud_anchor" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().sharedAnchor ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_mesh_anchor" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().spatialMesh ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_scene_anchor" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().sceneCapture ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_plane_detection" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().planeDetection ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_environment_depth" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().environmentDepth ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_light_estimation" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().lightEstimation ? "1" : "0" } });
            bool objectTrackingEnabled = PXR_ProjectSetting.GetProjectConfig().objectTracking;
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_dynamic_object_tracking" } }, new Dictionary<string, string> { { "value", objectTrackingEnabled ? "1" : "0" } });
            doc.ApplyImageTrackingMetadata(PXR_ProjectSetting.GetProjectConfig().imageTracking);
            doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_keyboard_passthrough" } });
            doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "enable_environment_passthrough_preference" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "pvr.SuperResolution" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().superResolution ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "pvr.NormalSharpening" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().normalSharpening ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "pvr.QualitySharpening" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().qualitySharpening ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "pvr.FixedFoveatedSharpening" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().fixedFoveatedSharpening ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "pvr.SelfAdaptiveSharpening" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().selfAdaptiveSharpening ? "1" : "0" } });
            doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "pvr.app.secure_mr" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().secureMR ? "1" : "0" } });
            doc.CreateElementInTag(manifestTagPath,usesPermissionTagName,new Dictionary<string, string>{{"name","android.permission.WRITE_SETTINGS"}});

            if (PXR_ProjectSetting.GetProjectConfig().eyeTracking || PXR_ProjectSetting.GetProjectConfig().enableETFR)
            {
                doc.CreateElementInTag(manifestTagPath, usesPermissionTagName, new Dictionary<string, string> { { "name", "com.picovr.permission.EYE_TRACKING" } });
                doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "picovr.software.eye_tracking" } }, new Dictionary<string, string> { { "value", "1" } });
                doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "eyetracking_calibration" } }, new Dictionary<string, string> { { "value", PXR_ProjectSetting.GetProjectConfig().eyetrackingCalibration ? "true" : "false" } });
            }
            if (PXR_ProjectSetting.GetProjectConfig().enableETFR)
            {
                doc.CreateElementInTag(manifestTagPath, usesPermissionTagName, new Dictionary<string, string> { { "name", "com.pico.xrpermission.ETFR" } });
            }

            if (PXR_BuildProcessor.RequiresSpatialDataPermission(
                    PXR_ProjectSetting.GetProjectConfig()))
            {
                doc.CreateElementInTag(manifestTagPath, usesPermissionTagName,
                    new Dictionary<string, string> { { "name", "com.picovr.permission.SPATIAL_DATA" } });
            }

            if (PXR_ProjectSetting.GetProjectConfig().handTracking)
            {
                if (PXR_ProjectSetting.GetProjectConfig().handTrackingSupportType==HandTrackingSupport.HandsOnly)
                {
                    doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "handtracking" } },
                        new Dictionary<string, string> { { "value", "1" } });
                    doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "controller" } });

                    doc.CreateElementInTag(manifestTagPath, usesPermissionTagName,
                        new Dictionary<string, string> { { "name", "com.picovr.permission.HAND_TRACKING" } });
                }
                else
                {
                    doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "handtracking" } },
                        new Dictionary<string, string> { { "value", "1" } });
                    doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "controller" } },
                        new Dictionary<string, string> { { "value", "1" } });
                    doc.CreateElementInTag(manifestTagPath, usesPermissionTagName,
                        new Dictionary<string, string> { { "name", "com.picovr.permission.HAND_TRACKING" } });
                }
            }
            else
            {
                doc.RemoveAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "handtracking" } });
                doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "controller" } },
                    new Dictionary<string, string> { { "value", "1" } });
                doc.RemoveNameValueElementInTag(manifestTagPath, usesPermissionTagName,
                    "android:name", "com.picovr.permission.HAND_TRACKING");
            }
           
            if (PXR_ProjectSetting.GetProjectConfig().faceTracking) { doc.CreateElementInTag(manifestTagPath, usesPermissionTagName, new Dictionary<string, string> { { "name", "com.picovr.permission.FACE_TRACKING" } }); }
            if (PXR_ProjectSetting.GetProjectConfig().lipsyncTracking) { doc.CreateElementInTag(manifestTagPath, usesPermissionTagName, new Dictionary<string, string> { { "name", "android.permission.RECORD_AUDIO" } }); }
            if (PXR_ProjectSetting.GetProjectConfig().faceTracking) { doc.InsertAttributeInTargetTag(metaDataTagPath, new Dictionary<string, string> { { "name", "picovr.software.face_tracking" } }, new Dictionary<string, string> { { "value", "false/true" } }); }
            
#endif
           
            
            
            doc.Save(originManifestPath);
        }
        public int callbackOrder { get { return 10000; } }
    }
#endif

    public static class PXR_XmlTools
    {
        static readonly string androidURI = "http://schemas.android.com/apk/res/android";

        public static void ApplyImageTrackingMetadata(
            this XmlDocument doc,
            bool enabled)
        {
            const string metadataPath = "/manifest/application/meta-data";
            var filter = new Dictionary<string, string>
            {
                { "name", "enable_image_tracking" }
            };
            if (!enabled)
            {
                doc.RemoveAttributeInTargetTag(metadataPath, filter);
                return;
            }

            doc.InsertAttributeInTargetTag(
                metadataPath,
                filter,
                new Dictionary<string, string> { { "value", "1" } });
        }

        public static void InsertAttributeInTargetTag(this XmlDocument doc, string tagPath, Dictionary<string, string> filterDic, Dictionary<string, string> attributeDic)
        {
            XmlElement targetElement = null;
            if (filterDic == null)
                targetElement = doc.SelectSingleNode(tagPath) as XmlElement;
            else
            {
                XmlNodeList nodeList = doc.SelectNodes(tagPath);
                if (nodeList != null)
                {
                    foreach (XmlNode node in nodeList)
                    {
                        if (FilterCheck(node as XmlElement, filterDic))
                        {
                            targetElement = node as XmlElement;
                            break;
                        }
                    }
                }
            }
            if (targetElement == null)
            {
                string parentPath = tagPath.Substring(0, tagPath.LastIndexOf("/"));
                string tagName = tagPath.Substring(tagPath.LastIndexOf("/") + 1);
                foreach (var item in attributeDic)
                    filterDic.Add(item.Key, item.Value);
                doc.CreateElementInTag(parentPath, tagName, filterDic);
            }
            else UpdateOrCreateAttribute(targetElement, attributeDic);
        }
        public static void RemoveAttributeInTargetTag(this XmlDocument doc, string tagPath, Dictionary<string, string> filterDic)
        {
            if (filterDic != null)
            {
                XmlNodeList nodeList = doc.SelectNodes(tagPath);
                if (nodeList != null)
                {
                    foreach (XmlNode node in nodeList)
                    {
                        if (FilterCheck(node as XmlElement, filterDic))
                        {
                            node.ParentNode?.RemoveChild(node);
                            break;
                        }
                    }
                }
            }
        }

        public static void RemoveNameValueElementInTag(this XmlDocument doc, string parentPath, string tag, string name,
            string value)
        {
            var xmlNodeList = doc.SelectNodes(parentPath + "/" + tag);

            foreach (XmlNode node in xmlNodeList)
            {
                var attributeList = ((XmlElement)node).Attributes;

                foreach (XmlAttribute attrib in attributeList)
                {
                    if (attrib.Name == name && attrib.Value == value)
                    {
                        node.ParentNode?.RemoveChild(node);
                    }
                }
            }
        }
        public static void CreateElementInTag(this XmlDocument doc, string parentPath, string tagName,
            Dictionary<string, string> attributeDic)
        {
            XmlNode parentNode = doc.SelectSingleNode(parentPath);
            if (parentNode == null)
            {
                return;
            }

            foreach (XmlNode childNode in parentNode.ChildNodes)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    if (FilterCheck((XmlElement)childNode, attributeDic))
                        return;
                }
            }

            XmlElement newElement = doc.CreateElement(tagName);
            UpdateOrCreateAttribute(newElement, attributeDic);
            parentNode.AppendChild(newElement);
        }
        private static bool FilterCheck(XmlElement element, Dictionary<string, string> filterDic)
        {
            foreach (var filterCase in filterDic)
            {
                string caseValue = element.GetAttribute(filterCase.Key, androidURI);
                if (String.IsNullOrEmpty(caseValue) || caseValue != filterCase.Value)
                    return false;
            }
            return true;
        }
        private static void UpdateOrCreateAttribute(XmlElement element, Dictionary<string, string> attributeDic)
        {
            foreach (var attributeItem in attributeDic)
            {
                element.SetAttribute(attributeItem.Key, androidURI, attributeItem.Value);
            }
        }
        public static PXR_Settings GetSettings()
        {
            PXR_Settings settings = null;
#if UNITY_EDITOR
            UnityEditor.EditorBuildSettings.TryGetConfigObject<PXR_Settings>("ByteDance.PICO.XR.Settings", out settings);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            settings = PXR_Settings.settings;
#endif
            return settings;
        }
    }

#if AR_FOUNDATION_6_IMAGE_TRACKING
    public static class PXR_ImageTrackingBuildValidator
    {
        private const string ImageLibrarySearchFilter = "t:XRReferenceImageLibrary";
        private const string LogTag = "[PXR_ImageTrackingDebug]";
        private const int MaximumReferenceImageCountExclusive = 300;
        private const int MaximumImageDimensionInclusive = 640;
        private const int RecommendedMinimumImageDimension = 300;

        public static void ValidateAllProjectLibrariesOrThrow(bool imageTrackingEnabled)
        {
            ValidateAllProjectLibrariesOrThrow(
                imageTrackingEnabled,
                FindProjectImageLibraryAssetPaths());
        }

        public static void ValidateAllProjectLibrariesOrThrow(
            bool imageTrackingEnabled,
            IEnumerable<string> assetPaths)
        {
            if (!imageTrackingEnabled)
            {
                return;
            }

            if (assetPaths == null)
            {
                throw new ArgumentNullException(nameof(assetPaths));
            }

            List<string> invalidEntries = new List<string>();
            foreach (string assetPath in assetPaths)
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                XRReferenceImageLibrary library =
                    AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(
                        assetPath);
                if (library == null)
                {
                    continue;
                }

                CollectImageLibraryValidationIssues(
                    library,
                    assetPath,
                    invalidEntries);
            }

            if (invalidEntries.Count == 0)
            {
                return;
            }

            throw new BuildFailedException(CreateFailureMessage(invalidEntries));
        }

        private static string[] FindProjectImageLibraryAssetPaths()
        {
            string[] guids = AssetDatabase.FindAssets(
                ImageLibrarySearchFilter,
                new[] { "Assets" });
            string[] paths = new string[guids.Length];
            for (int index = 0; index < guids.Length; index++)
            {
                paths[index] = AssetDatabase.GUIDToAssetPath(guids[index]);
            }

            return paths;
        }

        private static void CollectImageLibraryValidationIssues(
            XRReferenceImageLibrary library,
            string assetPath,
            ICollection<string> invalidEntries)
        {
            int imageCount = library.count;
            if (imageCount <= 0 || imageCount >= MaximumReferenceImageCountExclusive)
            {
                invalidEntries.Add(
                    $"- {assetPath}: image count must satisfy 0 < count < 300, actual={imageCount}.");
            }

            var guids = new HashSet<Guid>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < library.count; index++)
            {
                XRReferenceImage referenceImage = library[index];
                string imageName = string.IsNullOrEmpty(referenceImage.name)
                    ? "<unnamed>"
                    : referenceImage.name;
                Texture2D texture = referenceImage.texture;
                if (texture == null)
                {
                    invalidEntries.Add(
                        $"- {assetPath} [{index}] {imageName}: Keep Texture at Runtime must be enabled.");
                }

                Vector2 size = referenceImage.size;
                bool hasValidPhysicalSize =
                    referenceImage.specifySize
                    && IsFiniteAndPositive(referenceImage.width)
                    && IsFiniteAndPositive(size.x)
                    && IsFiniteAndPositive(size.y);

                if (!hasValidPhysicalSize)
                {
                    invalidEntries.Add(
                        $"- {assetPath} [{index}] {imageName}: Physical Size must be specified and greater than zero.");
                }

                if (texture != null)
                {
                    if (texture.width > MaximumImageDimensionInclusive
                        || texture.height > MaximumImageDimensionInclusive)
                    {
                        invalidEntries.Add(
                            $"- {assetPath} [{index}] {imageName}: Resolution must be less than or equal to 640x640, actual={texture.width}x{texture.height}.");
                    }
                    else if (texture.width < RecommendedMinimumImageDimension
                             || texture.height < RecommendedMinimumImageDimension)
                    {
                        Debug.LogWarning(
                            $"{LogTag} {assetPath} [{index}] {imageName}: Reference image dimensions below 300 pixels may reduce tracking quality, actual={texture.width}x{texture.height}.");
                    }
                }

                if (!guids.Add(referenceImage.guid))
                {
                    Debug.LogWarning(
                        $"{LogTag} {assetPath} [{index}] {imageName}: The source image library contains a duplicate reference image GUID. Runtime image mapping may be ambiguous.");
                }

                if (!names.Add(referenceImage.name))
                {
                    Debug.LogWarning(
                        $"{LogTag} {assetPath} [{index}] {imageName}: The source image library contains duplicate reference image names. Use reference image GUIDs for unique mapping.");
                }
            }
        }

        private static bool IsFiniteAndPositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string CreateFailureMessage(IReadOnlyList<string> invalidEntries)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("PICO Image Tracking build validation failed.");
            builder.AppendLine(
                "XR_PICO_spatial_image_tracking requires image libraries to satisfy these blocking requirements before building:");
            builder.AppendLine("- 0 < image count < 300");
            builder.AppendLine("- Keep Texture at Runtime is enabled for every reference image");
            builder.AppendLine("- Physical Size / physical width is positive for every reference image");
            builder.AppendLine("- Every reference image resolution is less than or equal to 640x640 pixels");
            builder.AppendLine();
            builder.AppendLine("Blocking validation issues:");
            for (int index = 0; index < invalidEntries.Count; index++)
            {
                builder.AppendLine(invalidEntries[index]);
            }
            builder.AppendLine();
            builder.Append(
                "Open each XR Reference Image Library and fix the listed blocking requirements. Non-blocking quality issues are reported as warnings only.");
            return builder.ToString();
        }
    }
#endif
}



