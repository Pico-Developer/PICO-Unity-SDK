#if PICO_MS_SDK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

static class ProjectValidationRequired
{
    const string k_Catergory = "SpatialAdapter";

    [InitializeOnLoadMethod]
    static void AddRequiredRules()
    {
        NamedBuildTarget recommendedBuildTarget = NamedBuildTarget.Android;
        const int minSdkVersionInEditor = 29;
        const string minSdkNameInEditor = "SDK 29";

        var androidGlobalRules = new[]
        {
                new BuildValidationRule
                {
                    Category = k_Catergory,
                    Message = "Project has not been set up for SpatialAdapter. Please run 'Setup Project' to configure required settings and copy Android Manifest files.",
                    IsRuleEnabled = IsSpatialAdapterEnabled,
                    CheckPredicate = IsSetupProjectCompleted,
                    FixItMessage = "Run SpatialAdapter 'Setup Project' to apply required settings (Android platform, IL2CPP, ARM64, Vulkan, MinSDK 29) and copy AndroidManifest.xml / LauncherManifest.xml into Assets/Plugins/Android/.",
                    FixIt = InvokeSpatialAdapterSetup,
                    FixItAutomatic = false,
                    Error = true
                },

                new BuildValidationRule
                {
                    Category = k_Catergory,
                    Message = $"SpatialAdapter Android SDK targeting minimum {minSdkNameInEditor} API Level.",
                    IsRuleEnabled = IsSpatialAdapterEnabled,
                    CheckPredicate = () =>
                    {
                        return (int)PlayerSettings.Android.minSdkVersion >= minSdkVersionInEditor;
                    },
                    FixItMessage = "Open Project Settings > Player Settings > Player> Other Settings > Android tab to set PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29.",
                    FixIt = () =>
                    {
                        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
                    },
                    Error = true
                },

            new BuildValidationRule
                {
                    Category = k_Catergory,
                    Message = $"Build target platform needs to be modified to Android!",
                    IsRuleEnabled = IsSpatialAdapterEnabled,
                    CheckPredicate = () =>
                    {
                        return EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
                    },
                    FixItMessage = "Open Project Settings > Platform> Android",
                    FixIt = () =>
                    {
                        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                    },
                    Error = true
                },

                new BuildValidationRule
                {
                    Category = k_Catergory,
                    Message = "Set the Graphics API to Vulkan for Android.",
                    IsRuleEnabled = IsSpatialAdapterEnabled,
                    CheckPredicate = () =>
                    {
                        var buildTarget = BuildTarget.Android;
                        if (PlayerSettings.GetUseDefaultGraphicsAPIs(buildTarget))
                        {
                            return false;
                        }
                        var apis = PlayerSettings.GetGraphicsAPIs(buildTarget);
                        return apis != null && apis.Length > 0 && apis[0] == GraphicsDeviceType.Vulkan;
                    },
                    FixItMessage = "Open Project Settings > Player Settings > Player> Other Settings > 'Graphics API' set Vulkan for Android.",
                    FixIt = () =>
                    {
                        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
                    },
                    Error = true
                },

                new BuildValidationRule
                {
                    Category = k_Catergory,
                    Message = "Need to set ARM64 architecture and IL2CPP scripting.",
                    IsRuleEnabled = IsSpatialAdapterEnabled,
                    CheckPredicate = () =>
                    {

                        return (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != AndroidArchitecture.None && PlayerSettings.GetScriptingBackend(recommendedBuildTarget) == ScriptingImplementation.IL2CPP;

                    },
                    FixItMessage = "Open Project Settings > Player Settings > Player> Other Settings > Android tab and ensure 'Scripting Backend'" +
                        " is set to 'IL2CPP'. Then under 'Target Architectures' enable 'ARM64'.",
                    FixIt = () =>
                    {
                        PlayerSettings.SetScriptingBackend(recommendedBuildTarget, ScriptingImplementation.IL2CPP);
                        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    },
                    Error = true
                },

                new BuildValidationRule
                {
                    Category = k_Catergory,
                    Message = "Need to disable Splash Screen.",
                    IsRuleEnabled = IsSpatialAdapterEnabled,
                    CheckPredicate = () =>
                    {

                        return (!Application.HasProLicense() || !PlayerSettings.SplashScreen.show);

                    },
                    FixItMessage = "Open Project Settings > Player Settings > Player> Other Settings > SplashScreen" +
                        " is unchecked",
                    FixIt = () =>
                    {
                        PlayerSettings.SplashScreen.show = false;
                    },
                    Error = true
                },

                new BuildValidationRule
                {
                    Category = k_Catergory,
                    Message = $"Only one MainCamera is allowed.",
                    IsRuleEnabled = IsSpatialAdapterEnabled,
                    CheckPredicate = () =>
                    {
                        List<Camera> components = FindComponentsInScene<Camera>().Where(component => (component.isActiveAndEnabled && component.gameObject.CompareTag("MainCamera"))).ToList();
                        if (components.Count == 1)
                        {
                            return true;
                        }
                        return false;
                    },
                    FixItMessage = "Scene > MainCamera > Disable.",
                    FixIt = () =>
                    {
                        List<Camera> components = FindComponentsInScene<Camera>().Where(component => (component.enabled && component.gameObject.CompareTag("MainCamera"))).ToList();
                        for(int i=0; i < components.Count; i++)
                        {
                            GameObject gameObject = components[i].transform.gameObject;
                            if (i == 0)
                            {
                                gameObject.SetActive(true);
                            }
                            else
                            {
                                gameObject.tag = $"Camera{i}";
                                gameObject.SetActive(false);
                                Debug.LogWarning("SpatialAdapter Validation: Disabled MainCamera #" + i);
                            }
                        }
                    },
                    Error = true
                },

                new BuildValidationRule
                {
                    Category = k_Catergory,
                    Message = "ParticleSystem GameObject must not have a MeshRenderer component.",
                    IsRuleEnabled = IsSpatialAdapterEnabled,
                    CheckPredicate = () =>
                    {
                        return !FindComponentsInScene<ParticleSystem>().Any(particleSystem => particleSystem.GetComponent<MeshRenderer>() != null);
                    },
                    FixItMessage = "Move MeshRenderer (and MeshFilter if present) to a new empty child GameObject under the ParticleSystem GameObject.",
                    FixIt = () =>
                    {
                        var activeScene = SceneManager.GetActiveScene();
                        var particleSystems = FindComponentsInScene<ParticleSystem>();
                        var anyChanges = false;

                        foreach (var particleSystem in particleSystems)
                        {
                            if (particleSystem == null)
                                continue;

                            var particleGameObject = particleSystem.gameObject;
                            var meshRenderer = particleGameObject.GetComponent<MeshRenderer>();
                            if (meshRenderer == null)
                                continue;

                            var meshFilter = particleGameObject.GetComponent<MeshFilter>();

                            var childGameObject = new GameObject("MeshRenderer");
                            Undo.RegisterCreatedObjectUndo(childGameObject, "Move MeshRenderer to child");
                            var childTransform = childGameObject.transform;
                            childTransform.SetParent(particleGameObject.transform, false);
                            childTransform.localPosition = Vector3.zero;
                            childTransform.localRotation = Quaternion.identity;
                            childTransform.localScale = Vector3.one;

                            if (meshFilter != null)
                            {
                                var newMeshFilter = childGameObject.AddComponent<MeshFilter>();
                                EditorUtility.CopySerialized(meshFilter, newMeshFilter);
                                Undo.DestroyObjectImmediate(meshFilter);
                            }

                            var newMeshRenderer = childGameObject.AddComponent<MeshRenderer>();
                            EditorUtility.CopySerialized(meshRenderer, newMeshRenderer);
                            Undo.DestroyObjectImmediate(meshRenderer);

                            anyChanges = true;
                        }

                        if (anyChanges)
                        {
                            EditorSceneManager.MarkSceneDirty(activeScene);
                        }
                    },
                    Error = true
                },
#if UNITY_6000_0_OR_NEWER
                new BuildValidationRule
                {
                    Category = k_Catergory,
                    Message = "PlayerSetting Application Entry is Activity.",
                    IsRuleEnabled = IsSpatialAdapterEnabled,
                    CheckPredicate = () =>
                    {

                        return (PlayerSettings.Android.applicationEntry == AndroidApplicationEntry.Activity);

                    },
                    FixItMessage = "Open Project Settings > Player Settings > Player> Other Settings > Application Entry" +
                        " is Activity",
                    FixIt = () =>
                    {
                        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
                    },
                    Error = true
                },
#endif
                //TODO: Add rule for SpatialCamera (Volume) settings

        };
        BuildValidator.AddRules(BuildTargetGroup.Android, androidGlobalRules);
    }

    static bool IsSpatialAdapterEnabled()
    {
        //TODO: connect to SpatialAdapterRuntime Enabled flag
        return true;
    }

    const string k_SpatialAdapterPackageName = "com.bytedance.pico.spatialadapter";
    const string k_AndroidManifestFileName = "AndroidManifest.xml";
    const string k_LauncherManifestFileName = "LauncherManifest.xml";

    static bool IsSetupProjectCompleted()
    {
        // Primary completion indicator: Manifest files copied to the target Android plugin folder by Setup Project.
        var androidManifest = Path.Combine("Assets", "Plugins", "Android", k_AndroidManifestFileName);
        var launcherManifest = Path.Combine("Assets", "Plugins", "Android", k_LauncherManifestFileName);
        if (!File.Exists(androidManifest) || !File.Exists(launcherManifest))
        {
            return false;
        }

        // Detect package upgrades that ship updated Manifests so the user is prompted to re-run Setup Project.
        var packageAndroidManifest = Path.Combine("Packages", k_SpatialAdapterPackageName, "Manifest", k_AndroidManifestFileName);
        var packageLauncherManifest = Path.Combine("Packages", k_SpatialAdapterPackageName, "Manifest", k_LauncherManifestFileName);
        if (!AreManifestsEqual(packageAndroidManifest, androidManifest) ||
            !AreManifestsEqual(packageLauncherManifest, launcherManifest))
        {
            return false;
        }

#if UNITY_6000_0_OR_NEWER
        // Setup Project on Unity 6 also forces Application Entry to Activity.
        if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.Activity)
        {
            return false;
        }
#endif

        return true;
    }

    static bool AreManifestsEqual(string packageManifestPath, string projectManifestPath)
    {
        // If the package no longer ships this manifest, do not block validation.
        if (!File.Exists(packageManifestPath))
        {
            return true;
        }

        if (!File.Exists(projectManifestPath))
        {
            return false;
        }

        var packageContent = File.ReadAllText(packageManifestPath).Replace("\r\n", "\n");
        var projectContent = File.ReadAllText(projectManifestPath).Replace("\r\n", "\n");
        return string.Equals(packageContent, projectContent, StringComparison.Ordinal);
    }

    static void InvokeSpatialAdapterSetup()
    {
        // Use reflection to avoid a direct assembly reference from Runtime-Package/Scripts/Editor
        // to the Exporter/Editor assembly (which would cause a circular dependency).
        const string typeName = "ByteDance.PICO.SpatialAdapter.Exporter.Editor.SpatialAdapterSetup";
        const string methodName = "Setup";

        try
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName, false);
                if (type == null)
                {
                    continue;
                }

                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    Debug.LogError($"SpatialAdapter Validation: '{typeName}.{methodName}' not found.");
                    return;
                }

                method.Invoke(null, null);
                return;
            }

            Debug.LogError($"SpatialAdapter Validation: Type '{typeName}' not found. Make sure the SpatialAdapter Exporter editor assembly is present.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"SpatialAdapter Validation: Failed to invoke Setup Project. {ex}");
        }
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
#endif
