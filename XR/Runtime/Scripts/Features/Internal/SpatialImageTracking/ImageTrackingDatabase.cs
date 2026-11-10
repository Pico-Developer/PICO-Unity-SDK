#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ByteDance.PICO.XR
{
    internal readonly struct ImageTrackingDatabaseCreateResult
    {
        internal ImageTrackingDatabaseCreateResult(
            PxrResult nativeResult,
            bool cancellationRequested,
            bool timedOut,
            bool cleanupUnresolved = false)
        {
            NativeResult = nativeResult;
            CancellationRequested = cancellationRequested;
            TimedOut = timedOut;
            CleanupUnresolved = cleanupUnresolved;
        }

        internal PxrResult NativeResult { get; }

        internal bool CancellationRequested { get; }

        internal bool TimedOut { get; }

        internal bool CleanupUnresolved { get; }

        internal bool Succeeded =>
            NativeResult == PxrResult.SUCCESS
            && !CancellationRequested
            && !TimedOut
            && !CleanupUnresolved;
    }

    internal sealed class ImageTrackingDatabase : IDisposable
    {
        private readonly ISpatialImageTrackingNativeApi m_Native;
        private readonly ulong m_Provider;
        private readonly IUnmanagedAllocator m_Allocator;
        private ulong m_Handle;
        private ulong m_PendingFuture;
        private FutureRunner m_PendingFutureRunner;
        private MarshalledImageDatabaseInput m_PendingInput;
        private bool m_IsDisposed;

        internal ImageTrackingDatabase(
            ISpatialImageTrackingNativeApi native,
            ulong provider,
            IUnmanagedAllocator allocator)
        {
            m_Native = native ?? throw new ArgumentNullException(nameof(native));
            m_Provider = provider;
            m_Allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        }

        internal ulong Handle => m_Handle;

        internal bool IsCreated => m_Handle != 0;

        internal async Task<ImageTrackingDatabaseCreateResult> CreateAsync(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            XrSpatialReferenceImageFormat format,
            FutureRunner futureRunner,
            CancellationToken cancellationToken)
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ImageTrackingDatabase));
            }

            if (library == null)
            {
                throw new ArgumentNullException(nameof(library));
            }

            if (futureRunner == null)
            {
                throw new ArgumentNullException(nameof(futureRunner));
            }

            ImageTrackingLog.Verbose(() => $"Database.CreateAsync begin provider={m_Provider} libraryCount={library.Count} valid={library.IsValid} validation={library.ValidationReport.Status} format={format} mode={options.ModeIntent} maxMoving={options.MaximumMovingImageCount}");
            ulong completedDatabase = 0;
            MarshalledImageDatabaseInput input =
                ImageReferenceMarshaller.Marshal(
                    CreateInputs(library, options, format),
                    m_Allocator);
            bool inputRetained = false;
            try
            {
                XrSpatialImageTrackingDatabaseCreateInfo createInfo = input.CreateInfo;
                ImageTrackingLog.Verbose(() => $"Database.CreateDatabaseAsync call provider={m_Provider}");
                PxrResult createResult = m_Native.CreateDatabaseAsync(
                    m_Provider,
                    ref createInfo,
                    out ulong future);
                ImageTrackingLog.Verbose(() => $"Database.CreateDatabaseAsync result={createResult} future={future}");
                if (createResult != PxrResult.SUCCESS)
                {
                    Debug.LogError($"[PXR_ImageTrackingDebug] Database.CreateDatabaseAsync failed result={createResult}");
                    return new ImageTrackingDatabaseCreateResult(
                        createResult,
                        cancellationToken.IsCancellationRequested,
                        false);
                }

                m_PendingFuture = future;
                m_PendingFutureRunner = futureRunner;
                m_PendingInput = input;
                inputRetained = true;
                FutureWaitOutcome waitOutcome = await futureRunner.WaitWithOutcomeAsync(
                        future,
                        options.FutureTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                ImageTrackingLog.Verbose(() => $"Database.Wait outcome ready={waitOutcome.FutureReady} poll={waitOutcome.PollResult} timedOut={waitOutcome.TimedOut} canceled={waitOutcome.CancellationRequested} cleanupUnresolved={waitOutcome.CleanupUnresolved}");
                if (waitOutcome.CleanupUnresolved)
                {
                    Debug.LogError($"[PXR_ImageTrackingDebug] Database.Wait cleanup unresolved");
                    return new ImageTrackingDatabaseCreateResult(
                        PxrResult.TIMEOUT_EXPIRED,
                        waitOutcome.CancellationRequested,
                        waitOutcome.TimedOut,
                        true);
                }

                if (!waitOutcome.FutureReady)
                {
                    Debug.LogError($"[PXR_ImageTrackingDebug] Database.Wait future not ready poll={waitOutcome.PollResult} timedOut={waitOutcome.TimedOut} canceled={waitOutcome.CancellationRequested}");
                    ReleasePendingInput();
                    return new ImageTrackingDatabaseCreateResult(
                        waitOutcome.PollResult,
                        waitOutcome.CancellationRequested,
                        waitOutcome.TimedOut);
                }

                ImageTrackingLog.Verbose(() => $"Database.CreateDatabaseComplete call future={future}");
                PxrResult completeResult = m_Native.CreateDatabaseComplete(
                    m_Provider,
                    future,
                    out XrCreateSpatialImageTrackingDatabaseCompletion completion);
                ImageTrackingLog.Verbose(() => $"Database.CreateDatabaseComplete result={completeResult} futureResult={completion.futureResult} database={completion.database}");
                completedDatabase = completion.database;
                PxrResult nativeResult =
                    waitOutcome.PollResult != PxrResult.SUCCESS
                        ? waitOutcome.PollResult
                        : completeResult == PxrResult.SUCCESS
                            ? completion.futureResult
                            : completeResult;

                try
                {
                    if (nativeResult == PxrResult.SUCCESS
                        && !waitOutcome.CancellationRequested
                        && !waitOutcome.TimedOut)
                    {
                        m_Handle = completedDatabase;
                        completedDatabase = 0;
                    }

                    ImageTrackingLog.Verbose(() => $"Database.CreateAsync final nativeResult={nativeResult} handle={m_Handle} canceled={waitOutcome.CancellationRequested} timedOut={waitOutcome.TimedOut}");
                    return new ImageTrackingDatabaseCreateResult(
                        nativeResult,
                        waitOutcome.CancellationRequested,
                        waitOutcome.TimedOut);
                }
                finally
                {
                    if (completedDatabase != 0)
                    {
                        PxrResult destroyResult =
                            m_Native.DestroyDatabase(completedDatabase);
                        if (destroyResult != PxrResult.SUCCESS)
                        {
                            m_Handle = completedDatabase;
                        }
                    }

                    ReleasePendingInput();
                }
            }
            finally
            {
                if (!inputRetained)
                {
                    input.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Destroy();
        }

        internal PxrResult Destroy()
        {
            if (m_IsDisposed)
            {
                return PxrResult.SUCCESS;
            }

            PxrResult cleanupResult = ResolvePendingCleanup();
            if (!IsSuccessOrStaleHandle(cleanupResult))
            {
                // ResolvePendingCleanup may have adopted a freshly-completed
                // native handle into m_Handle before reporting failure; destroy
                // it so the handle is not leaked. Leave m_IsDisposed false so the
                // remaining cleanup can still be retried.
                if (m_Handle != 0
                    && m_Native.DestroyDatabase(m_Handle) == PxrResult.SUCCESS)
                {
                    m_Handle = 0;
                }

                return cleanupResult;
            }
            if (IsStaleHandle(cleanupResult))
            {
                // The pending future or completed database handle is already
                // invalid from the runtime's point of view. Treat it as
                // released and drop retained marshalled input so stale handles
                // do not block higher-level provider teardown/restart.
                ReleasePendingInput();
            }

            if (m_Handle != 0)
            {
                PxrResult result = m_Native.DestroyDatabase(m_Handle);
                if (!IsSuccessOrStaleHandle(result))
                {
                    return result;
                }

                m_Handle = 0;
            }

            m_IsDisposed = true;
            return PxrResult.SUCCESS;
        }

        private static bool IsSuccessOrStaleHandle(PxrResult result)
        {
            return result == PxrResult.SUCCESS
                   || IsStaleHandle(result);
        }

        private static bool IsStaleHandle(PxrResult result)
        {
            return result == PxrResult.ERROR_HANDLE_INVALID
                   || result == PxrResult.ERROR_VALIDATION_FAILURE;
        }

        internal void Abandon()
        {
            ReleasePendingInput();
            m_Handle = 0;
            m_IsDisposed = true;
        }

        internal PxrResult ResolvePendingCleanup()
        {
            if (m_PendingFuture == 0)
            {
                return PxrResult.SUCCESS;
            }

            FutureCleanupState cleanupState = m_PendingFutureRunner
                .RetryCleanupAsync(m_PendingFuture)
                .GetAwaiter()
                .GetResult();
            if (cleanupState == FutureCleanupState.Unresolved)
            {
                return PxrResult.TIMEOUT_EXPIRED;
            }

            if (cleanupState == FutureCleanupState.Ready)
            {
                PxrResult completeResult = m_Native.CreateDatabaseComplete(
                    m_Provider,
                    m_PendingFuture,
                    out XrCreateSpatialImageTrackingDatabaseCompletion completion);
                if (completeResult != PxrResult.SUCCESS)
                {
                    return completeResult;
                }

                if (completion.database != 0)
                {
                    m_Handle = completion.database;
                }

                if (completion.futureResult != PxrResult.SUCCESS)
                {
                    ReleasePendingInput();
                    return completion.futureResult;
                }
            }

            ReleasePendingInput();
            return PxrResult.SUCCESS;
        }

        private void ReleasePendingInput()
        {
            m_PendingInput?.Dispose();
            m_PendingInput = null;
            m_PendingFuture = 0;
            m_PendingFutureRunner = null;
        }

        private static IReadOnlyList<ImageReferenceInput> CreateInputs(
            ImageTrackingRuntimeLibrary library,
            ImageTrackingConfigureOptions options,
            XrSpatialReferenceImageFormat format)
        {
            var inputs = new ImageReferenceInput[library.Count];
            for (int index = 0; index < inputs.Length; index++)
            {
                ImageTrackingReferenceRecord record = library[index];
                options.TryGetOptimizeForStaticImage(
                    record,
                    out bool optimizeForStaticImage);
                inputs[index] = ImageReferenceMarshaller.CreateInput(
                    record,
                    format,
                    optimizeForStaticImage);
            }

            return inputs;
        }
    }
}
#endif
