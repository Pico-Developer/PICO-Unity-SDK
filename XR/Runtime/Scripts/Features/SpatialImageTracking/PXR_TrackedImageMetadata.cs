#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    /// <summary>
    /// PICO-specific metadata associated with an ARFoundation tracked image.
    /// </summary>
    public readonly struct PXR_TrackedImageMetadata
    {
        public PXR_TrackedImageMetadata(
            TrackableId trackableId,
            Guid sourceImageGuid,
            ulong picoEntityId,
            int referenceImageIndex,
            Vector2 boundingBoxOffset,
            Vector2 boundingBoxExtent,
            bool hasBoundingBox2D,
            bool hasValidPose = true,
            PXR_ImageTrackingAlignmentMode alignmentMode =
                PXR_ImageTrackingAlignmentMode.Original,
            bool isAligned = false,
            bool hasAlignedPose = false,
            float modelScale = 1.0f)
            : this(
                trackableId,
                sourceImageGuid,
                picoEntityId,
                referenceImageIndex,
                boundingBoxOffset,
                boundingBoxExtent,
                hasBoundingBox2D,
                hasValidPose,
                alignmentMode,
                isAligned,
                hasAlignedPose,
                modelScale,
                0L)
        {
        }

        internal PXR_TrackedImageMetadata(
            TrackableId trackableId,
            Guid sourceImageGuid,
            ulong picoEntityId,
            int referenceImageIndex,
            Vector2 boundingBoxOffset,
            Vector2 boundingBoxExtent,
            bool hasBoundingBox2D,
            bool hasValidPose,
            PXR_ImageTrackingAlignmentMode alignmentMode,
            bool isAligned,
            bool hasAlignedPose,
            float modelScale,
            long entityLastUpdateTime)
        {
            TrackableId = trackableId;
            SourceImageGuid = sourceImageGuid;
            PicoEntityId = picoEntityId;
            ReferenceImageIndex = referenceImageIndex;
            BoundingBoxOffset = boundingBoxOffset;
            BoundingBoxExtent = boundingBoxExtent;
            HasBoundingBox2D = hasBoundingBox2D;
            HasValidPose = hasValidPose;
            AlignmentMode = alignmentMode;
            IsAligned = isAligned;
            HasAlignedPose = hasAlignedPose;
            ModelScale = modelScale;
            EntityLastUpdateTime = entityLastUpdateTime;
        }

        public TrackableId TrackableId { get; }

        public Guid SourceImageGuid { get; }

        public ulong PicoEntityId { get; }

        public int ReferenceImageIndex { get; }

        public Vector2 BoundingBoxOffset { get; }

        public Vector2 BoundingBoxExtent { get; }

        public bool HasBoundingBox2D { get; }

        public bool HasValidPose { get; }

        public PXR_ImageTrackingAlignmentMode AlignmentMode { get; }

        public bool IsAligned { get; }

        public bool HasAlignedPose { get; }

        public float ModelScale { get; }

        /// <summary>
        /// Gets the raw OpenXR <c>XrTime</c> at which the runtime last updated
        /// the spatial entity backing this tracked image.
        /// </summary>
        /// <remarks>
        /// The value is a signed 64-bit nanosecond timestamp in the OpenXR
        /// runtime clock domain. A value of <c>0</c> means the entity update
        /// time is unavailable, including metadata created with the default
        /// value or the public compatibility constructor. This is not wall-clock
        /// time and is not the pose locate <c>predictedDisplayTime</c>.
        /// </remarks>
        public long EntityLastUpdateTime { get; }
    }
}
#endif
