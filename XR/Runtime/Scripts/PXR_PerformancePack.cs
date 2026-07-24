using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
#if ENABLE_PICO_OPENXR_SDK
using ByteDance.PICO.OpenXR;
#endif

namespace ByteDance.PICO.XR
{
    public enum PXR_PerformancePackDeviceProfile
    {
        ProjectSwan = 0,
        PICO4Series = 1,
        OtherDevice = 2,
    }

    [Serializable]
    public sealed class PXR_PerformancePackDeviceConfig
    {
        public float renderScale = 1.0f;
        public string refreshRate = "90Hz";
        public string foveation = "FFR-None";
        public string antiAliasing = "Default";
        public bool superResolution;
        public bool hdr = true;
        public bool adaptiveResolution;
        public SharpeningMode sharpeningMode = SharpeningMode.None;
    }

    public sealed class PXR_PerformancePackConfig : ScriptableObject
    {
        public const string ResourceName = "PXR_PerformancePackRuntimeConfig";

        public bool deviceProjectSwan = true;
        public bool devicePico4Series = true;
        public bool deviceOtherDevice;
        public PXR_PerformancePackDeviceProfile editingProfile = PXR_PerformancePackDeviceProfile.ProjectSwan;

        public PXR_PerformancePackDeviceConfig projectSwan = new PXR_PerformancePackDeviceConfig
        {
            renderScale = 1.15f,
            refreshRate = "90Hz",
            foveation = "ETFR-Med",
            antiAliasing = "Default",
            superResolution = true,
            hdr = true,
            adaptiveResolution = true,
            sharpeningMode = SharpeningMode.None,
        };

        public PXR_PerformancePackDeviceConfig pico4Series = new PXR_PerformancePackDeviceConfig
        {
            renderScale = 1.0f,
            refreshRate = "72Hz",
            foveation = "FFR-None",
            antiAliasing = "Default",
            superResolution = false,
            hdr = false,
            adaptiveResolution = true,
            sharpeningMode = SharpeningMode.None,
        };

        public PXR_PerformancePackDeviceConfig otherDevice = new PXR_PerformancePackDeviceConfig
        {
            renderScale = 1.0f,
            refreshRate = "Default",
            foveation = "FFR-None",
            antiAliasing = "Default",
            superResolution = false,
            hdr = false,
            adaptiveResolution = false,
            sharpeningMode = SharpeningMode.None,
        };

        public string presetName = "Default";
        public long updatedAtUnixMs;

        public PXR_PerformancePackDeviceConfig GetDeviceConfig(PXR_PerformancePackDeviceProfile profile)
        {
            EnsureDeviceConfigs();
            switch (profile)
            {
                case PXR_PerformancePackDeviceProfile.PICO4Series:
                    return pico4Series;
                case PXR_PerformancePackDeviceProfile.OtherDevice:
                    return otherDevice;
                case PXR_PerformancePackDeviceProfile.ProjectSwan:
                default:
                    return projectSwan;
            }
        }

        public void EnsureDeviceConfigs()
        {
            if (projectSwan == null) projectSwan = new PXR_PerformancePackDeviceConfig();
            if (pico4Series == null) pico4Series = new PXR_PerformancePackDeviceConfig();
            if (otherDevice == null) otherDevice = new PXR_PerformancePackDeviceConfig();
        }
    }

    public sealed class PXR_PerformancePackRuntime : MonoBehaviour
    {
        private static PXR_PerformancePackConfig _config;
        private static PXR_PerformancePackDeviceConfig _activeConfig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void ApplyBeforeXrLoader()
        {
            _config = Resources.Load<PXR_PerformancePackConfig>(PXR_PerformancePackConfig.ResourceName);
            if (_config == null) return;

            _config.EnsureDeviceConfigs();
            _activeConfig = _config.GetDeviceConfig(ResolveRuntimeDeviceProfile());
            ApplyToProjectSettings(_activeConfig);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeApplier()
        {
            if (_config == null)
            {
                _config = Resources.Load<PXR_PerformancePackConfig>(PXR_PerformancePackConfig.ResourceName);
            }
            if (_config == null) return;

            var go = new GameObject("PXR Performance Pack Runtime");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<PXR_PerformancePackRuntime>();
        }

        private void Start()
        {
            if (_config == null) return;
            _config.EnsureDeviceConfigs();
            _activeConfig = _config.GetDeviceConfig(ResolveRuntimeDeviceProfile());
            ApplyAtRuntime(_activeConfig);
        }

        public static PXR_PerformancePackDeviceProfile ResolveRuntimeDeviceProfile()
        {
            string productName = PXR_Plugin.System.UPxr_GetProductName();
            if (StartsWithDeviceName(productName, "Project Swan"))
            {
                return PXR_PerformancePackDeviceProfile.ProjectSwan;
            }
            if (StartsWithDeviceName(productName, "PICO 4"))
            {
                return PXR_PerformancePackDeviceProfile.PICO4Series;
            }
            return PXR_PerformancePackDeviceProfile.OtherDevice;
        }

        private static bool StartsWithDeviceName(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyToProjectSettings(PXR_PerformancePackDeviceConfig config)
        {
            var projectConfig = PXR_ProjectSetting.GetProjectConfig();
            if (projectConfig != null)
            {
                ApplyFoveationToProjectSetting(projectConfig, config);
                projectConfig.adaptiveResolution = config.adaptiveResolution;
                projectConfig.superResolution = config.superResolution;

                SharpeningMode sharpeningMode = config.superResolution ? SharpeningMode.None : config.sharpeningMode;
                projectConfig.normalSharpening = sharpeningMode == SharpeningMode.Normal;
                projectConfig.qualitySharpening = sharpeningMode == SharpeningMode.Quality;
                projectConfig.fixedFoveatedSharpening = false;
                projectConfig.selfAdaptiveSharpening = false;
            }

            var settings = PXR_Settings.GetSettings();
            if (settings != null)
            {
                settings.systemDisplayFrequency = MapDisplayFrequency(config.refreshRate);
            }
        }

        private static void ApplyAtRuntime(PXR_PerformancePackDeviceConfig config)
        {
            float scale = Mathf.Clamp(config.renderScale, 0f, 2f);
            XRSettings.eyeTextureResolutionScale = scale;

            PXR_Manager manager = PXR_Manager.Instance;
            manager.adaptiveResolution = config.adaptiveResolution;
            manager.enableSuperResolution = config.superResolution;
            manager.sharpeningMode = config.superResolution ? SharpeningMode.None : config.sharpeningMode;
            manager.sharpeningEnhance = SharpeningEnhance.None;
            manager.maxEyeTextureScale = scale;
            manager.minEyeTextureScale = Mathf.Clamp(scale * 0.85f, 0f, manager.maxEyeTextureScale);

            if (TryParseFoveation(config.foveation, out FoveatedRenderingMode mode, out FoveationLevel level))
            {
                manager.foveatedRenderingMode = mode;
                if (mode == FoveatedRenderingMode.EyeTrackedFoveatedRendering)
                {
                    manager.eyeFoveationLevel = level;
                    manager.foveationLevel = FoveationLevel.None;
                    manager.eyeTracking = level != FoveationLevel.None;
                }
                else
                {
                    manager.foveationLevel = level;
                    manager.eyeFoveationLevel = FoveationLevel.None;
                }
            }

            if (TryParseRefreshRateHz(config.refreshRate, out int hz))
            {
#if ENABLE_PICO_OPENXR_SDK
                DisplayRefreshRateFeature.SetDisplayRefreshRate((float)hz);
#else
                PXR_Plugin.System.UPxr_SetSystemDisplayFrequency((float)hz);
#endif
            }
        }

        private static void ApplyFoveationToProjectSetting(PXR_ProjectSetting projectConfig, PXR_PerformancePackDeviceConfig config)
        {
            if (!TryParseFoveation(config.foveation, out FoveatedRenderingMode mode, out FoveationLevel level))
            {
                mode = FoveatedRenderingMode.FixedFoveatedRendering;
                level = FoveationLevel.None;
            }

            bool enabled = level != FoveationLevel.None;
            projectConfig.enableETFR = enabled && mode == FoveatedRenderingMode.EyeTrackedFoveatedRendering;
            projectConfig.foveationLevel = level;
            projectConfig.validationFFREnabled = enabled && mode == FoveatedRenderingMode.FixedFoveatedRendering;
            projectConfig.validationETFREnabled = enabled && mode == FoveatedRenderingMode.EyeTrackedFoveatedRendering;
            projectConfig.eyeTracking = projectConfig.enableETFR;
            projectConfig.enableSubsampled = enabled;
            projectConfig.recommendSubsamping = enabled;
        }

        private static PXR_Settings.SystemDisplayFrequency MapDisplayFrequency(string refreshRate)
        {
            if (!TryParseRefreshRateHz(refreshRate, out int hz)) return PXR_Settings.SystemDisplayFrequency.Default;
            if (hz == 72) return PXR_Settings.SystemDisplayFrequency.RefreshRate72;
            if (hz == 90) return PXR_Settings.SystemDisplayFrequency.RefreshRate90;
            return PXR_Settings.SystemDisplayFrequency.Default;
        }

        private static bool TryParseRefreshRateHz(string text, out int hz)
        {
            hz = 0;
            if (string.IsNullOrEmpty(text)) return false;

            int i = 0;
            while (i < text.Length && (text[i] < '0' || text[i] > '9')) i++;
            if (i >= text.Length) return false;

            int value = 0;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9')
            {
                value = value * 10 + (text[i] - '0');
                i++;
            }
            if (value <= 0) return false;
            hz = value;
            return true;
        }

        private static bool TryParseFoveation(string value, out FoveatedRenderingMode mode, out FoveationLevel level)
        {
            mode = FoveatedRenderingMode.FixedFoveatedRendering;
            level = FoveationLevel.None;
            if (string.IsNullOrEmpty(value)) return false;

            if (value.StartsWith("FFR-", StringComparison.Ordinal))
            {
                return TryParseFoveationLevel(value.Substring(4), out level);
            }
            if (value.StartsWith("ETFR-", StringComparison.Ordinal))
            {
                mode = FoveatedRenderingMode.EyeTrackedFoveatedRendering;
                return TryParseFoveationLevel(value.Substring(5), out level);
            }
            return false;
        }

        private static bool TryParseFoveationLevel(string value, out FoveationLevel level)
        {
            level = FoveationLevel.None;
            if (string.Equals(value, "None", StringComparison.Ordinal)) { level = FoveationLevel.None; return true; }
            if (string.Equals(value, "Low", StringComparison.Ordinal)) { level = FoveationLevel.Low; return true; }
            if (string.Equals(value, "Med", StringComparison.Ordinal)) { level = FoveationLevel.Med; return true; }
            if (string.Equals(value, "High", StringComparison.Ordinal)) { level = FoveationLevel.High; return true; }
            if (string.Equals(value, "TopHigh", StringComparison.Ordinal)) { level = FoveationLevel.TopHigh; return true; }
            return false;
        }
    }
}
