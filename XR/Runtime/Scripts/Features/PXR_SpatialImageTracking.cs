#if AR_FOUNDATION_6_IMAGE_TRACKING
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    public static class PXR_SpatialImageTracking
    {
        private static readonly object s_Gate = new object();
        private static ImageTrackingProviderModule s_Module;

        internal static ImageTrackingProviderStatus Status
        {
            get
            {
                ImageTrackingProviderModule module;
                lock (s_Gate)
                {
                    module = s_Module;
                }

                return module == null
                    ? new ImageTrackingProviderStatus(
                        ImageTrackingProviderState.Uninitialized,
                        PxrResult.SUCCESS)
                    : module.Status;
            }
        }

        public static PXR_ImageTrackingMode GetTrackingMode()
        {
            return PXRImageTrackingOptionsRegistry.Current.Mode;
        }

        public static void SetTrackingMode(PXR_ImageTrackingMode mode)
        {
            PXRImageTrackingOptionsRegistry.SetMode(mode);
        }

        public static PXR_ImageTrackingAlignmentMode GetAlignmentMode()
        {
            return PXRImageTrackingOptionsRegistry.Current.AlignmentMode;
        }

        public static void SetAlignmentMode(PXR_ImageTrackingAlignmentMode mode)
        {
            PXRImageTrackingOptionsRegistry.SetAlignmentMode(mode);
        }

        public static PXR_ImageTrackingAlignmentInvalidPolicy
            GetAlignmentInvalidPolicy()
        {
            return PXRImageTrackingOptionsRegistry.Current.AlignmentInvalidPolicy;
        }

        public static void SetAlignmentInvalidPolicy(
            PXR_ImageTrackingAlignmentInvalidPolicy policy)
        {
            PXRImageTrackingOptionsRegistry.SetAlignmentInvalidPolicy(policy);
        }

        public static PXR_ImageTrackingLibraryHints GetLibraryHints()
        {
            return PXRImageTrackingOptionsRegistry.Current.LibraryHints;
        }

        public static void SetLibraryHints(PXR_ImageTrackingLibraryHints hints)
        {
            PXRImageTrackingOptionsRegistry.SetLibraryHints(hints);
        }

        public static bool TryGetMetadata(
            TrackableId trackableId,
            out PXR_TrackedImageMetadata metadata)
        {
            return PXRImageTrackingMetadataRegistry.TryGet(
                trackableId,
                out metadata);
        }

        public static PXR_TrackedImageMetadata[] GetMetadataSnapshot()
        {
            return PXRImageTrackingMetadataRegistry.Snapshot();
        }

        internal static void ClearMetadataForTests()
        {
            PXRImageTrackingMetadataRegistry.Clear();
        }

        internal static ImageTrackingProviderModule CreateModule(
            ISpatialImageTrackingNativeApi native)
        {
            return new ImageTrackingProviderModule(native);
        }

        internal static Task<ImageTrackingConfigureResult> ConfigureAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            CancellationToken cancellationToken)
        {
            ImageTrackingProviderModule module;
            lock (s_Gate)
            {
                if (s_Module == null)
                {
                    s_Module = CreateModule(
                        SpatialImageTrackingNativeApi.Instance);
                }

                module = s_Module;
            }

            return module.ConfigureAsync(library, options, cancellationToken);
        }

        internal static PxrResult SetTrackingEnabled(bool enabled)
        {
            ImageTrackingProviderModule module;
            lock (s_Gate)
            {
                module = s_Module;
            }

            return module == null
                ? PxrResult.ERROR_CALL_ORDER_INVALID
                : module.SetTrackingEnabled(enabled);
        }

        internal static ImageTrackingObservation[] AcquireObservations()
        {
            ImageTrackingProviderModule module;
            lock (s_Gate)
            {
                module = s_Module;
            }

            return module == null
                ? System.Array.Empty<ImageTrackingObservation>()
                : module.LocateTrackedImages();
        }

        internal static ulong[] AcquireRemovedEntityIds()
        {
            ImageTrackingProviderModule module;
            lock (s_Gate)
            {
                module = s_Module;
            }

            return module == null
                ? System.Array.Empty<ulong>()
                : module.AcquireRemovedEntityIds();
        }

        internal static Task<PxrResult> QueryAsync(
            CancellationToken cancellationToken)
        {
            ImageTrackingProviderModule module;
            lock (s_Gate)
            {
                module = s_Module;
            }

            return module == null
                ? Task.FromResult(PxrResult.ERROR_CALL_ORDER_INVALID)
                : module.QueryAsync(cancellationToken);
        }

        internal static Task<PxrResult> QueryAsync(
            long providerGeneration,
            CancellationToken cancellationToken)
        {
            ImageTrackingProviderModule module;
            lock (s_Gate)
            {
                module = s_Module;
            }

            return module == null
                ? Task.FromResult(PxrResult.ERROR_CALL_ORDER_INVALID)
                : module.QueryAsync(
                    providerGeneration,
                    cancellationToken);
        }

        internal static PxrResult Destroy()
        {
            ImageTrackingProviderModule module;
            lock (s_Gate)
            {
                module = s_Module;
            }

            if (module == null)
            {
                return PxrResult.SUCCESS;
            }

            PxrResult result = module.Destroy();
            if (result == PxrResult.SUCCESS)
            {
                PXRImageTrackingMetadataRegistry.Clear();
                lock (s_Gate)
                {
                    if (ReferenceEquals(s_Module, module))
                    {
                        s_Module = null;
                    }
                }
            }

            return result;
        }

        internal static bool HandleProviderStateChanged(
            long providerGeneration,
            PxrSenseDataProviderState providerState)
        {
            ImageTrackingProviderModule module;
            lock (s_Gate)
            {
                module = s_Module;
            }

            return module != null
                   && module.HandleProviderStateChanged(
                       providerGeneration,
                       providerState);
        }

        internal static void Abandon()
        {
            ImageTrackingProviderModule module;
            lock (s_Gate)
            {
                module = s_Module;
                s_Module = null;
            }

            module?.Abandon();
            PXRImageTrackingMetadataRegistry.Clear();
        }
    }
}
#endif
