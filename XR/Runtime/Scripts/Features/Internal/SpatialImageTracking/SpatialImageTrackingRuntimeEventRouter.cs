#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;

namespace ByteDance.PICO.XR
{
    internal readonly struct SpatialImageTrackingRuntimeEvent
    {
        internal SpatialImageTrackingRuntimeEvent(
            XrEventDataBuffer eventData,
            ulong providerHandle,
            long providerGeneration)
        {
            EventData = eventData;
            ProviderHandle = providerHandle;
            ProviderGeneration = providerGeneration;
        }

        internal XrEventDataBuffer EventData { get; }

        internal ulong ProviderHandle { get; }

        internal long ProviderGeneration { get; }
    }

    internal static class SpatialImageTrackingRuntimeEventRouter
    {
        private static event Action<SpatialImageTrackingRuntimeEvent>
            RuntimeEvent;

        internal static void Subscribe(
            Action<SpatialImageTrackingRuntimeEvent> handler)
        {
            RuntimeEvent += handler;
        }

        internal static void Unsubscribe(
            Action<SpatialImageTrackingRuntimeEvent> handler)
        {
            RuntimeEvent -= handler;
        }

        internal static void Dispatch(XrEventDataBuffer eventData)
        {
            if (eventData.type
                    != XrStructureType
                        .XR_TYPE_EVENT_DATA_SENSE_DATA_PROVIDER_STATE_CHANGED
                && eventData.type
                    != XrStructureType.XR_TYPE_EVENT_DATA_SENSE_DATA_UPDATED)
            {
                return;
            }

            if (eventData.data == null
                || eventData.data.Length < sizeof(ulong))
            {
                return;
            }

            ulong providerHandle = BitConverter.ToUInt64(eventData.data, 0);
            if (!SpatialImageTrackingSessionLifecycle
                    .TryGetProviderGeneration(
                        providerHandle,
                        out long providerGeneration))
            {
                return;
            }

            RuntimeEvent?.Invoke(
                new SpatialImageTrackingRuntimeEvent(
                    eventData,
                    providerHandle,
                    providerGeneration));
        }
    }
}
#endif
