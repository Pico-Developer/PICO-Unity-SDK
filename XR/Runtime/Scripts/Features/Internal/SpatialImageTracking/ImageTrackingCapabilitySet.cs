#if AR_FOUNDATION_6_IMAGE_TRACKING
namespace ByteDance.PICO.XR
{
    internal readonly struct ImageTrackingCapabilitySet
    {
        internal const int V1MinimumImageDimension = 250;
        internal const int V1RecommendedMinimumImageDimension = 300;
        internal const int V1MaximumImageDimension = 640;
        internal const int V1MaximumReferenceImageCount = 300;
        internal const int V1MaximumMovingImageCount = 20;

        internal ImageTrackingCapabilitySet(
            int minimumImageDimension,
            int recommendedMinimumImageDimension,
            int maximumImageDimension,
            int maximumReferenceImageCount,
            int maximumMovingImageCount)
        {
            MinimumImageDimension = minimumImageDimension;
            RecommendedMinimumImageDimension = recommendedMinimumImageDimension;
            MaximumImageDimension = maximumImageDimension;
            MaximumReferenceImageCount = maximumReferenceImageCount;
            MaximumMovingImageCount = maximumMovingImageCount;
        }

        internal static ImageTrackingCapabilitySet V1 { get; } =
            new ImageTrackingCapabilitySet(
                V1MinimumImageDimension,
                V1RecommendedMinimumImageDimension,
                V1MaximumImageDimension,
                V1MaximumReferenceImageCount,
                V1MaximumMovingImageCount);

        internal int MinimumImageDimension { get; }

        internal int RecommendedMinimumImageDimension { get; }

        internal int MaximumImageDimension { get; }

        internal int MaximumReferenceImageCount { get; }

        internal int MaximumMovingImageCount { get; }

        internal bool SupportsDimensions(int width, int height)
        {
            return width > 0
                && width <= MaximumImageDimension
                && height > 0
                && height <= MaximumImageDimension;
        }

        internal bool RequiresDimensionWarning(int width, int height)
        {
            return width < RecommendedMinimumImageDimension
                || height < RecommendedMinimumImageDimension;
        }
    }
}
#endif
