#if ENABLE_PICO_OPENXR_SDK && AR_FOUNDATION_6_IMAGE_TRACKING
using System.Collections.Generic;
using ByteDance.PICO.XR;
#if AR_FOUNDATION_6_IMAGE_TRACKING
using UnityEngine.XR.ARSubsystems;
#endif
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
        UiName = "PICO Spatial Image Tracking",
        Hidden = false,
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "PICO",
        OpenxrExtensionStrings = extensionString,
        Version = "1.0.0",
        FeatureId = featureId)]
#endif
    public class PICOSpatialImageTracking : OpenXRFeature
    {
        public const string featureId =
            "com.pico.openxr.feature.spatialimagetracking";
        public const string extensionString =
            "XR_PICO_spatial_image_tracking "
            + "XR_PICO_spatial_sensing XR_EXT_future";

#if AR_FOUNDATION_6_IMAGE_TRACKING
        private static readonly List<XRImageTrackingSubsystemDescriptor>
            s_ImageTrackingSubsystemDescriptors =
                new List<XRImageTrackingSubsystemDescriptor>();
#endif

        private bool m_HasSession;
        private bool m_OwnsSession;
        private ulong m_SessionToken;
#if AR_FOUNDATION_6_IMAGE_TRACKING
        private bool m_SubsystemCreateRequested;
        private bool m_IsSubsystemCreated;
        private bool m_IsSubsystemStarted;
#endif

        internal ISpatialImageTrackingSessionLifecycle
            imageTrackingSessionLifecycle { get; set; }

#if AR_FOUNDATION_6_IMAGE_TRACKING
        internal IImageTrackingSubsystemLifecycle
            imageTrackingSubsystemLifecycle { get; set; }
#endif

        public static bool isEnable =>
            OpenXRRuntime.IsExtensionEnabled(
                "XR_PICO_spatial_image_tracking");

        public static bool isPassthroughAlignedLocationEnabled =>
            OpenXRRuntime.IsExtensionEnabled(
                PICOPassthroughAlignedLocation.extensionString);

        protected override void OnSessionCreate(ulong xrSession)
        {
            base.OnSessionCreate(xrSession);
            if (m_HasSession && m_SessionToken == xrSession)
            {
                return;
            }

            if (m_HasSession)
            {
                CleanupSession(m_SessionToken);
            }

            m_HasSession = true;
            m_SessionToken = xrSession;
            m_OwnsSession = SessionLifecycle.Acquire(
                SpatialImageTrackingSessionOwner.OpenXrFeature,
                xrSession);
#if AR_FOUNDATION_6_IMAGE_TRACKING
            if (m_OwnsSession)
            {
                EnsureSubsystemCreated();
            }
#endif
        }

        protected override void OnSessionExiting(ulong xrSession)
        {
            CleanupSession(xrSession);
            base.OnSessionExiting(xrSession);
        }

        protected override void OnSessionLossPending(ulong xrSession)
        {
            CleanupSession(xrSession);
            base.OnSessionLossPending(xrSession);
        }

        protected override void OnSessionDestroy(ulong xrSession)
        {
            CleanupSession(xrSession);
            base.OnSessionDestroy(xrSession);
        }

        private void CleanupSession(ulong xrSession)
        {
            if (!m_HasSession || m_SessionToken != xrSession)
            {
                return;
            }

            if (m_OwnsSession)
            {
#if AR_FOUNDATION_6_IMAGE_TRACKING
                StopSubsystemIfStarted();
#endif
                SessionLifecycle.Release(
                    SpatialImageTrackingSessionOwner.OpenXrFeature,
                    xrSession,
                    forceSessionTeardown: true);
            }

            m_HasSession = false;
            m_OwnsSession = false;
            m_SessionToken = 0;
        }

#if AR_FOUNDATION_6_IMAGE_TRACKING
        protected override void OnSubsystemCreate()
        {
            base.OnSubsystemCreate();
            m_SubsystemCreateRequested = true;
            EnsureSubsystemCreated();
        }

        protected override void OnSubsystemStart()
        {
            if (!m_OwnsSession
                || !m_IsSubsystemCreated
                || m_IsSubsystemStarted)
            {
                return;
            }

            SubsystemLifecycle.Start();
            m_IsSubsystemStarted = true;
        }

        protected override void OnSubsystemStop()
        {
            StopSubsystemIfStarted();
        }

        protected override void OnSubsystemDestroy()
        {
            StopSubsystemIfStarted();

            if (m_IsSubsystemCreated)
            {
                SubsystemLifecycle.Destroy();
                m_IsSubsystemCreated = false;
            }

            m_SubsystemCreateRequested = false;
            base.OnSubsystemDestroy();
        }

        private void EnsureSubsystemCreated()
        {
            if (!m_SubsystemCreateRequested || m_IsSubsystemCreated)
            {
                return;
            }

            SubsystemLifecycle.Create();
            m_IsSubsystemCreated = true;
        }

        private void StopSubsystemIfStarted()
        {
            if (!m_IsSubsystemStarted)
            {
                return;
            }

            SubsystemLifecycle.Stop();
            m_IsSubsystemStarted = false;
        }

        private ISpatialImageTrackingSessionLifecycle SessionLifecycle
        {
            get
            {
                if (imageTrackingSessionLifecycle == null)
                {
                    imageTrackingSessionLifecycle =
                        SpatialImageTrackingSessionLifecycle.Shared;
                }

                return imageTrackingSessionLifecycle;
            }
        }

        private IImageTrackingSubsystemLifecycle SubsystemLifecycle
        {
            get
            {
                if (imageTrackingSubsystemLifecycle == null)
                {
                    imageTrackingSubsystemLifecycle =
                        new ImageTrackingSubsystemLifecycle(
                            () => CreateSubsystem<
                                XRImageTrackingSubsystemDescriptor,
                                XRImageTrackingSubsystem>(
                                s_ImageTrackingSubsystemDescriptors,
                                PXR_ImageTrackingSubsystem.k_SubsystemId),
                            () => StartSubsystem<XRImageTrackingSubsystem>(),
                            () => StopSubsystem<XRImageTrackingSubsystem>(),
                            () => DestroySubsystem<XRImageTrackingSubsystem>());
                }

                return imageTrackingSubsystemLifecycle;
            }
        }
#endif
    }
}
#endif
