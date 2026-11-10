#if AR_FOUNDATION_6_IMAGE_TRACKING
namespace ByteDance.PICO.XR
{
    internal static class ImageTrackingBackendFactory
    {
        internal static IImageTrackingBackend Create()
        {
            return new PicoSpatialImageTrackingBackend();
        }
    }
}
#endif
