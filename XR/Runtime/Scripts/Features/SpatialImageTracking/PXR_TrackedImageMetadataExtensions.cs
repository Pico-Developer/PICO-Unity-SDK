#if AR_FOUNDATION_6_IMAGE_TRACKING
using UnityEngine.XR.ARFoundation;

namespace ByteDance.PICO.XR
{
    /// <summary>
    /// ARFoundation helpers for reading PICO-specific tracked image metadata.
    /// </summary>
    public static class PXR_TrackedImageMetadataExtensions
    {
        public static bool TryGetPicoMetadata(
            this ARTrackedImage trackedImage,
            out PXR_TrackedImageMetadata metadata)
        {
            if (trackedImage == null)
            {
                metadata = default;
                return false;
            }

            return PXR_SpatialImageTracking.TryGetMetadata(
                trackedImage.trackableId,
                out metadata);
        }
    }
}
#endif
