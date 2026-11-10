#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;

namespace ByteDance.PICO.XR
{
    internal sealed class SnapshotLease : IDisposable
    {
        private readonly ISpatialImageTrackingNativeApi m_Native;
        private bool m_IsDisposed;

        internal SnapshotLease(
            ISpatialImageTrackingNativeApi native,
            ulong handle)
        {
            m_Native = native ?? throw new ArgumentNullException(nameof(native));
            Handle = handle;
        }

        internal ulong Handle { get; }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            m_IsDisposed = true;
            if (Handle != 0)
            {
                m_Native.DestroySnapshot(Handle);
            }
        }
    }
}
#endif
