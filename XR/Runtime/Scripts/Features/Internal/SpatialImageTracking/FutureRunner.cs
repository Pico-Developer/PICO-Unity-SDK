#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ByteDance.PICO.XR
{
    internal enum FutureCleanupState
    {
        NotRequired,
        Canceled,
        Ready,
        Unresolved
    }

    internal readonly struct FutureWaitOutcome
    {
        internal FutureWaitOutcome(
            PxrResult pollResult,
            bool cancellationRequested,
            bool timedOut,
            bool futureReady,
            FutureCleanupState cleanupState)
        {
            PollResult = pollResult;
            CancellationRequested = cancellationRequested;
            TimedOut = timedOut;
            FutureReady = futureReady;
            CleanupState = cleanupState;
        }

        internal PxrResult PollResult { get; }

        internal bool CancellationRequested { get; }

        internal bool TimedOut { get; }

        internal bool FutureReady { get; }

        internal FutureCleanupState CleanupState { get; }

        internal bool CleanupUnresolved =>
            CleanupState == FutureCleanupState.Unresolved;
    }

    internal sealed class FutureRunner
    {
        private const int PollDelayMilliseconds = 11;
        private static readonly TimeSpan CleanupTimeout =
            TimeSpan.FromMilliseconds(200);

        private readonly ISpatialImageTrackingNativeApi m_Native;

        internal FutureRunner(ISpatialImageTrackingNativeApi native)
        {
            m_Native = native ?? throw new ArgumentNullException(nameof(native));
        }

        internal async Task<PxrResult> WaitAsync(
            ulong future,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            FutureWaitOutcome outcome = await WaitWithOutcomeAsync(
                    future,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return outcome.TimedOut
                ? PxrResult.TIMEOUT_EXPIRED
                : outcome.PollResult;
        }

        internal async Task<FutureWaitOutcome> WaitWithOutcomeAsync(
            ulong future,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            DateTime startedAt = DateTime.UtcNow;
            bool cancellationRequested = cancellationToken.IsCancellationRequested;
            bool timedOut = false;

            while (true)
            {
                cancellationRequested |= cancellationToken.IsCancellationRequested;
                if (!timedOut
                    && timeout != Timeout.InfiniteTimeSpan
                    && DateTime.UtcNow - startedAt >= timeout)
                {
                    timedOut = true;
                }

                if (cancellationRequested || timedOut)
                {
                    FutureCleanupState cleanupState =
                        await CancelAndWaitForCleanupAsync(future)
                        .ConfigureAwait(false);
                    return new FutureWaitOutcome(
                        cleanupState == FutureCleanupState.Unresolved
                            ? PxrResult.TIMEOUT_EXPIRED
                            : PxrResult.SUCCESS,
                        cancellationRequested,
                        timedOut,
                        cleanupState == FutureCleanupState.Ready,
                        cleanupState);
                }

                PxrResult pollResult = m_Native.PollFuture(
                    future,
                    out PxrFutureState state);
                if (pollResult != PxrResult.SUCCESS)
                {
                    FutureCleanupState cleanupState =
                        await CancelAndWaitForCleanupAsync(future)
                        .ConfigureAwait(false);
                    return new FutureWaitOutcome(
                        cleanupState == FutureCleanupState.Unresolved
                            ? PxrResult.TIMEOUT_EXPIRED
                            : pollResult,
                        cancellationRequested,
                        timedOut,
                        cleanupState == FutureCleanupState.Ready,
                        cleanupState);
                }

                if (state == PxrFutureState.Ready)
                {
                    return new FutureWaitOutcome(
                        PxrResult.SUCCESS,
                        cancellationRequested,
                        timedOut,
                        true,
                        FutureCleanupState.NotRequired);
                }

                await Task.Delay(PollDelayMilliseconds).ConfigureAwait(false);
            }
        }

        internal Task<FutureCleanupState> RetryCleanupAsync(ulong future)
        {
            return CancelAndWaitForCleanupAsync(future);
        }

        private async Task<FutureCleanupState> CancelAndWaitForCleanupAsync(
            ulong future)
        {
            if (m_Native.CancelFuture(future) == PxrResult.SUCCESS)
            {
                return FutureCleanupState.Canceled;
            }

            DateTime deadline = DateTime.UtcNow + CleanupTimeout;
            while (DateTime.UtcNow < deadline)
            {
                PxrResult pollResult = m_Native.PollFuture(
                    future,
                    out PxrFutureState state);
                if (pollResult == PxrResult.SUCCESS
                    && state == PxrFutureState.Ready)
                {
                    return FutureCleanupState.Ready;
                }

                await Task.Delay(PollDelayMilliseconds).ConfigureAwait(false);
            }

            return FutureCleanupState.Unresolved;
        }
    }
}
#endif
