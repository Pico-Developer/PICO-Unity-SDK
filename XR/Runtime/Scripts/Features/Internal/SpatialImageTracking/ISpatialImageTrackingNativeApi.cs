#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ByteDance.PICO.XR
{
    internal readonly struct SpatialImageTrackingLocation
    {
        internal SpatialImageTrackingLocation(ulong locationFlags, PxrPosef pose)
        {
            LocationFlags = locationFlags;
            Pose = pose;
        }

        internal ulong LocationFlags { get; }

        internal PxrPosef Pose { get; }
    }

    internal readonly struct SpatialImageTrackingAlignedLocation
    {
        internal SpatialImageTrackingAlignedLocation(
            ulong locationFlags,
            PxrPosef pose,
            bool alignedValid,
            float modelScale)
        {
            LocationFlags = locationFlags;
            Pose = pose;
            AlignedValid = alignedValid;
            ModelScale = modelScale;
        }

        internal ulong LocationFlags { get; }

        internal PxrPosef Pose { get; }

        internal bool AlignedValid { get; }

        internal float ModelScale { get; }
    }

    internal interface ISpatialImageTrackingNativeApi
    {
        PxrResult CreateProvider(
            XrSpatialImageTrackingMode mode,
            out ulong provider);

        PxrResult EnumerateFormats(
            out XrSpatialReferenceImageFormat[] formats);

        PxrResult CreateDatabaseAsync(
            ulong provider,
            ref XrSpatialImageTrackingDatabaseCreateInfo createInfo,
            out ulong future);

        PxrResult CreateDatabaseComplete(
            ulong provider,
            ulong future,
            out XrCreateSpatialImageTrackingDatabaseCompletion completion);

        PxrResult DestroyDatabase(ulong database);

        PxrResult StartProviderAsync(ulong provider, out ulong future);

        PxrResult StartProviderComplete(
            ulong future,
            out XrSenseDataProviderStartCompletion completion);

        PxrResult StopProvider(ulong provider);

        PxrResult DestroyProvider(ulong provider);

        PxrResult PollFuture(ulong future, out PxrFutureState state);

        PxrResult CancelFuture(ulong future);

        PxrResult QueryAsync(ulong provider, out ulong future);

        PxrResult QueryComplete(
            ulong provider,
            ulong future,
            out XrSenseDataQueryCompletion completion);

        PxrResult GetQueriedSenseData(
            ulong provider,
            ulong snapshot,
            out IReadOnlyList<PxrQueriedSpatialEntityInfo> entities);

        PxrResult DestroySnapshot(ulong snapshot);

        PxrResult GetImageTrackingData(
            ulong snapshot,
            ulong entity,
            out XrSpatialImageTrackingData data);

        PxrResult GetBoundingBox2D(
            ulong snapshot,
            ulong entity,
            out Vector2 offset,
            out Vector2 extent);

        PxrResult RetrieveAnchor(
            ulong snapshot,
            ulong entity,
            out ulong anchor);

        PxrResult LocateAnchor(
            ulong anchor,
            out SpatialImageTrackingLocation location);

        PxrResult LocateAnchorAligned(
            ulong anchor,
            out SpatialImageTrackingAlignedLocation location);

        PxrResult DestroyAnchor(ulong anchor);
    }

    internal sealed class SpatialImageTrackingNativeApi : ISpatialImageTrackingNativeApi
    {
        internal static SpatialImageTrackingNativeApi Instance { get; } =
            new SpatialImageTrackingNativeApi(
                PXR_SpatialImageTrackingPlugin.CreateProvider);

        private readonly PXR_Plugin.MixedReality
            .SpatialImageTrackingProviderCreateDelegate m_CreateProvider;

        internal SpatialImageTrackingNativeApi(
            PXR_Plugin.MixedReality
                .SpatialImageTrackingProviderCreateDelegate createProvider)
        {
            m_CreateProvider = createProvider
                ?? throw new ArgumentNullException(nameof(createProvider));
        }

        public PxrResult CreateProvider(
            XrSpatialImageTrackingMode mode,
            out ulong provider)
        {
            PxrResult result = m_CreateProvider(mode, out provider);
            if (result == PxrResult.SUCCESS)
            {
                PXR_Plugin.MixedReality.ImageTrackingProviderHandle = provider;
            }

            return result;
        }

        public PxrResult EnumerateFormats(
            out XrSpatialReferenceImageFormat[] formats)
        {
            return PXR_SpatialImageTrackingPlugin.EnumerateFormats(out formats);
        }

        public PxrResult CreateDatabaseAsync(
            ulong provider,
            ref XrSpatialImageTrackingDatabaseCreateInfo createInfo,
            out ulong future)
        {
            return PXR_SpatialImageTrackingPlugin.CreateDatabaseAsync(
                provider,
                ref createInfo,
                out future);
        }

        public PxrResult CreateDatabaseComplete(
            ulong provider,
            ulong future,
            out XrCreateSpatialImageTrackingDatabaseCompletion completion)
        {
            return PXR_SpatialImageTrackingPlugin.CreateDatabaseComplete(
                provider,
                future,
                out completion);
        }

        public PxrResult DestroyDatabase(ulong database)
        {
            return PXR_SpatialImageTrackingPlugin.DestroyDatabase(database);
        }

        public PxrResult StartProviderAsync(ulong provider, out ulong future)
        {
            return PXR_Plugin.MixedReality.UPxr_StartSenseDataProviderAsync(
                provider,
                out future);
        }

        public PxrResult StartProviderComplete(
            ulong future,
            out XrSenseDataProviderStartCompletion completion)
        {
            return PXR_Plugin.MixedReality.UPxr_StartSenseDataProviderComplete(
                future,
                out completion);
        }

        public PxrResult StopProvider(ulong provider)
        {
            return PXR_Plugin.MixedReality.UPxr_StopSenseDataProvider(provider);
        }

        public PxrResult DestroyProvider(ulong provider)
        {
            if (provider
                == PXR_Plugin.MixedReality.ImageTrackingProviderHandle)
            {
                return PXR_Plugin.MixedReality
                    .UPxr_DestroySpatialImageTrackingSenseDataProvider();
            }

            return PXR_Plugin.MixedReality.UPxr_DestroySenseDataProvider(provider);
        }

        public PxrResult PollFuture(ulong future, out PxrFutureState state)
        {
            return PXR_Plugin.MixedReality.UPxr_PollFuture(future, out state);
        }

        public PxrResult CancelFuture(ulong future)
        {
            return PXR_Plugin.MixedReality.UPxr_CancelFuture(future);
        }

        public PxrResult QueryAsync(ulong provider, out ulong future)
        {
            var queryInfo = new XrSenseDataQueryInfo
            {
                type = XrStructureType.XR_TYPE_SENSE_DATA_QUERY_INFO,
                filter = IntPtr.Zero
            };
            return PXR_Plugin.MixedReality.UPxr_QuerySenseDataAsync(
                provider,
                ref queryInfo,
                out future);
        }

        public PxrResult QueryComplete(
            ulong provider,
            ulong future,
            out XrSenseDataQueryCompletion completion)
        {
            return PXR_Plugin.MixedReality.UPxr_QuerySenseDataComplete(
                provider,
                future,
                out completion);
        }

        public PxrResult GetQueriedSenseData(
            ulong provider,
            ulong snapshot,
            out IReadOnlyList<PxrQueriedSpatialEntityInfo> entities)
        {
            PxrResult result = PXR_Plugin.MixedReality.UPxr_GetQueriedSenseData(
                provider,
                snapshot,
                out List<PxrQueriedSpatialEntityInfo> queriedEntities);
            entities = queriedEntities;
            return result;
        }

        public PxrResult DestroySnapshot(ulong snapshot)
        {
            return PXR_Plugin.MixedReality.UPxr_DestroySenseDataQueryResult(snapshot);
        }

        public PxrResult GetImageTrackingData(
            ulong snapshot,
            ulong entity,
            out XrSpatialImageTrackingData data)
        {
            return PXR_SpatialImageTrackingPlugin.GetImageTrackingData(
                snapshot,
                entity,
                out data);
        }

        public PxrResult GetBoundingBox2D(
            ulong snapshot,
            ulong entity,
            out Vector2 offset,
            out Vector2 extent)
        {
            return PXR_Plugin.MixedReality.UPxr_GetSpatialEntityBox2DInfo(
                snapshot,
                entity,
                out offset,
                out extent);
        }

        public PxrResult RetrieveAnchor(
            ulong snapshot,
            ulong entity,
            out ulong anchor)
        {
            return PXR_Plugin.MixedReality.UPxr_RetrieveSpatialEntityAnchor(
                snapshot,
                entity,
                out anchor);
        }

        public PxrResult LocateAnchor(
            ulong anchor,
            out SpatialImageTrackingLocation location)
        {
            PxrResult result = PXR_SpatialImageTrackingPlugin.LocateAnchorRaw(
                anchor,
                out XrSpaceLocation nativeLocation);
            location = new SpatialImageTrackingLocation(
                nativeLocation.locationFlags,
                nativeLocation.pose);
            return result;
        }

        public PxrResult LocateAnchorAligned(
            ulong anchor,
            out SpatialImageTrackingAlignedLocation location)
        {
            PxrResult result = PXR_SpatialImageTrackingPlugin.LocateAnchorAlignedRaw(
                anchor,
                out XrSpaceLocationAligned nativeLocation,
                out float modelScale);
            location = new SpatialImageTrackingAlignedLocation(
                nativeLocation.locationFlags,
                nativeLocation.pose,
                nativeLocation.valid != 0,
                modelScale);
            return result;
        }

        public PxrResult DestroyAnchor(ulong anchor)
        {
            return PXR_Plugin.MixedReality.UPxr_DestroyAnchor(anchor);
        }
    }
}
#endif
