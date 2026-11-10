#if AR_FOUNDATION_6_IMAGE_TRACKING
namespace ByteDance.PICO.XR
{
    /// <summary>
    /// Selects how PICO ImageTracking locates recognized image anchors.
    /// </summary>
    public enum PXR_ImageTrackingAlignmentMode
    {
        /// <summary>
        /// Use the original anchor location from xrLocateAnchorPICO.
        /// </summary>
        Original = 0,

        /// <summary>
        /// Use passthrough-aligned position and orientation without applying model scale.
        /// </summary>
        AlignedPosition = 1,

        /// <summary>
        /// Use passthrough-aligned position and orientation and expose runtime model scale.
        /// </summary>
        AlignedPositionAndScale = 2
    }
}
#endif
