#if AR_FOUNDATION_6_IMAGE_TRACKING
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    internal readonly struct ImageTrackingAlignmentOptions
    {
        internal ImageTrackingAlignmentOptions(
            PXR_ImageTrackingAlignmentMode mode,
            PXR_ImageTrackingAlignmentInvalidPolicy invalidPolicy)
        {
            Mode = mode;
            InvalidPolicy = invalidPolicy;
        }

        internal PXR_ImageTrackingAlignmentMode Mode { get; }

        internal PXR_ImageTrackingAlignmentInvalidPolicy InvalidPolicy { get; }
    }

    internal readonly struct ImageTrackingAlignmentLocateResult
    {
        internal ImageTrackingAlignmentLocateResult(
            Pose pose,
            TrackingState trackingState,
            bool hasValidPose,
            bool isAligned,
            bool hasAlignedPose,
            float modelScale,
            PXR_ImageTrackingAlignmentMode mode)
        {
            Pose = pose;
            TrackingState = trackingState;
            HasValidPose = hasValidPose;
            IsAligned = isAligned;
            HasAlignedPose = hasAlignedPose;
            ModelScale = modelScale;
            Mode = mode;
        }

        internal Pose Pose { get; }

        internal TrackingState TrackingState { get; }

        internal bool HasValidPose { get; }

        internal bool IsAligned { get; }

        internal bool HasAlignedPose { get; }

        internal float ModelScale { get; }

        internal PXR_ImageTrackingAlignmentMode Mode { get; }
    }

    internal static class ImageTrackingAlignmentLocator
    {
        private const ulong RequiredTrackedFlags =
            XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_VALID_BIT
            | XrSpaceLocationFlags.XR_SPACE_LOCATION_POSITION_VALID_BIT
            | XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_TRACKED_BIT
            | XrSpaceLocationFlags.XR_SPACE_LOCATION_POSITION_TRACKED_BIT;

        internal static ImageTrackingAlignmentLocateResult Locate(
            ISpatialImageTrackingNativeApi native,
            ulong anchor,
            Pose lastValidPose,
            bool hasLastValidPose,
            Pose lastValidAlignedPose,
            bool hasLastValidAlignedPose,
            ImageTrackingAlignmentOptions options)
        {
            if (options.Mode == PXR_ImageTrackingAlignmentMode.Original)
            {
                return LocateOriginal(native, anchor, options.Mode);
            }

            return LocateAligned(
                native,
                anchor,
                lastValidPose,
                hasLastValidPose,
                lastValidAlignedPose,
                hasLastValidAlignedPose,
                options);
        }

        private static ImageTrackingAlignmentLocateResult LocateOriginal(
            ISpatialImageTrackingNativeApi native,
            ulong anchor,
            PXR_ImageTrackingAlignmentMode mode)
        {
            PxrResult result = native.LocateAnchor(
                anchor,
                out SpatialImageTrackingLocation location);
            if (result != PxrResult.SUCCESS)
            {
                return Invalid(Pose.identity, false, false, 1.0f, mode);
            }

            ImageTrackingPoseConverter.Convert(
                location,
                out Pose pose,
                out TrackingState trackingState);
            bool hasValidPose = trackingState != TrackingState.None;
            return new ImageTrackingAlignmentLocateResult(
                pose,
                trackingState,
                hasValidPose,
                false,
                false,
                1.0f,
                mode);
        }

        private static ImageTrackingAlignmentLocateResult LocateAligned(
            ISpatialImageTrackingNativeApi native,
            ulong anchor,
            Pose lastValidPose,
            bool hasLastValidPose,
            Pose lastValidAlignedPose,
            bool hasLastValidAlignedPose,
            ImageTrackingAlignmentOptions options)
        {
            PxrResult result = native.LocateAnchorAligned(
                anchor,
                out SpatialImageTrackingAlignedLocation alignedLocation);
            if (result == PxrResult.SUCCESS
                && alignedLocation.AlignedValid
                && (alignedLocation.LocationFlags & RequiredTrackedFlags)
                == RequiredTrackedFlags)
            {
                var location = new SpatialImageTrackingLocation(
                    alignedLocation.LocationFlags,
                    alignedLocation.Pose);
                ImageTrackingPoseConverter.Convert(
                    location,
                    out Pose pose,
                    out TrackingState trackingState);
                bool hasValidPose = trackingState != TrackingState.None;
                float scale =
                    options.Mode
                    == PXR_ImageTrackingAlignmentMode.AlignedPositionAndScale
                        ? SanitizeScale(alignedLocation.ModelScale)
                        : 1.0f;
                return new ImageTrackingAlignmentLocateResult(
                    pose,
                    trackingState,
                    hasValidPose,
                    true,
                    hasValidPose,
                    scale,
                    options.Mode);
            }

            if (options.InvalidPolicy
                == PXR_ImageTrackingAlignmentInvalidPolicy.UseOriginalFallback)
            {
                ImageTrackingAlignmentLocateResult fallback =
                    LocateOriginal(native, anchor, options.Mode);
                return new ImageTrackingAlignmentLocateResult(
                    fallback.Pose,
                    fallback.TrackingState,
                    fallback.HasValidPose,
                    false,
                    false,
                    1.0f,
                    options.Mode);
            }

            if (options.InvalidPolicy
                == PXR_ImageTrackingAlignmentInvalidPolicy.HoldLastAlignedPose
                && hasLastValidAlignedPose)
            {
                return new ImageTrackingAlignmentLocateResult(
                    lastValidAlignedPose,
                    TrackingState.Limited,
                    true,
                    true,
                    false,
                    1.0f,
                    options.Mode);
            }

            return Invalid(
                hasLastValidPose ? lastValidPose : Pose.identity,
                true,
                false,
                1.0f,
                options.Mode);
        }

        private static ImageTrackingAlignmentLocateResult Invalid(
            Pose pose,
            bool isAligned,
            bool hasAlignedPose,
            float modelScale,
            PXR_ImageTrackingAlignmentMode mode)
        {
            return new ImageTrackingAlignmentLocateResult(
                pose,
                TrackingState.None,
                false,
                isAligned,
                hasAlignedPose,
                modelScale,
                mode);
        }

        private static float SanitizeScale(float scale)
        {
            return !float.IsNaN(scale) && !float.IsInfinity(scale) && scale > 0.0f
                ? scale
                : 1.0f;
        }
    }
}
#endif
