#if ENABLE_PICO_OPENXR_SDK && AR_FOUNDATION_6_IMAGE_TRACKING
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace ByteDance.PICO.OpenXR
{
#if UNITY_EDITOR
    [OpenXRFeature(
        UiName = "PICO Passthrough Aligned Location",
        Hidden = false,
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "PICO",
        OpenxrExtensionStrings = extensionString,
        Version = "1.0.0",
        FeatureId = featureId)]
#endif
    public class PICOPassthroughAlignedLocation : OpenXRFeature
    {
        public const string featureId =
            "com.pico.openxr.feature.passthroughalignedlocation";

        public const string extensionString =
            "XR_PICO_passthrough_aligned_location";

        public static bool isEnable =>
            OpenXRRuntime.IsExtensionEnabled(extensionString);
    }
}
#endif
