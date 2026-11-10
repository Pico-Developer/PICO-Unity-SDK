#if AR_FOUNDATION_6_IMAGE_TRACKING
namespace ByteDance.PICO.XR
{
    /// <summary>
    /// Selects behavior when aligned location is unavailable for a frame.
    /// </summary>
    public enum PXR_ImageTrackingAlignmentInvalidPolicy
    {
        /// <summary>
        /// Report the tracked image as not having a valid aligned pose for the frame.
        /// </summary>
        Hide = 0,

        /// <summary>
        /// Reuse the last valid aligned pose and report the image as limited.
        /// </summary>
        HoldLastAlignedPose = 1,

        /// <summary>
        /// Fall back to the original unaligned anchor location for the frame.
        /// </summary>
        UseOriginalFallback = 2
    }
}
#endif
