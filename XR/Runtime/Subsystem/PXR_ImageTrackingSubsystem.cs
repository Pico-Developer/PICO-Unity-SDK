#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    public sealed class PXR_ImageTrackingSubsystem : XRImageTrackingSubsystem
    {
        internal const string k_SubsystemId = "PXR_ImageTrackingSubsystem";
        private const string k_LogTag = "[PXR_ImageTrackingDebug]";

        internal sealed class ImageTrackingProvider : Provider
        {
            private readonly object m_Gate = new object();
            private readonly IImageTrackingBackend m_Backend;

            private CancellationTokenSource m_ConfigurationCancellation;
            private Task m_ConfigurationTask = Task.CompletedTask;
            private PXRRuntimeReferenceImageLibrary m_ImageLibrary;
            private Exception m_LastConfigurationException;
            private long m_ConfigurationGeneration;
            private long m_ConfigurationTargetRuntimeGeneration = -1;
            private long m_ConfiguredRuntimeGeneration = -1;
            private ImageTrackingConfigureOptions m_ConfigurationOptions;
            private int m_RequestedMaxNumberOfMovingImages = 1;
            private int m_AppliedMaxNumberOfMovingImages;
            private bool m_IsConfigured;
            private bool m_IsRunning;
            private bool m_IsDestroyed;

            public ImageTrackingProvider()
                : this(ImageTrackingBackendFactory.Create())
            {
            }

            internal ImageTrackingProvider(IImageTrackingBackend backend)
            {
                m_Backend =
                    backend ?? throw new ArgumentNullException(nameof(backend));
                PXRImageTrackingOptionsRegistry.OptionsChanged +=
                    OnPicoOptionsChanged;
            }

            internal Task configurationTask
            {
                get
                {
                    lock (m_Gate)
                    {
                        return m_ConfigurationTask;
                    }
                }
            }

            internal Exception lastConfigurationException
            {
                get
                {
                    lock (m_Gate)
                    {
                        return m_LastConfigurationException;
                    }
                }
            }

            public override RuntimeReferenceImageLibrary imageLibrary
            {
                set
                {
                    ImageTrackingLog.Verbose(() => $"Provider.imageLibrary setter valueType={value?.GetType().Name ?? "null"}");
                    PXRRuntimeReferenceImageLibrary runtimeLibrary;
                    if (value == null)
                    {
                        runtimeLibrary = null;
                    }
                    else
                    {
                        runtimeLibrary =
                            value as PXRRuntimeReferenceImageLibrary;
                        if (runtimeLibrary == null)
                        {
                            throw new ArgumentException(
                                $"{nameof(imageLibrary)} must be created by "
                                + $"{nameof(PXR_ImageTrackingSubsystem)}.",
                                nameof(value));
                        }
                    }

                    ImageTrackingLog.Verbose(() => $"Provider.imageLibrary scheduling config runtimeLibraryNull={runtimeLibrary == null} count={runtimeLibrary?.count ?? -1}");
                    ScheduleConfiguration(runtimeLibrary);
                }
            }

            public override int requestedMaxNumberOfMovingImages
            {
                get
                {
                    lock (m_Gate)
                    {
                        return m_RequestedMaxNumberOfMovingImages;
                    }
                }
                set
                {
                    if (value == 0)
                    {
                        value = 1;
                    }

                    int maximum = m_Backend.Capabilities.MaximumMovingImageCount;
                    if (value < 1 || value > maximum)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(value),
                            value,
                            $"The requested count must be between 1 and {maximum}.");
                    }

                    PXRRuntimeReferenceImageLibrary imageLibraryToReconfigure;
                    lock (m_Gate)
                    {
                        ThrowIfDestroyedLocked();
                        if (value == m_RequestedMaxNumberOfMovingImages)
                        {
                            return;
                        }

                        m_RequestedMaxNumberOfMovingImages = value;
                        imageLibraryToReconfigure = m_ImageLibrary;
                    }

                    if (imageLibraryToReconfigure != null)
                    {
                        ScheduleConfiguration(imageLibraryToReconfigure);
                    }
                }
            }

            public override int currentMaxNumberOfMovingImages
            {
                get
                {
                    lock (m_Gate)
                    {
                        return m_AppliedMaxNumberOfMovingImages;
                    }
                }
            }

            public override RuntimeReferenceImageLibrary CreateRuntimeLibrary(
                XRReferenceImageLibrary serializedLibrary)
            {
                lock (m_Gate)
                {
                    ThrowIfDestroyedLocked();
                }

                ImageTrackingLog.Verbose(() => $"CreateRuntimeLibrary serializedNull={serializedLibrary == null} count={serializedLibrary?.count ?? -1}");
                ImageTrackingRuntimeLibrary backendLibrary =
                    m_Backend.CreateRuntimeLibrary(serializedLibrary);
                ImageTrackingLog.Verbose(() => $"CreateRuntimeLibrary backend count={backendLibrary.Count} valid={backendLibrary.IsValid} status={backendLibrary.ValidationReport.Status} warning={backendLibrary.ValidationReport.Warning} message={backendLibrary.ValidationReport.Message}");
                return new PXRRuntimeReferenceImageLibrary(
                    serializedLibrary,
                    backendLibrary);
            }

            public override void Start()
            {
                PXRRuntimeReferenceImageLibrary recoveryLibrary = null;
                ImageTrackingConfigureOptions recoveryOptions = default;
                long expectedConfigurationGeneration = -1;
                bool enableTracking;
                lock (m_Gate)
                {
                    ThrowIfDestroyedLocked();
                    m_IsRunning = true;
                    long runtimeGeneration = m_Backend.RuntimeGeneration;
                    bool configurationInFlight =
                        !m_ConfigurationTask.IsCompleted;
                    bool pendingConfigurationMatchesRuntime =
                        configurationInFlight
                        && (m_ConfigurationTargetRuntimeGeneration < 0
                            || m_ConfigurationTargetRuntimeGeneration
                            == runtimeGeneration);
                    bool requiresRecovery =
                        m_ImageLibrary != null
                        && ((m_IsConfigured
                             && m_ConfiguredRuntimeGeneration
                             != runtimeGeneration)
                            || (!m_IsConfigured
                                && !pendingConfigurationMatchesRuntime));
                    if (requiresRecovery)
                    {
                        recoveryLibrary = m_ImageLibrary;
                        recoveryOptions = m_ConfigurationOptions;
                        expectedConfigurationGeneration =
                            m_ConfigurationGeneration;
                    }

                    enableTracking = m_IsConfigured && !requiresRecovery;
                }

                ImageTrackingLog.Verbose(() => $"Provider.Start running={m_IsRunning} configured={m_IsConfigured} imageLibraryNull={m_ImageLibrary == null} enableTracking={enableTracking} recovery={recoveryLibrary != null}");
                if (recoveryLibrary != null)
                {
                    ScheduleConfiguration(
                        recoveryLibrary,
                        recoveryOptions,
                        expectedConfigurationGeneration);
                }
                else if (enableTracking)
                {
                    ImageTrackingLog.Verbose("Provider.Start SetTrackingEnabled(true)");
                    m_Backend.SetTrackingEnabled(true);
                }
            }

            public override void Stop()
            {
                CancellationTokenSource cancellation;
                lock (m_Gate)
                {
                    if (m_IsDestroyed)
                    {
                        return;
                    }

                    m_IsRunning = false;
                    cancellation = m_ConfigurationCancellation;
                    m_ConfigurationCancellation = null;
                }

                Cancel(cancellation);
                m_Backend.SetTrackingEnabled(false);
            }

            public override void Destroy()
            {
                PXRImageTrackingOptionsRegistry.OptionsChanged -=
                    OnPicoOptionsChanged;
                CancellationTokenSource cancellation;
                lock (m_Gate)
                {
                    if (m_IsDestroyed)
                    {
                        return;
                    }

                    m_IsDestroyed = true;
                    m_IsRunning = false;
                    m_IsConfigured = false;
                    m_ImageLibrary = null;
                    m_ConfigurationTargetRuntimeGeneration = -1;
                    m_ConfiguredRuntimeGeneration = -1;
                    cancellation = m_ConfigurationCancellation;
                    m_ConfigurationCancellation = null;
                }

                Cancel(cancellation);
                try
                {
                    m_Backend.SetTrackingEnabled(false);
                }
                finally
                {
                    m_Backend.Dispose();
                }
            }

            internal bool TryGetMetadata(
                TrackableId trackableId,
                out PXR_TrackedImageMetadata metadata)
            {
                return m_Backend.TryGetMetadata(trackableId, out metadata);
            }

            internal PXR_TrackedImageMetadata[] GetMetadataSnapshot()
            {
                return m_Backend.GetMetadataSnapshot();
            }

            public override TrackableChanges<XRTrackedImage> GetChanges(
                XRTrackedImage defaultTrackedImage,
                Allocator allocator)
            {
                ImageTrackingChanges backendChanges =
                    m_Backend.AcquireChanges();
                XRTrackedImage[] added =
                    backendChanges.Added ?? Array.Empty<XRTrackedImage>();
                XRTrackedImage[] updated =
                    backendChanges.Updated ?? Array.Empty<XRTrackedImage>();
                TrackableId[] removed =
                    backendChanges.Removed ?? Array.Empty<TrackableId>();

                NativeArray<XRTrackedImage> addedArray = default;
                NativeArray<XRTrackedImage> updatedArray = default;
                NativeArray<TrackableId> removedArray = default;
                try
                {
                    addedArray = new NativeArray<XRTrackedImage>(
                        added,
                        allocator);
                    updatedArray = new NativeArray<XRTrackedImage>(
                        updated,
                        allocator);
                    removedArray = new NativeArray<TrackableId>(
                        removed,
                        allocator);

                    var changes = new TrackableChanges<XRTrackedImage>(
                        addedArray,
                        updatedArray,
                        removedArray);
                    addedArray = default;
                    updatedArray = default;
                    removedArray = default;
                    return changes;
                }
                catch
                {
                    if (addedArray.IsCreated)
                    {
                        addedArray.Dispose();
                    }

                    if (updatedArray.IsCreated)
                    {
                        updatedArray.Dispose();
                    }

                    if (removedArray.IsCreated)
                    {
                        removedArray.Dispose();
                    }

                    throw;
                }
            }

            private void ScheduleConfiguration(
                PXRRuntimeReferenceImageLibrary runtimeLibrary)
            {
                ScheduleConfiguration(
                    runtimeLibrary,
                    preservedOptions: null,
                    expectedConfigurationGeneration: null);
            }

            private void ScheduleConfiguration(
                PXRRuntimeReferenceImageLibrary runtimeLibrary,
                ImageTrackingConfigureOptions? preservedOptions,
                long? expectedConfigurationGeneration)
            {
                CancellationTokenSource previousCancellation;
                CancellationTokenSource cancellation;
                ImageTrackingLog.Verbose(() => $"ScheduleConfiguration enter runtimeLibraryNull={runtimeLibrary == null} count={runtimeLibrary?.count ?? -1} preserved={preservedOptions.HasValue} expectedGeneration={expectedConfigurationGeneration?.ToString() ?? "null"}");
                Task previousConfiguration;
                TaskCompletionSource<bool> completion;
                long generation;
                long targetRuntimeGeneration;
                ImageTrackingConfigureOptions options;
                bool disableTracking;
                lock (m_Gate)
                {
                    ThrowIfDestroyedLocked();
                    if (expectedConfigurationGeneration.HasValue
                        && (expectedConfigurationGeneration.Value
                            != m_ConfigurationGeneration
                            || !ReferenceEquals(m_ImageLibrary, runtimeLibrary)
                            || !m_IsRunning))
                    {
                        return;
                    }

                    previousCancellation = m_ConfigurationCancellation;
                    previousConfiguration = m_ConfigurationTask;
                    cancellation = new CancellationTokenSource();
                    completion = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    m_ConfigurationCancellation = cancellation;
                    m_ConfigurationTask = completion.Task;
                    disableTracking =
                        !expectedConfigurationGeneration.HasValue
                        && m_IsRunning
                        && m_IsConfigured;
                    m_IsConfigured = false;
                    m_ImageLibrary = runtimeLibrary;
                    m_LastConfigurationException = null;
                    generation = ++m_ConfigurationGeneration;
                    options = preservedOptions
                              ?? CreateOptionsLocked(runtimeLibrary);
                    m_ConfigurationOptions = options;
                    targetRuntimeGeneration = m_Backend.RuntimeGeneration;
                    m_ConfigurationTargetRuntimeGeneration =
                        targetRuntimeGeneration;
                }

                Cancel(previousCancellation);
                _ = CompleteConfigurationSeriallyAsync(
                    runtimeLibrary,
                    options,
                    generation,
                    targetRuntimeGeneration,
                    disableTracking,
                    previousConfiguration,
                    cancellation,
                    completion);
            }


            private void OnPicoOptionsChanged()
            {
                PXRRuntimeReferenceImageLibrary runtimeLibrary;
                lock (m_Gate)
                {
                    if (m_IsDestroyed || m_ImageLibrary == null)
                    {
                        return;
                    }

                    runtimeLibrary = m_ImageLibrary;
                }

                ScheduleConfiguration(runtimeLibrary);
            }

            private ImageTrackingConfigureOptions CreateOptionsLocked(
                PXRRuntimeReferenceImageLibrary runtimeLibrary)
            {
                PXRImageTrackingOptions picoOptions =
                    PXRImageTrackingOptionsRegistry.Current;
                return new ImageTrackingConfigureOptions(
                    picoOptions.ToModeIntent(),
                    ImageTrackingConfigureOptions.Default.FutureTimeout,
                    runtimeLibrary == null
                        ? 0
                        : m_RequestedMaxNumberOfMovingImages,
                    picoOptions.LibraryHints);
            }

            private async Task CompleteConfigurationSeriallyAsync(
                PXRRuntimeReferenceImageLibrary runtimeLibrary,
                ImageTrackingConfigureOptions options,
                long generation,
                long targetRuntimeGeneration,
                bool disableTracking,
                Task previousConfiguration,
                CancellationTokenSource cancellation,
                TaskCompletionSource<bool> completion)
            {
                try
                {
                    await ObserveCompletionAsync(previousConfiguration)
                        .ConfigureAwait(false);

                    lock (m_Gate)
                    {
                        if (m_IsDestroyed)
                        {
                            return;
                        }
                    }

                    if (disableTracking)
                    {
                        m_Backend.SetTrackingEnabled(false);
                    }

                    if (runtimeLibrary == null)
                    {
                        lock (m_Gate)
                        {
                            if (!m_IsDestroyed
                                && generation == m_ConfigurationGeneration
                                && !cancellation.IsCancellationRequested)
                            {
                                m_AppliedMaxNumberOfMovingImages = 0;
                                m_ConfiguredRuntimeGeneration =
                                    targetRuntimeGeneration;
                            }
                        }

                        return;
                    }

                    ImageTrackingLog.Verbose(() => $"CompleteConfiguration EnsureStartPreconditionsAsync begin generation={generation}");
                    PxrResult preconditionResult =
                        await m_Backend.EnsureStartPreconditionsAsync(
                                cancellation.Token)
                            .ConfigureAwait(false);
                    ImageTrackingLog.Verbose(() => $"CompleteConfiguration EnsureStartPreconditionsAsync result={preconditionResult}");
                    ImageTrackingConfigureResult result;
                    if (preconditionResult != PxrResult.SUCCESS)
                    {
                        result = new ImageTrackingConfigureResult(
                            ImageTrackingConfigureStatus.PermissionDenied,
                            preconditionResult,
                            ImageTrackingProviderState.Uninitialized);
                    }
                    else
                    {
                        ImageTrackingLog.Verbose(() => $"CompleteConfiguration ConfigureAsync begin generation={generation} count={runtimeLibrary.backendLibrary.Count} valid={runtimeLibrary.backendLibrary.IsValid} status={runtimeLibrary.backendLibrary.ValidationReport.Status} message={runtimeLibrary.backendLibrary.ValidationReport.Message}");
                        result = await m_Backend.ConfigureAsync(
                                runtimeLibrary.backendLibrary,
                                options,
                                cancellation.Token)
                            .ConfigureAwait(false);
                    }
                    ImageTrackingLog.Verbose(() => $"CompleteConfiguration ConfigureAsync result status={result.Status} native={result.NativeResult} state={result.State}");
                    bool succeeded =
                        result.Status == ImageTrackingConfigureStatus.Success
                        || result.Status == ImageTrackingConfigureStatus.Degraded;
                    bool enableTracking = false;

                    lock (m_Gate)
                    {
                        if (!m_IsDestroyed
                            && generation == m_ConfigurationGeneration
                            && !cancellation.IsCancellationRequested)
                        {
                            m_IsConfigured = succeeded;
                            if (succeeded)
                            {
                                m_AppliedMaxNumberOfMovingImages =
                                    options.MaximumMovingImageCount;
                                m_ConfiguredRuntimeGeneration =
                                    m_Backend.RuntimeGeneration;
                                enableTracking = m_IsRunning;
                            }
                            else
                            {
                                m_ConfiguredRuntimeGeneration = -1;
                                m_LastConfigurationException =
                                    new InvalidOperationException(
                                        "Image tracking configuration failed "
                                        + $"with {result.Status} "
                                        + $"({result.NativeResult}).");
                            }
                        }
                    }

                    if (enableTracking)
                    {
                        ImageTrackingLog.Verbose("CompleteConfiguration SetTrackingEnabled(true)");
                        m_Backend.SetTrackingEnabled(true);
                        bool disableAfterStateChange;
                        lock (m_Gate)
                        {
                            disableAfterStateChange =
                                m_IsDestroyed
                                || generation != m_ConfigurationGeneration
                                || !m_IsRunning
                                || !m_IsConfigured;
                        }

                        if (disableAfterStateChange)
                        {
                            ImageTrackingLog.Verbose("CompleteConfiguration disable-after-state-change SetTrackingEnabled(false)");
                            m_Backend.SetTrackingEnabled(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                    when (cancellation.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    Debug.LogError($"{k_LogTag} CompleteConfiguration exception: {exception}");
                    lock (m_Gate)
                    {
                        if (!m_IsDestroyed
                            && generation == m_ConfigurationGeneration
                            && !cancellation.IsCancellationRequested)
                        {
                            m_IsConfigured = false;
                            m_LastConfigurationException = exception;
                        }
                    }
                }
                finally
                {
                    lock (m_Gate)
                    {
                        if (ReferenceEquals(
                                m_ConfigurationCancellation,
                                cancellation))
                        {
                            m_ConfigurationCancellation = null;
                        }
                    }

                    cancellation.Dispose();
                    completion.TrySetResult(true);
                }
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
                    // Each operation records its own failure. Successors wait
                    // only until the predecessor has completed cleanup.
                }
            }

            private void Cancel(CancellationTokenSource cancellation)
            {
                if (cancellation == null)
                {
                    return;
                }

                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Cleanup won the race; no in-flight operation remains.
                }
                catch (Exception exception)
                {
                    Debug.LogError($"{k_LogTag} CompleteConfiguration exception: {exception}");
                    lock (m_Gate)
                    {
                        m_LastConfigurationException = exception;
                    }
                }
            }

            private void ThrowIfDestroyedLocked()
            {
                if (m_IsDestroyed)
                {
                    throw new ObjectDisposedException(
                        nameof(ImageTrackingProvider));
                }
            }
        }

        private sealed class PXRRuntimeReferenceImageLibrary
            : RuntimeReferenceImageLibrary
        {
            private readonly XRReferenceImageLibrary m_SerializedLibrary;

            internal PXRRuntimeReferenceImageLibrary(
                XRReferenceImageLibrary serializedLibrary,
                ImageTrackingRuntimeLibrary backendLibrary)
            {
                m_SerializedLibrary = serializedLibrary;
                this.backendLibrary =
                    backendLibrary
                    ?? throw new ArgumentNullException(nameof(backendLibrary));
            }

            internal ImageTrackingRuntimeLibrary backendLibrary { get; }

            public override int count =>
                m_SerializedLibrary == null ? 0 : m_SerializedLibrary.count;

            protected override XRReferenceImage GetReferenceImageAt(int index)
            {
                return m_SerializedLibrary[index];
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void RegisterDescriptor()
        {
            var cinfo = new XRImageTrackingSubsystemDescriptor.Cinfo
            {
                id = k_SubsystemId,
                providerType = typeof(ImageTrackingProvider),
                subsystemTypeOverride = typeof(PXR_ImageTrackingSubsystem),
                supportsMovingImages = true,
                supportsMutableLibrary = false,
                requiresPhysicalImageDimensions = true
            };

            XRImageTrackingSubsystemDescriptor.Register(cinfo);
        }
    }
}
#endif

