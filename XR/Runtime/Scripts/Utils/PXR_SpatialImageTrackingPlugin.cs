#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Runtime.InteropServices;

namespace ByteDance.PICO.XR
{
    internal static class PXR_SpatialImageTrackingPlugin
    {
        [DllImport(PXR_Plugin.PXR_PLATFORM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern PxrResult Pxr_CreateSpatialImageTrackingSenseDataProvider(
            XrSpatialImageTrackingMode mode,
            out ulong provider);

        [DllImport(PXR_Plugin.PXR_PLATFORM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern PxrResult Pxr_CreateSpatialImageTrackingDatabaseAsync(
            ulong provider,
            ref XrSpatialImageTrackingDatabaseCreateInfo createInfo,
            out ulong future);

        [DllImport(PXR_Plugin.PXR_PLATFORM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern PxrResult Pxr_CreateSpatialImageTrackingDatabaseComplete(
            ulong provider,
            ulong future,
            ref XrCreateSpatialImageTrackingDatabaseCompletion completion);

        [DllImport(PXR_Plugin.PXR_PLATFORM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern PxrResult Pxr_DestroySpatialImageTrackingDatabase(ulong database);

        [DllImport(PXR_Plugin.PXR_PLATFORM_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern PxrResult Pxr_EnumerateSpatialReferenceImageFormats(
            uint capacity,
            ref uint count,
            IntPtr formats);

        [DllImport(
            PXR_Plugin.PXR_PLATFORM_DLL,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "Pxr_GetSpatialEntityComponentInfo")]
        private static extern int Pxr_GetSpatialEntityImageTrackingInfo(
            ulong snapshot,
            ref XrSpatialEntityImageTrackingGetInfo getInfo,
            ref XrSpatialEntityComponentDataImageTracking data);

        [DllImport(
            PXR_Plugin.PXR_PLATFORM_DLL,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "Pxr_LocateAnchorRaw")]
        private static extern int Pxr_LocateImageTrackingAnchor(
            ulong anchor,
            ref XrSpaceLocation location);

        [DllImport(
            PXR_Plugin.PXR_PLATFORM_DLL,
            CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "Pxr_LocateAnchorAligned")]
        private static extern int Pxr_LocateImageTrackingAnchorAligned(
            ulong anchor,
            ref XrSpaceLocationAligned location,
            ref XrSpaceLocationModelScale modelScale);

        internal static PxrResult CreateProvider(
            XrSpatialImageTrackingMode mode,
            out ulong provider)
        {
#if (UNITY_ANDROID && !UNITY_EDITOR) || PICO_LIVE_PREVIEW
            return Pxr_CreateSpatialImageTrackingSenseDataProvider(mode, out provider);
#else
            provider = 0;
            return PxrResult.ERROR_RUNTIME_FAILURE;
#endif
        }

        internal static PxrResult CreateDatabaseAsync(
            ulong provider,
            ref XrSpatialImageTrackingDatabaseCreateInfo info,
            out ulong future)
        {
#if (UNITY_ANDROID && !UNITY_EDITOR) || PICO_LIVE_PREVIEW
            return Pxr_CreateSpatialImageTrackingDatabaseAsync(provider, ref info, out future);
#else
            future = 0;
            return PxrResult.ERROR_RUNTIME_FAILURE;
#endif
        }

        internal static PxrResult CreateDatabaseComplete(
            ulong provider,
            ulong future,
            out XrCreateSpatialImageTrackingDatabaseCompletion completion)
        {
            completion = new XrCreateSpatialImageTrackingDatabaseCompletion
            {
                type = XrStructureType.XR_TYPE_CREATE_SPATIAL_IMAGE_TRACKING_DATABASE_COMPLETION_PICO
            };
#if (UNITY_ANDROID && !UNITY_EDITOR) || PICO_LIVE_PREVIEW
            return Pxr_CreateSpatialImageTrackingDatabaseComplete(
                provider,
                future,
                ref completion);
#else
            return PxrResult.ERROR_RUNTIME_FAILURE;
#endif
        }

        internal static PxrResult DestroyDatabase(ulong database)
        {
#if (UNITY_ANDROID && !UNITY_EDITOR) || PICO_LIVE_PREVIEW
            return Pxr_DestroySpatialImageTrackingDatabase(database);
#else
            return PxrResult.ERROR_RUNTIME_FAILURE;
#endif
        }

        internal static PxrResult EnumerateFormats(
            out XrSpatialReferenceImageFormat[] formats)
        {
            formats = Array.Empty<XrSpatialReferenceImageFormat>();
#if (UNITY_ANDROID && !UNITY_EDITOR) || PICO_LIVE_PREVIEW
            uint count = 0;
            PxrResult result = Pxr_EnumerateSpatialReferenceImageFormats(
                0,
                ref count,
                IntPtr.Zero);
            if (result != PxrResult.SUCCESS || count == 0)
            {
                return result;
            }

            const int elementSize = sizeof(int);
            IntPtr nativeFormats = Marshal.AllocHGlobal(checked((int)count * elementSize));
            try
            {
                uint outputCount = count;
                result = Pxr_EnumerateSpatialReferenceImageFormats(
                    count,
                    ref outputCount,
                    nativeFormats);
                if (result != PxrResult.SUCCESS)
                {
                    return result;
                }

                if (outputCount > count)
                {
                    return PxrResult.ERROR_SIZE_INSUFFICIENT;
                }

                formats = new XrSpatialReferenceImageFormat[outputCount];
                for (int index = 0; index < outputCount; index++)
                {
                    formats[index] = (XrSpatialReferenceImageFormat)Marshal.ReadInt32(
                        nativeFormats,
                        checked(index * elementSize));
                }

                return PxrResult.SUCCESS;
            }
            finally
            {
                Marshal.FreeHGlobal(nativeFormats);
            }
#else
            return PxrResult.ERROR_RUNTIME_FAILURE;
#endif
        }

        internal static PxrResult GetImageTrackingData(
            ulong snapshot,
            ulong entity,
            out XrSpatialImageTrackingData data)
        {
            var getInfo = new XrSpatialEntityImageTrackingGetInfo
            {
                type = XrStructureType.XR_TYPE_SPATIAL_ENTITY_IMAGE_TRACKING_GET_INFO_PICO,
                entity = entity,
                componentType = XrSpatialEntityComponentType.ImageTracking
            };
            var componentData = new XrSpatialEntityComponentDataImageTracking
            {
                type = XrStructureType.XR_TYPE_SPATIAL_ENTITY_COMPONENT_DATA_IMAGE_TRACKING_PICO
            };
#if (UNITY_ANDROID && !UNITY_EDITOR) || PICO_LIVE_PREVIEW
            PxrResult result = PXR_Plugin.MixedReality.UPxr_ConvertIntToPxrResult(
                Pxr_GetSpatialEntityImageTrackingInfo(
                    snapshot,
                    ref getInfo,
                    ref componentData));
            data = componentData.data;
            return result;
#else
            data = default;
            return PxrResult.ERROR_RUNTIME_FAILURE;
#endif
        }

        internal static PxrResult LocateAnchorRaw(
            ulong anchor,
            out XrSpaceLocation location)
        {
            location = new XrSpaceLocation
            {
                type = XrStructureType.XR_TYPE_SPACE_LOCATION
            };
#if (UNITY_ANDROID && !UNITY_EDITOR) || PICO_LIVE_PREVIEW
            return PXR_Plugin.MixedReality.UPxr_ConvertIntToPxrResult(
                Pxr_LocateImageTrackingAnchor(anchor, ref location));
#else
            return PxrResult.ERROR_RUNTIME_FAILURE;
#endif
        }

        internal static PxrResult LocateAnchorAlignedRaw(
            ulong anchor,
            out XrSpaceLocationAligned location,
            out float modelScale)
        {
            location = new XrSpaceLocationAligned
            {
                type = XrStructureType.XR_TYPE_SPACE_LOCATION_ALIGNED_PICO
            };
            var scale = new XrSpaceLocationModelScale
            {
                type = XrStructureType.XR_TYPE_SPACE_LOCATION_MODEL_SCALE_PICO,
                scale = 1.0f
            };
#if (UNITY_ANDROID && !UNITY_EDITOR) || PICO_LIVE_PREVIEW
            PxrResult result = PXR_Plugin.MixedReality.UPxr_ConvertIntToPxrResult(
                Pxr_LocateImageTrackingAnchorAligned(
                    anchor,
                    ref location,
                    ref scale));
            modelScale = scale.scale;
            return result;
#else
            modelScale = 1.0f;
            return PxrResult.ERROR_RUNTIME_FAILURE;
#endif
        }
    }
}
#endif
