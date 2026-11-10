#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    internal enum ImageTrackingConfigureStatus
    {
        Success,
        UnsupportedExtension,
        UnsupportedCapability,
        PermissionDenied,
        InvalidLibrary,
        InvalidReferenceImage,
        FormatUnsupported,
        SizeMissing,
        LimitReached,
        ConfigurationCanceled,
        CleanupUnresolved,
        NativeFailure,
        Degraded
    }

    internal enum ImageTrackingModeIntent
    {
        Auto,
        RecognitionOnly,
        ContinuousTracking,
        StaticOptimized
    }

    internal enum XrSpatialImageTrackingMode
    {
        Recognition = 0,
        Tracking = 1
    }

    internal readonly struct ImageTrackingModeResolution
    {
        private ImageTrackingModeResolution(
            XrSpatialImageTrackingMode actualMode,
            bool isDegraded)
        {
            ActualMode = actualMode;
            IsDegraded = isDegraded;
        }

        internal XrSpatialImageTrackingMode ActualMode { get; }

        internal bool IsDegraded { get; }

        internal static ImageTrackingModeResolution Resolve(ImageTrackingModeIntent intent)
        {
            switch (intent)
            {
                case ImageTrackingModeIntent.RecognitionOnly:
                    return new ImageTrackingModeResolution(
                        XrSpatialImageTrackingMode.Recognition,
                        false);
                case ImageTrackingModeIntent.Auto:
                case ImageTrackingModeIntent.ContinuousTracking:
                case ImageTrackingModeIntent.StaticOptimized:
                    return new ImageTrackingModeResolution(
                        XrSpatialImageTrackingMode.Tracking,
                        false);
                default:
                    return new ImageTrackingModeResolution(
                        XrSpatialImageTrackingMode.Tracking,
                        true);
            }
        }
    }

    internal readonly struct ImageTrackingObservation
    {
        internal ImageTrackingObservation(
            Guid referenceImageGuid,
            Pose pose,
            TrackingState trackingState)
            : this(
                0,
                referenceImageGuid,
                pose,
                -1,
                0,
                Vector2.zero,
                Vector2.zero,
                false,
                trackingState,
                trackingState != TrackingState.None)
        {
        }

        internal ImageTrackingObservation(
            ulong entityId,
            Guid referenceImageGuid,
            Pose pose,
            Vector2 size,
            TrackingState trackingState)
            : this(
                entityId,
                referenceImageGuid,
                pose,
                -1,
                0,
                Vector2.zero,
                size,
                false,
                trackingState,
                trackingState != TrackingState.None)
        {
        }

        internal ImageTrackingObservation(
            ulong entityId,
            Guid referenceImageGuid,
            Pose pose,
            Vector2 boundingBoxOffset,
            Vector2 size,
            TrackingState trackingState)
            : this(
                entityId,
                referenceImageGuid,
                pose,
                -1,
                0,
                boundingBoxOffset,
                size,
                true,
                trackingState,
                trackingState != TrackingState.None)
        {
        }

        internal ImageTrackingObservation(
            ulong entityId,
            Guid referenceImageGuid,
            Pose pose,
            int referenceImageIndex,
            ulong imageTrackingDatabase,
            Vector2 boundingBoxOffset,
            Vector2 size,
            bool hasBoundingBox2D,
            TrackingState trackingState,
            bool hasValidPose = true,
            PXR_ImageTrackingAlignmentMode alignmentMode =
                PXR_ImageTrackingAlignmentMode.Original,
            bool isAligned = false,
            bool hasAlignedPose = false,
            float modelScale = 1.0f,
            long entityLastUpdateTime = 0L)
        {
            EntityId = entityId;
            ReferenceImageGuid = referenceImageGuid;
            Pose = pose;
            ReferenceImageIndex = referenceImageIndex;
            ImageTrackingDatabase = imageTrackingDatabase;
            BoundingBoxOffset = boundingBoxOffset;
            Size = size;
            HasBoundingBox2D = hasBoundingBox2D;
            TrackingState = trackingState;
            HasValidPose = hasValidPose;
            AlignmentMode = alignmentMode;
            IsAligned = isAligned;
            HasAlignedPose = hasAlignedPose;
            ModelScale = modelScale;
            EntityLastUpdateTime = entityLastUpdateTime;
        }

        internal ulong EntityId { get; }

        internal Guid ReferenceImageGuid { get; }

        internal Pose Pose { get; }

        internal int ReferenceImageIndex { get; }

        internal ulong ImageTrackingDatabase { get; }

        internal Vector2 BoundingBoxOffset { get; }

        internal Vector2 Size { get; }

        internal bool HasBoundingBox2D { get; }

        internal TrackingState TrackingState { get; }

        internal bool HasValidPose { get; }

        internal PXR_ImageTrackingAlignmentMode AlignmentMode { get; }

        internal bool IsAligned { get; }

        internal bool HasAlignedPose { get; }

        internal float ModelScale { get; }

        internal long EntityLastUpdateTime { get; }
    }
}
#endif
