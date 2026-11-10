#if AR_FOUNDATION_6_IMAGE_TRACKING
namespace ByteDance.PICO.XR
{
    /// <summary>
    /// Selects the PICO native image tracking provider mode used behind
    /// ARFoundation image tracking.
    /// </summary>
    public enum PXR_ImageTrackingMode
    {
        /// <summary>
        /// Request continuous pose tracking after image recognition.
        /// </summary>
        Tracking = 0,

        /// <summary>
        /// Request recognition-only mode from the PICO runtime.
        /// </summary>
        RecognitionOnly = 1
    }
}
#endif
