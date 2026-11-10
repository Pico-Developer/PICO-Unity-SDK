#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    internal interface IPicoSpatialImageTrackingRuntime
    {
        ImageTrackingProviderStatus Status { get; }

        long ProviderGeneration { get; }

        Task<ImageTrackingConfigureResult> ConfigureAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            CancellationToken cancellationToken);

        PxrResult SetTrackingEnabled(bool enabled);

        Task<PxrResult> QueryAsync(
            long providerGeneration,
            CancellationToken cancellationToken);

        bool HandleProviderStateChanged(
            long providerGeneration,
            PxrSenseDataProviderState providerState);

        ImageTrackingObservation[] AcquireObservations();

        ulong[] AcquireRemovedEntityIds();

        void Destroy();
    }

    internal sealed class PicoSpatialImageTrackingRuntime
        : IPicoSpatialImageTrackingRuntime
    {
        internal static PicoSpatialImageTrackingRuntime Instance { get; } =
            new PicoSpatialImageTrackingRuntime();

        private PicoSpatialImageTrackingRuntime()
        {
        }

        public ImageTrackingProviderStatus Status =>
            PXR_SpatialImageTracking.Status;

        public long ProviderGeneration
        {
            get
            {
                ulong providerHandle =
                    PXR_Plugin.MixedReality.ImageTrackingProviderHandle;
                return SpatialImageTrackingSessionLifecycle
                    .TryGetProviderGeneration(
                        providerHandle,
                        out long providerGeneration)
                    ? providerGeneration
                    : -1;
            }
        }

        public Task<ImageTrackingConfigureResult> ConfigureAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            CancellationToken cancellationToken)
        {
            return PXR_SpatialImageTracking.ConfigureAsync(
                library,
                options,
                cancellationToken);
        }

        public PxrResult SetTrackingEnabled(bool enabled)
        {
            return PXR_SpatialImageTracking.SetTrackingEnabled(enabled);
        }

        public Task<PxrResult> QueryAsync(
            long providerGeneration,
            CancellationToken cancellationToken)
        {
            return PXR_SpatialImageTracking.QueryAsync(
                providerGeneration,
                cancellationToken);
        }

        public bool HandleProviderStateChanged(
            long providerGeneration,
            PxrSenseDataProviderState providerState)
        {
            return PXR_SpatialImageTracking.HandleProviderStateChanged(
                providerGeneration,
                providerState);
        }

        public ImageTrackingObservation[] AcquireObservations()
        {
            return PXR_SpatialImageTracking.AcquireObservations();
        }

        public ulong[] AcquireRemovedEntityIds()
        {
            return PXR_SpatialImageTracking.AcquireRemovedEntityIds();
        }

        public void Destroy()
        {
            PXR_SpatialImageTracking.Destroy();
        }
    }

    internal sealed class PicoSpatialImageTrackingBackend
        : IImageTrackingBackend
    {
        internal const string PicoBackendId = "PICO";

        private readonly object m_Gate = new object();
        private readonly IPicoSpatialImageTrackingRuntime m_Runtime;
        private readonly ImageTrackingChangeAssembler m_ChangeAssembler;
        private readonly CancellationTokenSource m_QueryCancellation =
            new CancellationTokenSource();

        private CancellationTokenSource m_ConfigureCancellation;
        private Task<ImageTrackingConfigureResult> m_PendingConfigure;
        private Task m_QueryPump;
        private long m_ConfigureGeneration;
        private bool m_RuntimeLifecycleStarted;
        private bool m_IsConfiguring;
        private bool m_ConfigureRefreshPending;
        private long m_ConfigureRefreshProviderGeneration = -1;
        private bool m_QueryScheduled;
        private bool m_QueryRefreshPending;
        private long m_QueryProviderGeneration = -1;
        private bool m_IsDisposed;
        private readonly bool m_IsSubscribedToRuntimeEvents;
        private ImageTrackingProviderState m_State;
        private PxrResult m_LastOperationResult;
        private ulong m_AuthoritativeProviderHandle;
        private long m_AuthoritativeProviderGeneration = -1;
        private long m_BoundProviderGeneration = -1;

        internal PicoSpatialImageTrackingBackend()
            : this(
                PicoSpatialImageTrackingRuntime.Instance,
                subscribeToRuntimeEvents: true)
        {
        }

        internal PicoSpatialImageTrackingBackend(
            IPicoSpatialImageTrackingRuntime runtime)
            : this(runtime, subscribeToRuntimeEvents: false)
        {
        }

        internal PicoSpatialImageTrackingBackend(
            IPicoSpatialImageTrackingRuntime runtime,
            bool subscribeToRuntimeEvents)
        {
            m_Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            m_ChangeAssembler =
                new ImageTrackingChangeAssembler(PicoBackendId, Guid.Empty);
            m_State = ImageTrackingProviderState.Uninitialized;
            m_LastOperationResult = PxrResult.SUCCESS;
            m_BoundProviderGeneration = m_Runtime.ProviderGeneration;
            m_IsSubscribedToRuntimeEvents = subscribeToRuntimeEvents;
            if (m_IsSubscribedToRuntimeEvents)
            {
                SpatialImageTrackingRuntimeEventRouter.Subscribe(
                    HandleRuntimeEvent);
            }
        }

        public string BackendId => PicoBackendId;

        public ImageTrackingCapabilitySet Capabilities =>
            ImageTrackingCapabilitySet.V1;

        public long RuntimeGeneration => m_Runtime.ProviderGeneration;

        internal bool IsFaulted
        {
            get
            {
                lock (m_Gate)
                {
                    RefreshRuntimeStatus(PxrResult.SUCCESS);
                    return m_State == ImageTrackingProviderState.Faulted;
                }
            }
        }

        internal ImageTrackingProviderState State
        {
            get
            {
                lock (m_Gate)
                {
                    return m_State;
                }
            }
        }

        internal PxrResult LastOperationResult
        {
            get
            {
                lock (m_Gate)
                {
                    RefreshRuntimeStatus(PxrResult.SUCCESS);
                    return m_LastOperationResult;
                }
            }
        }

        public ImageTrackingRuntimeLibrary CreateRuntimeLibrary(
            XRReferenceImageLibrary source)
        {
            ThrowIfDisposed();
            ImageTrackingLog.Verbose(() => $"Backend.CreateRuntimeLibrary sourceNull={source == null} count={source?.count ?? -1} maxRefs={Capabilities.MaximumReferenceImageCount} maxMoving={Capabilities.MaximumMovingImageCount}");
            ImageTrackingRuntimeLibrary library = ImageTrackingRuntimeLibrary.Create(source, Capabilities);
            ImageTrackingLog.Verbose(() => $"Backend.CreateRuntimeLibrary result count={library.Count} valid={library.IsValid} status={library.ValidationReport.Status} warning={library.ValidationReport.Warning} message={library.ValidationReport.Message}");
            return library;
        }

        public Task<PxrResult> EnsureStartPreconditionsAsync(
            CancellationToken cancellationToken)
        {
            return PicoSpatialDataPermissionGate.EnsureGrantedAsync(
                cancellationToken);
        }

        public Task<ImageTrackingConfigureResult> ConfigureAsync(
            ImageTrackingRuntimeLibrary library,
            CancellationToken cancellationToken)
        {
            return ConfigureAsync(
                library,
                ImageTrackingConfigureOptions.Default,
                cancellationToken);
        }

        public Task<ImageTrackingConfigureResult> ConfigureAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            CancellationToken cancellationToken)
        {
            ImageTrackingLog.Verbose(() => $"Backend.ConfigureAsync request libraryNull={library == null} count={library?.Count ?? -1} valid={library?.IsValid.ToString() ?? "null"} mode={options.ModeIntent} maxMoving={options.MaximumMovingImageCount} timeout={options.FutureTimeout.TotalSeconds}s");
            CancellationTokenSource ownedCancellation;
            Task<ImageTrackingConfigureResult> previousConfigure;
            TaskCompletionSource<ImageTrackingConfigureResult> completion;
            long generation;
            lock (m_Gate)
            {
                ThrowIfDisposed();
                m_ConfigureCancellation?.Cancel();
                previousConfigure = m_PendingConfigure;
                ownedCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                m_ConfigureCancellation = ownedCancellation;
                generation = ++m_ConfigureGeneration;
                m_IsConfiguring = true;
                completion =
                    new TaskCompletionSource<ImageTrackingConfigureResult>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                m_PendingConfigure = completion.Task;
            }

            _ = CompleteConfigureSeriallyAsync(
                library,
                options,
                generation,
                previousConfigure,
                ownedCancellation,
                completion);
            return completion.Task;
        }

        public void SetTrackingEnabled(bool enabled)
        {
            lock (m_Gate)
            {
                ThrowIfDisposed();
                ImageTrackingLog.Verbose(() => $"Backend.SetTrackingEnabled({enabled}) before state={m_State}");
                PxrResult operationResult =
                    m_Runtime.SetTrackingEnabled(enabled);
                ImageTrackingLog.Verbose(() => $"Backend.SetTrackingEnabled({enabled}) result={operationResult}");
                ClearAuthoritativeEventState();
                m_LastOperationResult = operationResult;
                RefreshRuntimeStatus(operationResult);
            }
        }

        public ImageTrackingChanges AcquireChanges()
        {
            lock (m_Gate)
            {
                ThrowIfDisposed();
                RefreshRuntimeStatus(PxrResult.SUCCESS);
                bool eventAllowsObservations =
                    !HasAuthoritativeEventState()
                    || m_State == ImageTrackingProviderState.Running;
                ImageTrackingObservation[] observations =
                    eventAllowsObservations
                        ? m_Runtime.AcquireObservations()
                        : Array.Empty<ImageTrackingObservation>();
                ulong[] removedEntityIds =
                    m_Runtime.AcquireRemovedEntityIds();
                for (int index = 0; index < removedEntityIds.Length; index++)
                {
                    m_ChangeAssembler.RemoveEntity(removedEntityIds[index]);
                }

                ImageTrackingChanges changes = m_ChangeAssembler.Assemble(observations);
                int addedCount = changes.Added?.Length ?? 0;
                int removedCount = changes.Removed?.Length ?? 0;
                if (addedCount > 0 || removedEntityIds.Length > 0 || removedCount > 0)
                {
                    ImageTrackingLog.Verbose(() => $"Backend.AcquireChanges observations={observations.Length} removedEntities={removedEntityIds.Length} added={addedCount} updated={changes.Updated?.Length ?? 0} removed={removedCount} state={m_State}");
                }

                return changes;
            }
        }

        public bool TryGetMetadata(
            TrackableId trackableId,
            out PXR_TrackedImageMetadata metadata)
        {
            lock (m_Gate)
            {
                ThrowIfDisposed();
                return m_ChangeAssembler.TryGetMetadata(
                    trackableId,
                    out metadata);
            }
        }

        public PXR_TrackedImageMetadata[] GetMetadataSnapshot()
        {
            lock (m_Gate)
            {
                ThrowIfDisposed();
                return PXRImageTrackingMetadataRegistry.Snapshot();
            }
        }

        public void HandleRuntimeEvent(IntPtr eventData)
        {
            lock (m_Gate)
            {
                ThrowIfDisposed();
            }

            if (eventData == IntPtr.Zero
                || (XrStructureType)Marshal.ReadInt32(eventData)
                   != XrStructureType.XR_TYPE_EVENT_DATA_SENSE_DATA_UPDATED)
            {
                return;
            }

            HandleDataUpdatedEvent(
                PXR_Plugin.MixedReality.ImageTrackingProviderGeneration);
        }

        private void HandleRuntimeEvent(
            SpatialImageTrackingRuntimeEvent runtimeEvent)
        {
            XrEventDataBuffer eventData = runtimeEvent.EventData;
            if (eventData.data == null || eventData.data.Length < sizeof(ulong))
            {
                return;
            }

            if (!IsCurrentProviderIdentity(runtimeEvent))
            {
                return;
            }

            if (eventData.type
                == XrStructureType.XR_TYPE_EVENT_DATA_SENSE_DATA_UPDATED)
            {
                HandleDataUpdatedEvent(runtimeEvent.ProviderGeneration);
                return;
            }

            if (eventData.type
                == XrStructureType
                    .XR_TYPE_EVENT_DATA_SENSE_DATA_PROVIDER_STATE_CHANGED)
            {
                if (eventData.data.Length < sizeof(ulong) + sizeof(int))
                {
                    return;
                }

                var providerState = (PxrSenseDataProviderState)
                    BitConverter.ToInt32(eventData.data, sizeof(ulong));
                if (!m_Runtime.HandleProviderStateChanged(
                        runtimeEvent.ProviderGeneration,
                        providerState))
                {
                    return;
                }

                lock (m_Gate)
                {
                    if (!m_IsDisposed
                        && runtimeEvent.ProviderGeneration
                        == m_BoundProviderGeneration)
                    {
                        m_AuthoritativeProviderHandle =
                            runtimeEvent.ProviderHandle;
                        m_AuthoritativeProviderGeneration =
                            runtimeEvent.ProviderGeneration;
                        m_State = MapProviderState(providerState);
                        m_LastOperationResult =
                            m_State == ImageTrackingProviderState.Faulted
                                ? PxrResult.ERROR_RUNTIME_FAILURE
                                : PxrResult.SUCCESS;
                        if (m_State == ImageTrackingProviderState.Stopped
                            || m_State == ImageTrackingProviderState.Faulted)
                        {
                            m_ConfigureRefreshPending = false;
                            m_ConfigureRefreshProviderGeneration = -1;
                            m_QueryRefreshPending = false;
                            m_QueryProviderGeneration = -1;
                        }
                    }
                }
            }
        }

        private void HandleDataUpdatedEvent(long providerGeneration)
        {
            lock (m_Gate)
            {
                if (m_IsDisposed)
                {
                    return;
                }

                if (m_IsConfiguring)
                {
                    m_ConfigureRefreshPending = true;
                    m_ConfigureRefreshProviderGeneration =
                        providerGeneration;
                    return;
                }

                RefreshRuntimeStatus(PxrResult.SUCCESS);
                if (HasAuthoritativeEventState()
                    && m_State != ImageTrackingProviderState.Running)
                {
                    return;
                }

                ScheduleQueryLocked(providerGeneration);
            }
        }

        public void Dispose()
        {
            Task pendingConfigure;
            Task queryPump;
            lock (m_Gate)
            {
                if (m_IsDisposed)
                {
                    return;
                }

                m_IsDisposed = true;
                m_ConfigureCancellation?.Cancel();
                m_QueryCancellation.Cancel();
                pendingConfigure = m_PendingConfigure;
                queryPump = m_QueryPump;
            }

            if (m_IsSubscribedToRuntimeEvents)
            {
                SpatialImageTrackingRuntimeEventRouter.Unsubscribe(
                    HandleRuntimeEvent);
            }

            WaitForCleanup(pendingConfigure);
            WaitForCleanup(queryPump);
            m_ChangeAssembler.RemoveAll();
            m_Runtime.Destroy();

            lock (m_Gate)
            {
                m_RuntimeLifecycleStarted = false;
                m_QueryScheduled = false;
                m_ConfigureRefreshPending = false;
                m_ConfigureRefreshProviderGeneration = -1;
                m_QueryRefreshPending = false;
                m_QueryProviderGeneration = -1;
                m_IsConfiguring = false;
                m_BoundProviderGeneration = -1;
                m_State = ImageTrackingProviderState.Disposed;
                m_ConfigureCancellation = null;
                m_QueryCancellation.Dispose();
            }
        }

        private async Task CompleteConfigureSeriallyAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            long generation,
            Task<ImageTrackingConfigureResult> previousConfigure,
            CancellationTokenSource ownedCancellation,
            TaskCompletionSource<ImageTrackingConfigureResult> completion)
        {
            try
            {
                ImageTrackingConfigureResult result =
                    await ConfigureSeriallyAsync(
                            library,
                            options,
                            generation,
                            previousConfigure,
                            ownedCancellation)
                        .ConfigureAwait(false);
                completion.TrySetResult(result);
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        private async Task<ImageTrackingConfigureResult> ConfigureSeriallyAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            long generation,
            Task<ImageTrackingConfigureResult> previousConfigure,
            CancellationTokenSource ownedCancellation)
        {
            bool completionHandled = false;
            try
            {
                await ObserveCompletionAsync(previousConfigure)
                    .ConfigureAwait(false);

                if (!CanRunConfigure(generation, ownedCancellation.Token))
                {
                    return CreateCanceledConfigureResult();
                }

                bool destroyPreviousLifecycle;
                lock (m_Gate)
                {
                    destroyPreviousLifecycle = m_RuntimeLifecycleStarted;
                    m_RuntimeLifecycleStarted = false;
                    m_BoundProviderGeneration = -1;
                }

                if (destroyPreviousLifecycle)
                {
                    m_ChangeAssembler.RemoveAll();
                    m_Runtime.Destroy();
                }

                Task<ImageTrackingConfigureResult> pendingRuntimeConfigure;
                lock (m_Gate)
                {
                    if (!CanRunConfigureLocked(
                            generation,
                            ownedCancellation.Token))
                    {
                        return CreateCanceledConfigureResult();
                    }

                    m_RuntimeLifecycleStarted = true;
                }

                ImageTrackingLog.Verbose(() => $"Backend.ConfigureSerially runtime.ConfigureAsync begin generation={generation} providerGeneration={m_Runtime.ProviderGeneration}");
                pendingRuntimeConfigure = m_Runtime.ConfigureAsync(
                    library,
                    options,
                    ownedCancellation.Token);
                lock (m_Gate)
                {
                    if (CanRunConfigureLocked(
                            generation,
                            ownedCancellation.Token))
                    {
                        m_BoundProviderGeneration =
                            m_Runtime.ProviderGeneration;
                    }
                }

                ImageTrackingConfigureResult result =
                    await pendingRuntimeConfigure.ConfigureAwait(false);
                ImageTrackingLog.Verbose(() => $"Backend.ConfigureSerially runtime.ConfigureAsync result status={result.Status} native={result.NativeResult} state={result.State}");

                lock (m_Gate)
                {
                    if (!m_IsDisposed
                        && generation == m_ConfigureGeneration)
                    {
                        m_State = result.State;
                        m_LastOperationResult = result.NativeResult;
                        ClearAuthoritativeEventState();
                        bool configurationSucceeded =
                            result.Status
                                == ImageTrackingConfigureStatus.Success
                            || result.Status
                                == ImageTrackingConfigureStatus.Degraded;
                        if (configurationSucceeded)
                        {
                            m_ChangeAssembler.ReplaceDatabase(Guid.NewGuid());
                            m_IsConfiguring = false;
                            if (m_ConfigureRefreshPending)
                            {
                                m_ConfigureRefreshPending = false;
                                long refreshProviderGeneration =
                                    m_ConfigureRefreshProviderGeneration;
                                m_ConfigureRefreshProviderGeneration = -1;
                                ScheduleQueryLocked(
                                    refreshProviderGeneration);
                            }
                        }
                        else
                        {
                            m_IsConfiguring = false;
                            m_ConfigureRefreshPending = false;
                            m_ConfigureRefreshProviderGeneration = -1;
                        }

                        completionHandled = true;
                    }
                }

                return result;
            }
            finally
            {
                lock (m_Gate)
                {
                    if (generation == m_ConfigureGeneration)
                    {
                        if (!completionHandled)
                        {
                            m_IsConfiguring = false;
                            m_ConfigureRefreshPending = false;
                            m_ConfigureRefreshProviderGeneration = -1;
                        }

                        if (ReferenceEquals(
                                m_ConfigureCancellation,
                                ownedCancellation))
                        {
                            m_ConfigureCancellation = null;
                        }
                    }
                }

                ownedCancellation.Dispose();
            }
        }

        private void ScheduleQueryLocked(long providerGeneration)
        {
            m_QueryProviderGeneration = providerGeneration;
            if (m_QueryScheduled)
            {
                m_QueryRefreshPending = true;
                return;
            }

            m_QueryScheduled = true;
            m_QueryPump = Task.Run(
                () => RunQueryPumpAsync(m_QueryCancellation.Token));
        }

        private async Task RunQueryPumpAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                long providerGeneration;
                lock (m_Gate)
                {
                    if (m_IsDisposed
                        || cancellationToken.IsCancellationRequested)
                    {
                        m_QueryScheduled = false;
                        m_QueryRefreshPending = false;
                        m_QueryProviderGeneration = -1;
                        return;
                    }

                    RefreshRuntimeStatus(PxrResult.SUCCESS);
                    if (m_State != ImageTrackingProviderState.Running)
                    {
                        m_QueryScheduled = false;
                        m_QueryRefreshPending = false;
                        m_QueryProviderGeneration = -1;
                        return;
                    }

                    providerGeneration = m_QueryProviderGeneration;
                }

                PxrResult result;
                try
                {
                    result = await m_Runtime.QueryAsync(
                            providerGeneration,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    CompleteQueryPump();
                    return;
                }
                catch (OperationCanceledException)
                {
                    result = PxrResult.SUCCESS;
                }
                catch
                {
                    result = PxrResult.ERROR_RUNTIME_FAILURE;
                }

                lock (m_Gate)
                {
                    if (!m_IsDisposed)
                    {
                        RefreshRuntimeStatus(result);
                    }

                    if (m_IsDisposed || cancellationToken.IsCancellationRequested)
                    {
                        m_QueryScheduled = false;
                        m_QueryRefreshPending = false;
                        m_QueryProviderGeneration = -1;
                        return;
                    }

                    if (m_QueryRefreshPending
                        && m_State == ImageTrackingProviderState.Running)
                    {
                        m_QueryRefreshPending = false;
                        continue;
                    }

                    m_QueryScheduled = false;
                    m_QueryProviderGeneration = -1;
                    return;
                }
            }
        }

        private void CompleteQueryPump()
        {
            lock (m_Gate)
            {
                m_QueryScheduled = false;
                m_QueryRefreshPending = false;
                m_QueryProviderGeneration = -1;
            }
        }

        private bool CanRunConfigure(
            long generation,
            CancellationToken cancellationToken)
        {
            lock (m_Gate)
            {
                return CanRunConfigureLocked(generation, cancellationToken);
            }
        }

        private bool CanRunConfigureLocked(
            long generation,
            CancellationToken cancellationToken)
        {
            return !m_IsDisposed
                   && generation == m_ConfigureGeneration
                   && !cancellationToken.IsCancellationRequested;
        }

        private ImageTrackingConfigureResult CreateCanceledConfigureResult()
        {
            ImageTrackingProviderStatus status = m_Runtime.Status;
            return new ImageTrackingConfigureResult(
                ImageTrackingConfigureStatus.ConfigurationCanceled,
                PxrResult.SUCCESS,
                status.State);
        }

        private static async Task ObserveCompletionAsync(Task task)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // The previous operation owns its result. Its successor waits
                // only for cleanup to finish before touching the runtime.
            }
        }

        private void RefreshRuntimeStatus(PxrResult operationResult)
        {
            if (HasAuthoritativeEventState())
            {
                if (operationResult != PxrResult.SUCCESS)
                {
                    m_LastOperationResult = operationResult;
                }

                return;
            }

            ClearAuthoritativeEventState();
            ImageTrackingProviderStatus status = m_Runtime.Status;
            m_State = status.State;
            if (status.State == ImageTrackingProviderState.Faulted)
            {
                m_LastOperationResult = status.LastStartResult;
            }
            else if (operationResult != PxrResult.SUCCESS)
            {
                m_LastOperationResult = operationResult;
            }
        }

        private bool HasAuthoritativeEventState()
        {
            return m_AuthoritativeProviderGeneration >= 0
                   && m_AuthoritativeProviderHandle != 0
                   && SpatialImageTrackingSessionLifecycle
                       .TryGetProviderGeneration(
                           m_AuthoritativeProviderHandle,
                           out long currentGeneration)
                   && m_AuthoritativeProviderGeneration
                   == currentGeneration;
        }

        private static bool IsCurrentProviderIdentity(
            SpatialImageTrackingRuntimeEvent runtimeEvent)
        {
            return SpatialImageTrackingSessionLifecycle
                       .TryGetProviderGeneration(
                           runtimeEvent.ProviderHandle,
                           out long currentGeneration)
                   && currentGeneration
                   == runtimeEvent.ProviderGeneration;
        }

        private void ClearAuthoritativeEventState()
        {
            m_AuthoritativeProviderHandle = 0;
            m_AuthoritativeProviderGeneration = -1;
        }

        private static ImageTrackingProviderState MapProviderState(
            PxrSenseDataProviderState state)
        {
            switch (state)
            {
                case PxrSenseDataProviderState.Initialized:
                    return ImageTrackingProviderState.Initialized;
                case PxrSenseDataProviderState.Running:
                    return ImageTrackingProviderState.Running;
                case PxrSenseDataProviderState.Stopped:
                    return ImageTrackingProviderState.Stopped;
                default:
                    return ImageTrackingProviderState.Faulted;
            }
        }

        private static void WaitForCleanup(Task task)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                task.GetAwaiter().GetResult();
            }
            catch
            {
                // Disposal must continue after the operation settles.
            }
        }

        private void ThrowIfDisposed()
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(
                    nameof(PicoSpatialImageTrackingBackend));
            }
        }
    }
    internal static class PicoSpatialDataPermissionGate
    {
        private const string PermissionName = "com.picovr.permission.SPATIAL_DATA";
        private const string LogTag = "[PXR_ImageTrackingDebug]";

        internal static async Task<PxrResult> EnsureGrantedAsync(
            CancellationToken cancellationToken)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            await Awaitable.MainThreadAsync();

            if (cancellationToken.IsCancellationRequested)
            {
                return PxrResult.ERROR_RUNTIME_FAILURE;
            }

            if (Permission.HasUserAuthorizedPermission(PermissionName))
            {
                ImageTrackingLog.Verbose("SpatialDataPermission already granted.");
                return PxrResult.SUCCESS;
            }

            var completion = new TaskCompletionSource<PxrResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var callbacks = new PermissionCallbacks();

            callbacks.PermissionGranted += permission =>
            {
                ImageTrackingLog.Verbose(() => $"SpatialDataPermission granted: {permission}");
                completion.TrySetResult(PxrResult.SUCCESS);
            };
            callbacks.PermissionDenied += permission =>
            {
                Debug.LogError($"{LogTag} SpatialDataPermission denied: {permission}");
                completion.TrySetResult(PxrResult.ERROR_PERMISSION_INSUFFICIENT);
            };
            callbacks.PermissionDeniedAndDontAskAgain += permission =>
            {
                Debug.LogError($"{LogTag} SpatialDataPermission denied and don't ask again: {permission}");
                completion.TrySetResult(PxrResult.ERROR_PERMISSION_INSUFFICIENT);
            };

            ImageTrackingLog.Verbose(() => $"SpatialDataPermission request: {PermissionName}");
            Permission.RequestUserPermission(PermissionName, callbacks);

            using (cancellationToken.Register(
                       () => completion.TrySetCanceled(cancellationToken)))
            {
                try
                {
                    return await completion.Task.ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    return PxrResult.ERROR_RUNTIME_FAILURE;
                }
            }
#else
            return await Task.FromResult(PxrResult.SUCCESS);
#endif
        }
    }
}
#endif
