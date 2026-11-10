#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    internal readonly struct ImageTrackingChanges
    {
        internal ImageTrackingChanges(
            XRTrackedImage[] added,
            XRTrackedImage[] updated,
            TrackableId[] removed)
        {
            Added = added ?? Array.Empty<XRTrackedImage>();
            Updated = updated ?? Array.Empty<XRTrackedImage>();
            Removed = removed ?? Array.Empty<TrackableId>();
        }

        internal XRTrackedImage[] Added { get; }

        internal XRTrackedImage[] Updated { get; }

        internal TrackableId[] Removed { get; }
    }

    internal interface IImageTrackingBackend : IDisposable
    {
        string BackendId { get; }

        ImageTrackingCapabilitySet Capabilities { get; }

        long RuntimeGeneration { get; }

        ImageTrackingRuntimeLibrary CreateRuntimeLibrary(
            XRReferenceImageLibrary source);

        Task<PxrResult> EnsureStartPreconditionsAsync(
            CancellationToken cancellationToken);

        Task<ImageTrackingConfigureResult> ConfigureAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            CancellationToken cancellationToken);

        void SetTrackingEnabled(bool enabled);

        ImageTrackingChanges AcquireChanges();

        bool TryGetMetadata(
            TrackableId trackableId,
            out PXR_TrackedImageMetadata metadata);

        PXR_TrackedImageMetadata[] GetMetadataSnapshot();

        void HandleRuntimeEvent(IntPtr eventData);
    }
}
#endif
