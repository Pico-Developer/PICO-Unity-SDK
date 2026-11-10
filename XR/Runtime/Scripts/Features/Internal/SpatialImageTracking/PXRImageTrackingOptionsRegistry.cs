#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    internal readonly struct PXRImageTrackingOptions
    {
        internal PXRImageTrackingOptions(
            PXR_ImageTrackingMode mode,
            PXR_ImageTrackingLibraryHints libraryHints,
            PXR_ImageTrackingAlignmentMode alignmentMode =
                PXR_ImageTrackingAlignmentMode.Original,
            PXR_ImageTrackingAlignmentInvalidPolicy alignmentInvalidPolicy =
                PXR_ImageTrackingAlignmentInvalidPolicy.Hide)
        {
            Mode = mode;
            LibraryHints = libraryHints;
            AlignmentMode = alignmentMode;
            AlignmentInvalidPolicy = alignmentInvalidPolicy;
        }

        internal static PXRImageTrackingOptions Default { get; } =
            new PXRImageTrackingOptions(
                PXR_ImageTrackingMode.Tracking,
                null,
                PXR_ImageTrackingAlignmentMode.Original,
                PXR_ImageTrackingAlignmentInvalidPolicy.Hide);

        internal PXR_ImageTrackingMode Mode { get; }

        internal PXR_ImageTrackingLibraryHints LibraryHints { get; }

        internal PXR_ImageTrackingAlignmentMode AlignmentMode { get; }

        internal PXR_ImageTrackingAlignmentInvalidPolicy AlignmentInvalidPolicy { get; }

        internal ImageTrackingModeIntent ToModeIntent()
        {
            switch (Mode)
            {
                case PXR_ImageTrackingMode.RecognitionOnly:
                    return ImageTrackingModeIntent.RecognitionOnly;
                case PXR_ImageTrackingMode.Tracking:
                default:
                    return ImageTrackingModeIntent.ContinuousTracking;
            }
        }
    }

    internal static class PXRImageTrackingOptionsRegistry
    {
        private static readonly object s_Gate = new object();
        private static PXRImageTrackingOptions s_Current =
            PXRImageTrackingOptions.Default;

        internal static event Action OptionsChanged;

        internal static event Action AlignmentOptionsChanged;

        internal static PXRImageTrackingOptions Current
        {
            get
            {
                lock (s_Gate)
                {
                    return s_Current;
                }
            }
        }

        internal static void SetMode(PXR_ImageTrackingMode mode)
        {
            PXRImageTrackingOptions current = Current;
            SetOptions(
                new PXRImageTrackingOptions(
                    mode,
                    current.LibraryHints,
                    current.AlignmentMode,
                    current.AlignmentInvalidPolicy));
        }

        internal static void SetLibraryHints(PXR_ImageTrackingLibraryHints hints)
        {
            PXRImageTrackingOptions current = Current;
            SetOptions(
                new PXRImageTrackingOptions(
                    current.Mode,
                    hints,
                    current.AlignmentMode,
                    current.AlignmentInvalidPolicy));
        }

        internal static void SetAlignmentMode(
            PXR_ImageTrackingAlignmentMode mode)
        {
            bool changed;
            lock (s_Gate)
            {
                changed = s_Current.AlignmentMode != mode;
                if (changed)
                {
                    s_Current = new PXRImageTrackingOptions(
                        s_Current.Mode,
                        s_Current.LibraryHints,
                        mode,
                        s_Current.AlignmentInvalidPolicy);
                }
            }

            if (changed)
            {
                AlignmentOptionsChanged?.Invoke();
            }
        }

        internal static void SetAlignmentInvalidPolicy(
            PXR_ImageTrackingAlignmentInvalidPolicy policy)
        {
            bool changed;
            lock (s_Gate)
            {
                changed = s_Current.AlignmentInvalidPolicy != policy;
                if (changed)
                {
                    s_Current = new PXRImageTrackingOptions(
                        s_Current.Mode,
                        s_Current.LibraryHints,
                        s_Current.AlignmentMode,
                        policy);
                }
            }

            if (changed)
            {
                AlignmentOptionsChanged?.Invoke();
            }
        }

        internal static void SetOptions(PXRImageTrackingOptions options)
        {
            bool providerOptionsChanged;
            bool alignmentOptionsChanged;
            lock (s_Gate)
            {
                providerOptionsChanged =
                    s_Current.Mode != options.Mode
                    || !ReferenceEquals(
                        s_Current.LibraryHints,
                        options.LibraryHints);
                alignmentOptionsChanged =
                    s_Current.AlignmentMode != options.AlignmentMode
                    || s_Current.AlignmentInvalidPolicy
                       != options.AlignmentInvalidPolicy;
                if (providerOptionsChanged || alignmentOptionsChanged)
                {
                    s_Current = options;
                }
            }

            if (providerOptionsChanged)
            {
                OptionsChanged?.Invoke();
            }

            if (alignmentOptionsChanged)
            {
                AlignmentOptionsChanged?.Invoke();
            }
        }

        internal static bool TryGetOptimizeForStaticImage(
            Guid referenceImageGuid,
            string referenceImageName,
            out bool optimizeForStaticImage)
        {
            PXR_ImageTrackingLibraryHints hints = Current.LibraryHints;
            if (hints == null)
            {
                optimizeForStaticImage = false;
                return false;
            }

            return hints.TryGetOptimizeForStaticImage(
                referenceImageGuid,
                referenceImageName,
                out optimizeForStaticImage);
        }

        internal static void ResetForTests()
        {
            lock (s_Gate)
            {
                s_Current = PXRImageTrackingOptions.Default;
            }

            OptionsChanged = null;
            AlignmentOptionsChanged = null;
        }
    }

    internal static class PXRImageTrackingMetadataRegistry
    {
        private static readonly object s_Gate = new object();
        private static readonly System.Collections.Generic.Dictionary<TrackableId, PXR_TrackedImageMetadata>
            s_Metadata = new System.Collections.Generic.Dictionary<TrackableId, PXR_TrackedImageMetadata>();

        internal static void Upsert(PXR_TrackedImageMetadata metadata)
        {
            lock (s_Gate)
            {
                s_Metadata[metadata.TrackableId] = metadata;
            }
        }

        internal static bool TryGet(
            TrackableId trackableId,
            out PXR_TrackedImageMetadata metadata)
        {
            lock (s_Gate)
            {
                if (s_Metadata.TryGetValue(trackableId, out metadata))
                {
                    return true;
                }
            }

            metadata = new PXR_TrackedImageMetadata(
                TrackableId.invalidId,
                Guid.Empty,
                0,
                -1,
                UnityEngine.Vector2.zero,
                UnityEngine.Vector2.zero,
                false,
                false);
            return false;
        }

        internal static PXR_TrackedImageMetadata[] Snapshot()
        {
            lock (s_Gate)
            {
                var values = new PXR_TrackedImageMetadata[s_Metadata.Count];
                s_Metadata.Values.CopyTo(values, 0);
                return values;
            }
        }

        internal static void Remove(TrackableId trackableId)
        {
            lock (s_Gate)
            {
                s_Metadata.Remove(trackableId);
            }
        }

        internal static void Clear()
        {
            lock (s_Gate)
            {
                s_Metadata.Clear();
            }
        }
    }
}
#endif
