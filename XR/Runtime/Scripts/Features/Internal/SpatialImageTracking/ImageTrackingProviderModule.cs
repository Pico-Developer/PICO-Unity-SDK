#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    internal enum ImageTrackingProviderState
    {
        Uninitialized,
        Initialized,
        Configured,
        Running,
        Stopped,
        Faulted,
        Disposed
    }

    internal readonly struct ImageTrackingConfigureOptions
    {
        internal ImageTrackingConfigureOptions(
            ImageTrackingModeIntent modeIntent,
            TimeSpan futureTimeout,
            int maximumMovingImageCount = 1,
            PXR_ImageTrackingLibraryHints libraryHints = null)
        {
            ModeIntent = modeIntent;
            FutureTimeout = futureTimeout;
            MaximumMovingImageCount = maximumMovingImageCount;
            LibraryHints = libraryHints;
        }

        internal static ImageTrackingConfigureOptions Default { get; } =
            new ImageTrackingConfigureOptions(
                ImageTrackingModeIntent.Auto,
                TimeSpan.FromSeconds(30));

        internal ImageTrackingModeIntent ModeIntent { get; }

        internal TimeSpan FutureTimeout { get; }

        internal int MaximumMovingImageCount { get; }

        internal PXR_ImageTrackingLibraryHints LibraryHints { get; }

        internal bool TryGetOptimizeForStaticImage(
            ImageTrackingReferenceRecord record,
            out bool optimizeForStaticImage)
        {
            if (LibraryHints != null
                && LibraryHints.TryGetOptimizeForStaticImage(
                    record.Guid,
                    record.Name,
                    out optimizeForStaticImage))
            {
                return true;
            }

            optimizeForStaticImage =
                ModeIntent == ImageTrackingModeIntent.StaticOptimized;
            return optimizeForStaticImage;
        }
    }

    internal readonly struct ImageTrackingConfigureResult
    {
        internal ImageTrackingConfigureResult(
            ImageTrackingConfigureStatus status,
            PxrResult nativeResult,
            ImageTrackingProviderState state)
        {
            Status = status;
            NativeResult = nativeResult;
            State = state;
        }

        internal ImageTrackingConfigureStatus Status { get; }

        internal PxrResult NativeResult { get; }

        internal ImageTrackingProviderState State { get; }
    }

    internal readonly struct ImageTrackingProviderStatus
    {
        internal ImageTrackingProviderStatus(
            ImageTrackingProviderState state,
            PxrResult lastStartResult)
        {
            State = state;
            LastStartResult = lastStartResult;
        }

        internal ImageTrackingProviderState State { get; }

        internal PxrResult LastStartResult { get; }
    }

    internal sealed class ImageTrackingProviderModule : IDisposable
    {
        // Tolerate short query dropouts observed in device logs before
        // emitting removed, reducing added->removed flicker while native
        // snapshot behavior is validated.
        private const int MissingSnapshotRemovalThreshold = 6;
        private enum PendingFutureKind
        {
            Start,
            Query
        }

        private readonly struct PendingFuture
        {
            internal PendingFuture(ulong handle, PendingFutureKind kind)
            {
                Handle = handle;
                Kind = kind;
            }

            internal ulong Handle { get; }

            internal PendingFutureKind Kind { get; }
        }

        private sealed class TrackedImage
        {
            internal ulong Entity;
            internal ulong Anchor;
            internal Guid ReferenceImageGuid;
            internal Vector2 BoundingBoxOffset;
            internal Vector2 Size;
            internal int ReferenceImageIndex;
            internal ulong ImageTrackingDatabase;
            internal long EntityLastUpdateTime;
            internal bool HasBoundingBox2D;
            internal int ConsecutiveMissingSnapshots;
            internal Pose LastValidPose;
            internal bool HasLastValidPose;
            internal Pose LastValidAlignedPose;
            internal bool HasLastValidAlignedPose;
        }

        private readonly object m_Gate = new object();
        private readonly ISpatialImageTrackingNativeApi m_Native;
        private readonly IUnmanagedAllocator m_Allocator;
        private readonly FutureRunner m_FutureRunner;
        private readonly Dictionary<ulong, TrackedImage> m_TrackedImages =
            new Dictionary<ulong, TrackedImage>();
        private readonly List<PendingFuture> m_UnresolvedFutures =
            new List<PendingFuture>();
        private readonly List<ulong> m_RemovedEntityIds =
            new List<ulong>();

        private ImageTrackingProviderState m_State;
        private PxrResult m_LastStartResult;
        private ulong m_Provider;
        private long m_ProviderGeneration;
        private bool m_OwnsProvider;
        private bool m_ProviderMayBeRunning;
        private ImageTrackingDatabase m_Database;
        private ImageTrackingRuntimeLibrary m_Library;
        private ImageTrackingModeIntent m_ModeIntent = ImageTrackingModeIntent.Auto;
        private CancellationTokenSource m_ConfigureCancellation;
        private Task<ImageTrackingConfigureResult> m_PendingConfigure;
        private CancellationTokenSource m_StartCancellation;
        private Task<PxrResult> m_PendingStart;
        private CancellationTokenSource m_QueryCancellation;
        private Task<PxrResult> m_PendingQuery;
        private bool m_DisposeStarted;

        internal ImageTrackingProviderModule(
            ISpatialImageTrackingNativeApi native,
            IUnmanagedAllocator allocator = null,
            ulong initialProviderHandle = 0,
            long initialProviderGeneration = -1)
        {
            m_Native = native ?? throw new ArgumentNullException(nameof(native));
            m_Allocator = allocator ?? MarshalUnmanagedAllocator.Instance;
            m_FutureRunner = new FutureRunner(native);
            m_Provider = initialProviderHandle;
            m_ProviderGeneration = initialProviderGeneration;
            m_OwnsProvider = initialProviderHandle == 0;
            m_State = ImageTrackingProviderState.Uninitialized;
            m_LastStartResult = PxrResult.SUCCESS;
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

        internal ImageTrackingProviderStatus Status
        {
            get
            {
                lock (m_Gate)
                {
                    return new ImageTrackingProviderStatus(
                        m_State,
                        m_LastStartResult);
                }
            }
        }

        internal Task<ImageTrackingConfigureResult> ConfigureAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            CancellationToken cancellationToken)
        {
            lock (m_Gate)
            {
                if (m_State == ImageTrackingProviderState.Disposed || m_DisposeStarted)
                {
                    return Task.FromResult(CreateResult(
                        ImageTrackingConfigureStatus.NativeFailure,
                        PxrResult.ERROR_HANDLE_INVALID));
                }

                if (m_PendingConfigure != null && !m_PendingConfigure.IsCompleted)
                {
                    return m_PendingConfigure;
                }

                bool canConfigure =
                    m_State == ImageTrackingProviderState.Uninitialized
                    || ((m_State == ImageTrackingProviderState.Stopped
                         || m_State == ImageTrackingProviderState.Faulted)
                        && (m_Database == null || !m_Database.IsCreated));
                if (!canConfigure)
                {
                    return Task.FromResult(CreateResult(
                        ImageTrackingConfigureStatus.NativeFailure,
                        PxrResult.ERROR_CALL_ORDER_INVALID));
                }

                m_ConfigureCancellation?.Dispose();
                m_ConfigureCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                m_PendingConfigure = ConfigureCoreAsync(
                    library,
                    options,
                    m_ConfigureCancellation.Token);
                return m_PendingConfigure;
            }
        }

        internal PxrResult SetTrackingEnabled(bool enabled)
        {
            if (!enabled)
            {
                return Stop();
            }

            Task<PxrResult> startTask;
            lock (m_Gate)
            {
                if (m_State == ImageTrackingProviderState.Disposed || m_DisposeStarted)
                {
                    return PxrResult.ERROR_HANDLE_INVALID;
                }

                if (m_State == ImageTrackingProviderState.Running)
                {
                    return PxrResult.SUCCESS;
                }

                if (m_PendingStart != null && !m_PendingStart.IsCompleted)
                {
                    startTask = m_PendingStart;
                }
                else
                {
                    if (m_State != ImageTrackingProviderState.Stopped
                        || m_Database == null
                        || !m_Database.IsCreated)
                    {
                        return PxrResult.ERROR_CALL_ORDER_INVALID;
                    }

                    startTask = StartExistingProviderAsync(CancellationToken.None);
                }
            }

            return startTask.IsCompleted
                ? startTask.GetAwaiter().GetResult()
                : PxrResult.SUCCESS;
        }

        internal Task<PxrResult> QueryAsync(CancellationToken cancellationToken)
        {
            lock (m_Gate)
            {
                return QueryLocked(cancellationToken);
            }
        }

        internal Task<PxrResult> QueryAsync(
            long providerGeneration,
            CancellationToken cancellationToken)
        {
            lock (m_Gate)
            {
                if (providerGeneration != m_ProviderGeneration
                    || !SpatialImageTrackingSessionLifecycle
                        .TryGetProviderGeneration(
                            m_Provider,
                            out long currentProviderGeneration)
                    || currentProviderGeneration != m_ProviderGeneration)
                {
                    return Task.FromResult(PxrResult.ERROR_HANDLE_INVALID);
                }

                return QueryLocked(cancellationToken);
            }
        }

        private Task<PxrResult> QueryLocked(
            CancellationToken cancellationToken)
        {
            if (m_State != ImageTrackingProviderState.Running)
            {
                return Task.FromResult(PxrResult.ERROR_CALL_ORDER_INVALID);
            }

            if (m_PendingQuery != null && !m_PendingQuery.IsCompleted)
            {
                return Task.FromResult(PxrResult.ERROR_CALL_ORDER_INVALID);
            }

            m_QueryCancellation?.Dispose();
            m_QueryCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            m_PendingQuery = QueryCoreAsync(m_QueryCancellation.Token);
            return m_PendingQuery;
        }

        internal bool HandleProviderStateChanged(
            long providerGeneration,
            PxrSenseDataProviderState providerState)
        {
            lock (m_Gate)
            {
                if (m_State == ImageTrackingProviderState.Disposed
                    || m_DisposeStarted
                    || m_Provider == 0
                    || m_ProviderGeneration < 0
                    || providerGeneration != m_ProviderGeneration
                    || !SpatialImageTrackingSessionLifecycle
                        .TryGetProviderGeneration(
                            m_Provider,
                            out long currentProviderGeneration)
                    || currentProviderGeneration != m_ProviderGeneration)
                {
                    return false;
                }

                m_State = MapProviderState(providerState);
                if (providerState == PxrSenseDataProviderState.Running)
                {
                    m_ProviderMayBeRunning = true;
                }
                else if (providerState
                         == PxrSenseDataProviderState.Initialized
                         || providerState
                         == PxrSenseDataProviderState.Stopped)
                {
                    m_ProviderMayBeRunning = false;
                }

                m_LastStartResult =
                    m_State == ImageTrackingProviderState.Faulted
                        ? PxrResult.ERROR_RUNTIME_FAILURE
                        : PxrResult.SUCCESS;
                if (m_State == ImageTrackingProviderState.Stopped
                    || m_State == ImageTrackingProviderState.Faulted)
                {
                    m_QueryCancellation?.Cancel();
                }

                return true;
            }
        }

        internal ImageTrackingObservation[] LocateTrackedImages()
        {
            lock (m_Gate)
            {
                if (m_State != ImageTrackingProviderState.Running
                    || m_DisposeStarted)
                {
                    return Array.Empty<ImageTrackingObservation>();
                }

                var observations =
                    new ImageTrackingObservation[m_TrackedImages.Count];
                int index = 0;
                PXRImageTrackingOptions currentOptions =
                    PXRImageTrackingOptionsRegistry.Current;
                ImageTrackingAlignmentOptions alignmentOptions =
                    new ImageTrackingAlignmentOptions(
                        currentOptions.AlignmentMode,
                        currentOptions.AlignmentInvalidPolicy);
                foreach (TrackedImage trackedImage in m_TrackedImages.Values)
                {
                    Pose pose = trackedImage.HasLastValidPose
                        ? trackedImage.LastValidPose
                        : Pose.identity;
                    TrackingState trackingState;
                    bool hasValidPose = false;
                    ImageTrackingAlignmentLocateResult locate =
                        new ImageTrackingAlignmentLocateResult(
                            pose,
                            TrackingState.Limited,
                            false,
                            false,
                            false,
                            1.0f,
                            alignmentOptions.Mode);
                    if (trackedImage.ConsecutiveMissingSnapshots > 0
                        || trackedImage.Anchor == 0)
                    {
                        trackingState = TrackingState.Limited;
                    }
                    else
                    {
                        locate = ImageTrackingAlignmentLocator.Locate(
                            m_Native,
                            trackedImage.Anchor,
                            trackedImage.LastValidPose,
                            trackedImage.HasLastValidPose,
                            trackedImage.LastValidAlignedPose,
                            trackedImage.HasLastValidAlignedPose,
                            alignmentOptions);
                        pose = locate.Pose;
                        trackingState = locate.TrackingState;
                        hasValidPose = locate.HasValidPose;
                        if (locate.HasValidPose)
                        {
                            trackedImage.LastValidPose = locate.Pose;
                            trackedImage.HasLastValidPose = true;
                        }

                        if (locate.IsAligned && locate.HasAlignedPose)
                        {
                            trackedImage.LastValidAlignedPose = locate.Pose;
                            trackedImage.HasLastValidAlignedPose = true;
                        }
                    }

                    observations[index++] = new ImageTrackingObservation(
                        trackedImage.Entity,
                        trackedImage.ReferenceImageGuid,
                        pose,
                        trackedImage.ReferenceImageIndex,
                        trackedImage.ImageTrackingDatabase,
                        trackedImage.BoundingBoxOffset,
                        trackedImage.Size,
                        trackedImage.HasBoundingBox2D,
                        trackingState,
                        hasValidPose,
                        locate.Mode,
                        locate.IsAligned,
                        locate.HasAlignedPose,
                        locate.ModelScale,
                        trackedImage.EntityLastUpdateTime);
                }

                return observations;
            }
        }

        internal ulong[] AcquireRemovedEntityIds()
        {
            lock (m_Gate)
            {
                ulong[] removed = m_RemovedEntityIds.ToArray();
                m_RemovedEntityIds.Clear();
                return removed;
            }
        }

        public void Dispose()
        {
            Destroy();
        }

        internal PxrResult Destroy()
        {
            Task<ImageTrackingConfigureResult> pendingConfigure;
            Task<PxrResult> pendingStart;
            Task<PxrResult> pendingQuery;
            lock (m_Gate)
            {
                if (m_State == ImageTrackingProviderState.Disposed)
                {
                    return PxrResult.SUCCESS;
                }

                if (m_DisposeStarted)
                {
                    return PxrResult.ERROR_CALL_ORDER_INVALID;
                }

                m_DisposeStarted = true;
                m_ConfigureCancellation?.Cancel();
                m_StartCancellation?.Cancel();
                m_QueryCancellation?.Cancel();
                pendingConfigure = m_PendingConfigure;
                pendingStart = m_PendingStart;
                pendingQuery = m_PendingQuery;
            }

            if (!WaitForCleanup(pendingConfigure)
                || !WaitForCleanup(pendingStart)
                || !WaitForCleanup(pendingQuery))
            {
                lock (m_Gate)
                {
                    return FailDestroy(PxrResult.TIMEOUT_EXPIRED);
                }
            }

            lock (m_Gate)
            {
                PxrResult pendingCleanupResult =
                    ResolvePendingNativeCleanup();
                if (pendingCleanupResult != PxrResult.SUCCESS)
                {
                    return FailDestroy(pendingCleanupResult);
                }

                if (m_ProviderMayBeRunning && m_OwnsProvider)
                {
                    PxrResult stopResult = m_Native.StopProvider(m_Provider);
                    if (!IsSuccessOrStaleHandle(stopResult))
                    {
                        return FailDestroy(stopResult);
                    }

                    m_ProviderMayBeRunning = false;
                }

                var destroyedAnchors = new HashSet<ulong>();
                var failedAnchors = new HashSet<ulong>();
                PxrResult anchorResult = PxrResult.SUCCESS;
                foreach (TrackedImage trackedImage in m_TrackedImages.Values)
                {
                    if (trackedImage.Anchor != 0
                        && destroyedAnchors.Add(trackedImage.Anchor))
                    {
                        PxrResult result =
                            m_Native.DestroyAnchor(trackedImage.Anchor);
                        if (!IsSuccessOrStaleHandle(result))
                        {
                            failedAnchors.Add(trackedImage.Anchor);
                            if (anchorResult == PxrResult.SUCCESS)
                            {
                                anchorResult = result;
                            }
                        }
                    }
                }

                if (anchorResult != PxrResult.SUCCESS)
                {
                    RemoveDestroyedTrackedImages(failedAnchors);
                    return FailDestroy(anchorResult);
                }

                m_TrackedImages.Clear();
                m_RemovedEntityIds.Clear();
                PxrResult databaseResult =
                    m_Database?.Destroy() ?? PxrResult.SUCCESS;
                if (databaseResult != PxrResult.SUCCESS)
                {
                    return FailDestroy(databaseResult);
                }

                m_Database = null;
                if (m_Provider != 0 && m_OwnsProvider)
                {
                    PxrResult providerResult =
                        m_Native.DestroyProvider(m_Provider);
                    if (!IsSuccessOrStaleHandle(providerResult))
                    {
                        return FailDestroy(providerResult);
                    }
                }

                m_Provider = 0;
                m_ProviderGeneration = -1;
                m_ProviderMayBeRunning = false;
                m_Library = null;
                m_State = ImageTrackingProviderState.Disposed;
                DisposeCancellationSources();
                return PxrResult.SUCCESS;
            }
        }

        internal void Abandon()
        {
            Task<ImageTrackingConfigureResult> pendingConfigure;
            Task<PxrResult> pendingStart;
            Task<PxrResult> pendingQuery;
            lock (m_Gate)
            {
                if (m_State == ImageTrackingProviderState.Disposed
                    || m_DisposeStarted)
                {
                    return;
                }

                m_DisposeStarted = true;
                m_ConfigureCancellation?.Cancel();
                m_StartCancellation?.Cancel();
                m_QueryCancellation?.Cancel();
                pendingConfigure = m_PendingConfigure;
                pendingStart = m_PendingStart;
                pendingQuery = m_PendingQuery;
            }

            WaitForCleanup(pendingConfigure);
            WaitForCleanup(pendingStart);
            WaitForCleanup(pendingQuery);

            lock (m_Gate)
            {
                // The session is already invalid. Native child handles can no
                // longer be destroyed safely, so only managed ownership ends.
                m_TrackedImages.Clear();
                m_RemovedEntityIds.Clear();
                m_UnresolvedFutures.Clear();
                m_Database?.Abandon();
                m_Database = null;
                m_Provider = 0;
                m_ProviderGeneration = -1;
                m_ProviderMayBeRunning = false;
                m_Library = null;
                m_State = ImageTrackingProviderState.Disposed;
                DisposeCancellationSources();
            }
        }

        private async Task<ImageTrackingConfigureResult> ConfigureCoreAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            CancellationToken cancellationToken)
        {
            if (library == null || !library.IsValid)
            {
                ImageTrackingConfigureStatus status = library == null
                    ? ImageTrackingConfigureStatus.InvalidLibrary
                    : library.ValidationReport.Status;
                return CreateResult(status, PxrResult.ERROR_VALIDATION_FAILURE);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                SetState(ImageTrackingProviderState.Stopped);
                return CreateResult(
                    ImageTrackingConfigureStatus.ConfigurationCanceled,
                    PxrResult.SUCCESS);
            }

            PxrResult formatResult = m_Native.EnumerateFormats(
                out XrSpatialReferenceImageFormat[] supportedFormats);
            if (formatResult != PxrResult.SUCCESS)
            {
                SetState(ImageTrackingProviderState.Faulted);
                return CreateResult(
                    MapNativeFailure(formatResult),
                    formatResult);
            }

            if (!TrySelectReferenceImageFormat(
                    supportedFormats,
                    out XrSpatialReferenceImageFormat selectedFormat))
            {
                SetState(ImageTrackingProviderState.Faulted);
                return CreateResult(
                    ImageTrackingConfigureStatus.FormatUnsupported,
                    PxrResult.ERROR_FEATURE_UNSUPPORTED);
            }

            ImageTrackingModeResolution mode =
                ImageTrackingModeResolution.Resolve(options.ModeIntent);
            lock (m_Gate)
            {
                m_ModeIntent = options.ModeIntent;
            }
            if (m_Provider == 0)
            {
                ImageTrackingLog.Verbose(() => $"ProviderModule.CreateProvider begin mode={mode.ActualMode}");
                PxrResult providerResult = m_Native.CreateProvider(
                    mode.ActualMode,
                    out m_Provider);
                ImageTrackingLog.Verbose(() => $"ProviderModule.CreateProvider result={providerResult} provider={m_Provider}");
                if (providerResult != PxrResult.SUCCESS)
                {
                    SetState(ImageTrackingProviderState.Faulted);
                    return CreateResult(
                        MapNativeFailure(providerResult),
                        providerResult);
                }

                m_OwnsProvider = true;
                if (!SpatialImageTrackingSessionLifecycle
                        .TryGetProviderGeneration(
                            m_Provider,
                            out m_ProviderGeneration))
                {
                    m_ProviderGeneration = -1;
                }
            }

            SetState(ImageTrackingProviderState.Initialized);
            var candidateDatabase = new ImageTrackingDatabase(
                m_Native,
                m_Provider,
                m_Allocator);
            ImageTrackingDatabaseCreateResult databaseResult;
            try
            {
                databaseResult = await candidateDatabase.CreateAsync(
                        library,
                        options,
                        selectedFormat,
                        m_FutureRunner,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is InvalidOperationException
                || exception is NotSupportedException)
            {
                candidateDatabase.Dispose();
                SetState(ImageTrackingProviderState.Faulted);
                return CreateResult(
                    ImageTrackingConfigureStatus.InvalidReferenceImage,
                    PxrResult.ERROR_VALIDATION_FAILURE);
            }

            if (!databaseResult.Succeeded)
            {
                if (databaseResult.CleanupUnresolved)
                {
                    m_Database = candidateDatabase;
                    SetState(ImageTrackingProviderState.Faulted);
                    return CreateResult(
                        ImageTrackingConfigureStatus.CleanupUnresolved,
                        PxrResult.TIMEOUT_EXPIRED);
                }

                candidateDatabase.Dispose();
                if (databaseResult.CancellationRequested)
                {
                    SetState(ImageTrackingProviderState.Stopped);
                    return CreateResult(
                        ImageTrackingConfigureStatus.ConfigurationCanceled,
                        databaseResult.NativeResult);
                }

                SetState(ImageTrackingProviderState.Faulted);
                return CreateResult(
                    ImageTrackingConfigureStatus.NativeFailure,
                    databaseResult.TimedOut
                        ? PxrResult.TIMEOUT_EXPIRED
                        : databaseResult.NativeResult);
            }

            m_Database?.Dispose();
            m_Database = candidateDatabase;
            m_Library = library;
            SetState(ImageTrackingProviderState.Configured);
            if (cancellationToken.IsCancellationRequested)
            {
                SetState(ImageTrackingProviderState.Stopped);
                return CreateResult(
                    ImageTrackingConfigureStatus.ConfigurationCanceled,
                    PxrResult.SUCCESS);
            }

            PxrResult startResult = await StartExistingProviderAsync(
                    cancellationToken,
                    options.FutureTimeout)
                .ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return CreateResult(
                    ImageTrackingConfigureStatus.ConfigurationCanceled,
                    startResult);
            }

            if (startResult != PxrResult.SUCCESS)
            {
                SetState(ImageTrackingProviderState.Faulted);
                return CreateResult(
                    ImageTrackingConfigureStatus.NativeFailure,
                    startResult);
            }

            return CreateResult(
                mode.IsDegraded
                    ? ImageTrackingConfigureStatus.Degraded
                    : ImageTrackingConfigureStatus.Success,
                PxrResult.SUCCESS);
        }

        private Task<PxrResult> StartExistingProviderAsync(
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            lock (m_Gate)
            {
                if (m_PendingStart != null && !m_PendingStart.IsCompleted)
                {
                    return m_PendingStart;
                }

                m_StartCancellation?.Dispose();
                m_StartCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                m_PendingStart = StartExistingProviderCoreAsync(
                    m_StartCancellation.Token,
                    timeout ?? ImageTrackingConfigureOptions.Default.FutureTimeout);
                return m_PendingStart;
            }
        }

        private async Task<PxrResult> StartExistingProviderCoreAsync(
            CancellationToken cancellationToken,
            TimeSpan timeout)
        {
            ImageTrackingLog.Verbose(() => $"ProviderModule.StartProviderAsync begin provider={m_Provider} timeout={timeout.TotalSeconds}s");
            PxrResult startResult = m_Native.StartProviderAsync(
                m_Provider,
                out ulong future);
            ImageTrackingLog.Verbose(() => $"ProviderModule.StartProviderAsync result={startResult} future={future}");
            if (startResult != PxrResult.SUCCESS)
            {
                SetStartTerminalStatus(
                    startResult,
                    ImageTrackingProviderState.Faulted);
                return startResult;
            }

            SetProviderMayBeRunning(true);
            FutureWaitOutcome waitOutcome = await m_FutureRunner.WaitWithOutcomeAsync(
                    future,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            ImageTrackingLog.Verbose(() => $"ProviderModule.StartProvider wait ready={waitOutcome.FutureReady} poll={waitOutcome.PollResult} timedOut={waitOutcome.TimedOut} canceled={waitOutcome.CancellationRequested} cleanupUnresolved={waitOutcome.CleanupUnresolved}");
            if (waitOutcome.CleanupUnresolved)
            {
                RegisterUnresolvedFuture(future, PendingFutureKind.Start);
                SetStartTerminalStatus(
                    PxrResult.TIMEOUT_EXPIRED,
                    ImageTrackingProviderState.Faulted);
                return PxrResult.TIMEOUT_EXPIRED;
            }

            if (!waitOutcome.FutureReady)
            {
                SetProviderMayBeRunning(false);
                if (waitOutcome.CancellationRequested)
                {
                    SetStartTerminalStatus(
                        PxrResult.SUCCESS,
                        ImageTrackingProviderState.Stopped);
                    return PxrResult.SUCCESS;
                }

                if (waitOutcome.TimedOut)
                {
                    SetStartTerminalStatus(
                        PxrResult.TIMEOUT_EXPIRED,
                        ImageTrackingProviderState.Faulted);
                    return PxrResult.TIMEOUT_EXPIRED;
                }

                SetStartTerminalStatus(
                    waitOutcome.PollResult,
                    ImageTrackingProviderState.Faulted);
                return waitOutcome.PollResult;
            }

            ImageTrackingLog.Verbose(() => $"ProviderModule.StartProviderComplete begin future={future}");
            PxrResult completeResult = m_Native.StartProviderComplete(
                future,
                out XrSenseDataProviderStartCompletion completion);
            ImageTrackingLog.Verbose(() => $"ProviderModule.StartProviderComplete result={completeResult} futureResult={completion.futureResult}");
            PxrResult nativeResult = completeResult == PxrResult.SUCCESS
                ? completion.futureResult
                : completeResult;
            bool cancellationRequested =
                waitOutcome.CancellationRequested
                || cancellationToken.IsCancellationRequested;
            if (cancellationRequested)
            {
                if (nativeResult == PxrResult.SUCCESS)
                {
                    PxrResult stopResult = m_Native.StopProvider(m_Provider);
                    if (stopResult == PxrResult.SUCCESS)
                    {
                        SetProviderMayBeRunning(false);
                    }
                    else
                    {
                        SetStartTerminalStatus(
                            stopResult,
                            ImageTrackingProviderState.Faulted);
                        return stopResult;
                    }
                }
                else
                {
                    SetProviderMayBeRunning(false);
                }

                SetStartTerminalStatus(
                    nativeResult,
                    ImageTrackingProviderState.Stopped);
                return nativeResult;
            }

            if (waitOutcome.TimedOut)
            {
                if (nativeResult == PxrResult.SUCCESS)
                {
                    PxrResult stopResult = m_Native.StopProvider(m_Provider);
                    if (stopResult == PxrResult.SUCCESS)
                    {
                        SetProviderMayBeRunning(false);
                    }
                }

                SetStartTerminalStatus(
                    PxrResult.TIMEOUT_EXPIRED,
                    ImageTrackingProviderState.Faulted);
                return PxrResult.TIMEOUT_EXPIRED;
            }

            if (waitOutcome.PollResult != PxrResult.SUCCESS)
            {
                if (nativeResult == PxrResult.SUCCESS)
                {
                    PxrResult stopResult = m_Native.StopProvider(m_Provider);
                    if (stopResult == PxrResult.SUCCESS)
                    {
                        SetProviderMayBeRunning(false);
                    }
                }

                SetStartTerminalStatus(
                    waitOutcome.PollResult,
                    ImageTrackingProviderState.Faulted);
                return waitOutcome.PollResult;
            }

            if (nativeResult == PxrResult.SUCCESS)
            {
                SetStartTerminalStatus(
                    PxrResult.SUCCESS,
                    ImageTrackingProviderState.Running);
            }
            else
            {
                SetProviderMayBeRunning(false);
                SetStartTerminalStatus(
                    nativeResult,
                    ImageTrackingProviderState.Faulted);
            }

            return nativeResult;
        }

        private async Task<PxrResult> QueryCoreAsync(
            CancellationToken cancellationToken)
        {
            PxrResult queryResult = m_Native.QueryAsync(
                m_Provider,
                out ulong future);
            if (queryResult != PxrResult.SUCCESS)
            {
                return queryResult;
            }

            FutureWaitOutcome waitOutcome = await m_FutureRunner.WaitWithOutcomeAsync(
                    future,
                    ImageTrackingConfigureOptions.Default.FutureTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (waitOutcome.CleanupUnresolved)
            {
                RegisterUnresolvedFuture(future, PendingFutureKind.Query);
                return PxrResult.TIMEOUT_EXPIRED;
            }

            if (!waitOutcome.FutureReady)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (waitOutcome.TimedOut)
                {
                    return PxrResult.TIMEOUT_EXPIRED;
                }

                return waitOutcome.PollResult;
            }

            PxrResult completeResult = m_Native.QueryComplete(
                m_Provider,
                future,
                out XrSenseDataQueryCompletion completion);
            if (completeResult != PxrResult.SUCCESS)
            {
                return completeResult;
            }

            // Adopt the snapshot handle before any further early return so it is
            // always released by Dispose(); the failed-futureResult path below
            // could otherwise leak a non-zero handle.
            using (var snapshot = new SnapshotLease(
                       m_Native,
                       completion.snapshotHandle))
            {
                if (completion.futureResult != PxrResult.SUCCESS)
                {
                    return completion.futureResult;
                }

                if (waitOutcome.PollResult != PxrResult.SUCCESS)
                {
                    return waitOutcome.PollResult;
                }

                cancellationToken.ThrowIfCancellationRequested();
                PxrResult getResult = m_Native.GetQueriedSenseData(
                    m_Provider,
                    snapshot.Handle,
                    out IReadOnlyList<PxrQueriedSpatialEntityInfo> entities);
                if (getResult != PxrResult.SUCCESS)
                {
                    return getResult;
                }

                ImageTrackingLog.Verbose(() =>
                {
                    var entityList = new List<string>(entities.Count);
                    foreach (PxrQueriedSpatialEntityInfo entityInfo in entities)
                    {
                        entityList.Add($"{entityInfo.spatialEntity}@{entityInfo.time}");
                    }

                    return $"ProviderModule.Query snapshot={snapshot.Handle} entityCount={entities.Count} entities=[{string.Join(",", entityList)}]";
                });

                var observedEntities = new HashSet<ulong>();
                foreach (PxrQueriedSpatialEntityInfo entityInfo in entities)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long entityLastUpdateTime = unchecked((long)entityInfo.time);
                    PxrResult processResult = ProcessEntity(
                        snapshot.Handle,
                        entityInfo.spatialEntity,
                        entityLastUpdateTime);
                    if (processResult == PxrResult.SUCCESS)
                    {
                        observedEntities.Add(entityInfo.spatialEntity);
                    }
                    else
                    {
                        ImageTrackingLog.Verbose(() =>
                            $"ProviderModule.Query processEntityFailed snapshot={snapshot.Handle} entity={entityInfo.spatialEntity} time={entityInfo.time} result={processResult}");
                    }
                }

                ImageTrackingLog.Verbose(() =>
                    $"ProviderModule.Query processed observedEntityCount={observedEntities.Count}");

                PxrResult reconcileResult =
                    RemoveTrackedImagesMissingFromSnapshot(observedEntities);
                if (reconcileResult != PxrResult.SUCCESS)
                {
                    return reconcileResult;
                }
            }

            return waitOutcome.TimedOut
                ? PxrResult.TIMEOUT_EXPIRED
                : PxrResult.SUCCESS;
        }

        private PxrResult ProcessEntity(
            ulong snapshot,
            ulong entity,
            long entityLastUpdateTime)
        {
            PxrResult imageResult = m_Native.GetImageTrackingData(
                snapshot,
                entity,
                out XrSpatialImageTrackingData imageData);
            if (imageResult != PxrResult.SUCCESS)
            {
                return imageResult;
            }

            if (m_Database == null
                || imageData.imageTrackingDatabase != m_Database.Handle
                || imageData.referenceImageIndex >= m_Library.Count)
            {
                return PxrResult.ERROR_VALIDATION_FAILURE;
            }

            ImageTrackingReferenceRecord referenceImage =
                m_Library[checked((int)imageData.referenceImageIndex)];
            PxrResult boundsResult = m_Native.GetBoundingBox2D(
                snapshot,
                entity,
                out Vector2 offset,
                out Vector2 extent);
            bool hasBoundingBox2D = boundsResult == PxrResult.SUCCESS;
            if (!hasBoundingBox2D)
            {
                offset = Vector2.zero;
                extent = CalculateReferenceImageSize(referenceImage);
            }

            lock (m_Gate)
            {
                if (m_TrackedImages.TryGetValue(entity, out TrackedImage existing))
                {
                    existing.ReferenceImageGuid = referenceImage.Guid;
                    existing.BoundingBoxOffset = offset;
                    existing.Size = extent;
                    existing.ReferenceImageIndex = checked((int)imageData.referenceImageIndex);
                    existing.ImageTrackingDatabase = imageData.imageTrackingDatabase;
                    existing.EntityLastUpdateTime = entityLastUpdateTime;
                    existing.HasBoundingBox2D = hasBoundingBox2D;
                    if (existing.Anchor == 0
                        && m_ModeIntent != ImageTrackingModeIntent.RecognitionOnly)
                    {
                        PxrResult anchorRetryResult = m_Native.RetrieveAnchor(
                            snapshot,
                            entity,
                            out ulong retryAnchor);
                        if (anchorRetryResult == PxrResult.SUCCESS)
                        {
                            existing.Anchor = retryAnchor;
                        }
                    }

                    existing.ConsecutiveMissingSnapshots = 0;
                    return PxrResult.SUCCESS;
                }
            }

            bool shouldRetrieveAnchor =
                m_ModeIntent != ImageTrackingModeIntent.RecognitionOnly;
            ulong anchor = 0;
            if (shouldRetrieveAnchor)
            {
                PxrResult retrieveResult = m_Native.RetrieveAnchor(
                    snapshot,
                    entity,
                    out anchor);
                if (retrieveResult != PxrResult.SUCCESS)
                {
                    anchor = 0;
                }
            }

            var trackedImage = new TrackedImage
            {
                Entity = entity,
                Anchor = anchor,
                ReferenceImageGuid = referenceImage.Guid,
                ReferenceImageIndex = checked((int)imageData.referenceImageIndex),
                ImageTrackingDatabase = imageData.imageTrackingDatabase,
                EntityLastUpdateTime = entityLastUpdateTime,
                BoundingBoxOffset = offset,
                Size = extent,
                HasBoundingBox2D = hasBoundingBox2D
            };
            lock (m_Gate)
            {
                if (m_TrackedImages.ContainsKey(entity))
                {
                    m_Native.DestroyAnchor(anchor);
                }
                else
                {
                    m_TrackedImages.Add(entity, trackedImage);
                }
            }

            return PxrResult.SUCCESS;
        }

        private static Vector2 CalculateReferenceImageSize(
            ImageTrackingReferenceRecord referenceImage)
        {
            float height = referenceImage.PhysicalWidth;
            if (referenceImage.PixelWidth > 0)
            {
                height *= (float)referenceImage.PixelHeight
                          / referenceImage.PixelWidth;
            }

            return new Vector2(referenceImage.PhysicalWidth, height);
        }

        private PxrResult RemoveTrackedImagesMissingFromSnapshot(
            HashSet<ulong> observedEntities)
        {
            lock (m_Gate)
            {
                var missingEntities = new List<ulong>();
                foreach (KeyValuePair<ulong, TrackedImage> pair in m_TrackedImages)
                {
                    if (observedEntities.Contains(pair.Key))
                    {
                        pair.Value.ConsecutiveMissingSnapshots = 0;
                        continue;
                    }

                    pair.Value.ConsecutiveMissingSnapshots++;
                    if (pair.Value.ConsecutiveMissingSnapshots
                        >= MissingSnapshotRemovalThreshold)
                    {
                        missingEntities.Add(pair.Key);
                    }
                }

#if PXR_IMAGETRACKING_DEBUG
                var removedEntityParts = new List<string>(missingEntities.Count);
#endif
                foreach (ulong entity in missingEntities)
                {
                    TrackedImage trackedImage = m_TrackedImages[entity];
                    if (trackedImage.Anchor != 0)
                    {
                        PxrResult destroyResult =
                            m_Native.DestroyAnchor(trackedImage.Anchor);
                        if (!IsSuccessOrStaleHandle(destroyResult))
                        {
                            return destroyResult;
                        }
                    }

                    m_TrackedImages.Remove(entity);
                    m_RemovedEntityIds.Add(entity);
#if PXR_IMAGETRACKING_DEBUG
                    removedEntityParts.Add(
                        $"entity={entity} anchor={trackedImage.Anchor} missing={trackedImage.ConsecutiveMissingSnapshots}");
#endif
                }

#if PXR_IMAGETRACKING_DEBUG
                ImageTrackingLog.Verbose(() =>
                {
                    var stateParts = new List<string>(m_TrackedImages.Count);
                    foreach (KeyValuePair<ulong, TrackedImage> pair in m_TrackedImages)
                    {
                        stateParts.Add(
                            $"entity={pair.Key} observed={observedEntities.Contains(pair.Key)} missing={pair.Value.ConsecutiveMissingSnapshots}");
                    }

                    return $"ProviderModule.MissingReconcile observed={observedEntities.Count} tracked={m_TrackedImages.Count} removed={missingEntities.Count} removedEntities=[{string.Join(",", removedEntityParts)}] states=[{string.Join(",", stateParts)}]";
                });
#endif

                return PxrResult.SUCCESS;
            }
        }

        private PxrResult Stop()
        {
            Task<ImageTrackingConfigureResult> pendingConfigure;
            Task<PxrResult> pendingStart;
            Task<PxrResult> pendingQuery;
            lock (m_Gate)
            {
                if (m_State == ImageTrackingProviderState.Disposed || m_DisposeStarted)
                {
                    return PxrResult.ERROR_HANDLE_INVALID;
                }

                m_ConfigureCancellation?.Cancel();
                m_StartCancellation?.Cancel();
                m_QueryCancellation?.Cancel();
                pendingConfigure = m_PendingConfigure;
                pendingStart = m_PendingStart;
                pendingQuery = m_PendingQuery;
            }

            if (!WaitForCleanup(pendingConfigure)
                || !WaitForCleanup(pendingStart)
                || !WaitForCleanup(pendingQuery))
            {
                return PxrResult.TIMEOUT_EXPIRED;
            }

            lock (m_Gate)
            {
                if (m_State == ImageTrackingProviderState.Disposed || m_DisposeStarted)
                {
                    return PxrResult.ERROR_HANDLE_INVALID;
                }

                PxrResult pendingCleanupResult =
                    ResolvePendingNativeCleanup();
                if (pendingCleanupResult != PxrResult.SUCCESS)
                {
                    m_State = ImageTrackingProviderState.Faulted;
                    m_LastStartResult = pendingCleanupResult;
                    return pendingCleanupResult;
                }

                if (m_ProviderMayBeRunning)
                {
                    PxrResult result = m_Native.StopProvider(m_Provider);
                    if (result != PxrResult.SUCCESS)
                    {
                        return result;
                    }

                    m_ProviderMayBeRunning = false;
                }

                m_State = ImageTrackingProviderState.Stopped;
                return PxrResult.SUCCESS;
            }
        }

        private void SetState(ImageTrackingProviderState state)
        {
            lock (m_Gate)
            {
                if (m_State != ImageTrackingProviderState.Disposed)
                {
                    m_State = state;
                }
            }
        }

        private void SetStartTerminalStatus(
            PxrResult result,
            ImageTrackingProviderState state)
        {
            lock (m_Gate)
            {
                m_LastStartResult = result;
                if (m_State != ImageTrackingProviderState.Disposed)
                {
                    m_State = state;
                }
            }
        }

        private void SetProviderMayBeRunning(bool mayBeRunning)
        {
            lock (m_Gate)
            {
                m_ProviderMayBeRunning = mayBeRunning;
            }
        }

        private PxrResult FailDestroy(PxrResult result)
        {
            m_State = ImageTrackingProviderState.Faulted;
            m_LastStartResult = result;
            m_DisposeStarted = false;
            return result;
        }

        private static bool IsSuccessOrStaleHandle(PxrResult result)
        {
            return result == PxrResult.SUCCESS
                   || result == PxrResult.ERROR_HANDLE_INVALID
                   || result == PxrResult.ERROR_VALIDATION_FAILURE;
        }

        private void RemoveDestroyedTrackedImages(
            HashSet<ulong> failedAnchors)
        {
            var removedEntities = new List<ulong>();
            foreach (KeyValuePair<ulong, TrackedImage> pair in m_TrackedImages)
            {
                if (!failedAnchors.Contains(pair.Value.Anchor))
                {
                    removedEntities.Add(pair.Key);
                }
            }

            foreach (ulong entity in removedEntities)
            {
                m_TrackedImages.Remove(entity);
            }
        }

        private void DisposeCancellationSources()
        {
            m_ConfigureCancellation?.Dispose();
            m_ConfigureCancellation = null;
            m_StartCancellation?.Dispose();
            m_StartCancellation = null;
            m_QueryCancellation?.Dispose();
            m_QueryCancellation = null;
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

        private ImageTrackingConfigureResult CreateResult(
            ImageTrackingConfigureStatus status,
            PxrResult nativeResult)
        {
            return new ImageTrackingConfigureResult(status, nativeResult, State);
        }

        private static ImageTrackingConfigureStatus MapNativeFailure(
            PxrResult result)
        {
            switch (result)
            {
                case PxrResult.ERROR_EXTENSION_NOT_PRESENT:
                    return ImageTrackingConfigureStatus.UnsupportedExtension;
                case PxrResult.ERROR_FEATURE_UNSUPPORTED:
                case PxrResult.ERROR_FUNCTION_UNSUPPORTED:
                    return ImageTrackingConfigureStatus.UnsupportedCapability;
                case PxrResult.ERROR_PERMISSION_INSUFFICIENT:
                    return ImageTrackingConfigureStatus.PermissionDenied;
                default:
                    return ImageTrackingConfigureStatus.NativeFailure;
            }
        }

        private void RegisterUnresolvedFuture(
            ulong future,
            PendingFutureKind kind)
        {
            lock (m_Gate)
            {
                m_UnresolvedFutures.Add(new PendingFuture(future, kind));
            }
        }

        private PxrResult ResolvePendingNativeCleanup()
        {
            PxrResult databaseResult =
                m_Database?.ResolvePendingCleanup() ?? PxrResult.SUCCESS;
            if (databaseResult != PxrResult.SUCCESS)
            {
                return databaseResult;
            }

            for (int index = 0; index < m_UnresolvedFutures.Count;)
            {
                PendingFuture pending = m_UnresolvedFutures[index];
                FutureCleanupState cleanupState = m_FutureRunner
                    .RetryCleanupAsync(pending.Handle)
                    .GetAwaiter()
                    .GetResult();
                if (cleanupState == FutureCleanupState.Unresolved)
                {
                    return PxrResult.TIMEOUT_EXPIRED;
                }

                PxrResult completeResult = PxrResult.SUCCESS;
                if (cleanupState == FutureCleanupState.Ready)
                {
                    if (pending.Kind == PendingFutureKind.Start)
                    {
                        completeResult = m_Native.StartProviderComplete(
                            pending.Handle,
                            out XrSenseDataProviderStartCompletion completion);
                        if (completeResult == PxrResult.SUCCESS)
                        {
                            completeResult = completion.futureResult;
                        }

                        m_ProviderMayBeRunning =
                            completeResult == PxrResult.SUCCESS;
                    }
                    else
                    {
                        completeResult = m_Native.QueryComplete(
                            m_Provider,
                            pending.Handle,
                            out XrSenseDataQueryCompletion completion);
                        if (completeResult == PxrResult.SUCCESS)
                        {
                            completeResult = completion.futureResult;
                        }

                        if (completion.snapshotHandle != 0)
                        {
                            PxrResult snapshotResult =
                                m_Native.DestroySnapshot(
                                    completion.snapshotHandle);
                            if (completeResult == PxrResult.SUCCESS)
                            {
                                completeResult = snapshotResult;
                            }
                        }
                    }
                }
                else if (pending.Kind == PendingFutureKind.Start)
                {
                    m_ProviderMayBeRunning = false;
                }

                m_UnresolvedFutures.RemoveAt(index);
                if (completeResult != PxrResult.SUCCESS)
                {
                    return completeResult;
                }
            }

            return PxrResult.SUCCESS;
        }

        private static bool TrySelectReferenceImageFormat(
            IReadOnlyList<XrSpatialReferenceImageFormat> supportedFormats,
            out XrSpatialReferenceImageFormat selectedFormat)
        {
            XrSpatialReferenceImageFormat[] preference =
            {
                XrSpatialReferenceImageFormat.Rgba8888,
                XrSpatialReferenceImageFormat.Rgb888,
                XrSpatialReferenceImageFormat.Yuv420888
            };
            if (supportedFormats != null)
            {
                foreach (XrSpatialReferenceImageFormat preferred in preference)
                {
                    for (int index = 0; index < supportedFormats.Count; index++)
                    {
                        if (supportedFormats[index] == preferred)
                        {
                            selectedFormat = preferred;
                            return true;
                        }
                    }
                }
            }

            selectedFormat = default;
            return false;
        }

        private static bool WaitForCleanup(Task task)
        {
            if (task == null)
            {
                return true;
            }

            try
            {
                if (!task.Wait(TimeSpan.FromMilliseconds(500)))
                {
                    return false;
                }

                task.GetAwaiter().GetResult();
            }
            catch
            {
                // The originating operation owns its failure. Disposal must
                // continue releasing resources after the operation settles.
            }

            return true;
        }
    }
}
#endif
