#if AR_FOUNDATION_6_IMAGE_TRACKING
namespace ByteDance.PICO.XR
{
    internal enum SpatialImageTrackingSessionOwner
    {
        None,
        LegacyLoader,
        OpenXrFeature
    }

    internal enum SpatialImageTrackingSessionState
    {
        Idle,
        Active,
        Faulted
    }

    internal readonly struct SpatialImageTrackingSessionStatus
    {
        internal SpatialImageTrackingSessionStatus(
            SpatialImageTrackingSessionOwner owner,
            SpatialImageTrackingSessionState state,
            long generation,
            PxrResult lastOperationResult)
        {
            Owner = owner;
            State = state;
            Generation = generation;
            LastOperationResult = lastOperationResult;
        }

        internal SpatialImageTrackingSessionOwner Owner { get; }

        internal SpatialImageTrackingSessionState State { get; }

        internal long Generation { get; }

        internal PxrResult LastOperationResult { get; }
    }

    internal interface ISpatialImageTrackingSessionLifecycle
    {
        bool Acquire(
            SpatialImageTrackingSessionOwner owner,
            ulong sessionToken);

        PxrResult Release(
            SpatialImageTrackingSessionOwner owner,
            ulong sessionToken,
            bool forceSessionTeardown);
    }

    internal interface ISpatialImageTrackingSessionApi
    {
        PxrResult DestroyRuntimeResources();

        void AbandonRuntimeResources();
    }

    internal interface IImageTrackingSubsystemLifecycle
    {
        void Create();

        void Start();

        void Stop();

        void Destroy();
    }

    internal sealed class ImageTrackingSubsystemLifecycle
        : IImageTrackingSubsystemLifecycle
    {
        private readonly System.Action m_Create;
        private readonly System.Action m_Start;
        private readonly System.Action m_Stop;
        private readonly System.Action m_Destroy;

        internal ImageTrackingSubsystemLifecycle(
            System.Action create,
            System.Action start,
            System.Action stop,
            System.Action destroy)
        {
            m_Create = create
                ?? throw new System.ArgumentNullException(nameof(create));
            m_Start = start
                ?? throw new System.ArgumentNullException(nameof(start));
            m_Stop = stop
                ?? throw new System.ArgumentNullException(nameof(stop));
            m_Destroy = destroy
                ?? throw new System.ArgumentNullException(nameof(destroy));
        }

        public void Create()
        {
            m_Create();
        }

        public void Start()
        {
            m_Start();
        }

        public void Stop()
        {
            m_Stop();
        }

        public void Destroy()
        {
            m_Destroy();
        }
    }

    internal sealed class SpatialImageTrackingSessionLifecycle
        : ISpatialImageTrackingSessionLifecycle
    {
        internal static SpatialImageTrackingSessionLifecycle Shared { get; } =
            new SpatialImageTrackingSessionLifecycle(
                SpatialImageTrackingSessionApi.Instance);

        private readonly object m_Gate = new object();
        private readonly ISpatialImageTrackingSessionApi m_Api;
        private SpatialImageTrackingSessionOwner m_Owner;
        private SpatialImageTrackingSessionState m_State;
        private ulong m_SessionToken;
        private long m_Generation;
        private PxrResult m_LastOperationResult;

        internal SpatialImageTrackingSessionLifecycle(
            ISpatialImageTrackingSessionApi api)
        {
            m_Api = api
                ?? throw new System.ArgumentNullException(nameof(api));
            m_State = SpatialImageTrackingSessionState.Idle;
            m_LastOperationResult = PxrResult.SUCCESS;
        }

        internal static bool TryGetProviderGeneration(
            ulong eventProviderHandle,
            out long generation)
        {
            return PXR_Plugin.MixedReality
                .TryGetImageTrackingProviderGeneration(
                    eventProviderHandle,
                    out generation);
        }

        internal SpatialImageTrackingSessionStatus Status
        {
            get
            {
                lock (m_Gate)
                {
                    return new SpatialImageTrackingSessionStatus(
                        m_Owner,
                        m_State,
                        m_Generation,
                        m_LastOperationResult);
                }
            }
        }

        public bool Acquire(
            SpatialImageTrackingSessionOwner owner,
            ulong sessionToken)
        {
            if (owner == SpatialImageTrackingSessionOwner.None)
            {
                return false;
            }

            lock (m_Gate)
            {
                if (m_Owner != SpatialImageTrackingSessionOwner.None)
                {
                    return m_Owner == owner
                           && m_SessionToken == sessionToken;
                }

                m_Owner = owner;
                m_SessionToken = sessionToken;
                m_State = SpatialImageTrackingSessionState.Active;
                m_LastOperationResult = PxrResult.SUCCESS;
                m_Generation++;
                return true;
            }
        }

        public PxrResult Release(
            SpatialImageTrackingSessionOwner owner,
            ulong sessionToken,
            bool forceSessionTeardown)
        {
            lock (m_Gate)
            {
                if (m_Owner != owner || m_SessionToken != sessionToken)
                {
                    return PxrResult.SUCCESS;
                }

                PxrResult runtimeResult = m_Api.DestroyRuntimeResources();
                if (runtimeResult != PxrResult.SUCCESS)
                {
                    return FailRelease(runtimeResult, forceSessionTeardown);
                }

                m_Owner = SpatialImageTrackingSessionOwner.None;
                m_SessionToken = 0;
                m_State = SpatialImageTrackingSessionState.Idle;
                m_LastOperationResult = PxrResult.SUCCESS;
                return PxrResult.SUCCESS;
            }
        }

        private PxrResult FailRelease(
            PxrResult result,
            bool forceSessionTeardown)
        {
            m_LastOperationResult = result;
            if (forceSessionTeardown)
            {
                m_Api.AbandonRuntimeResources();
                m_Owner = SpatialImageTrackingSessionOwner.None;
                m_SessionToken = 0;
            }

            m_State = SpatialImageTrackingSessionState.Faulted;
            return result;
        }
    }

    internal sealed class SpatialImageTrackingSessionApi
        : ISpatialImageTrackingSessionApi
    {
        internal static SpatialImageTrackingSessionApi Instance { get; } =
            new SpatialImageTrackingSessionApi();

        private SpatialImageTrackingSessionApi()
        {
        }

        public PxrResult DestroyRuntimeResources()
        {
            return PXR_SpatialImageTracking.Destroy();
        }

        public void AbandonRuntimeResources()
        {
            PXR_SpatialImageTracking.Abandon();
            PXR_Plugin.MixedReality.InvalidateImageTrackingProviderHandle();
        }
    }
}
#endif
