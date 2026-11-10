#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Diagnostics;
using UnityEngine;

namespace ByteDance.PICO.XR
{
    internal static class ImageTrackingLog
    {
        internal const string Tag = "[PXR_ImageTrackingDebug]";

        [Conditional("PXR_IMAGETRACKING_DEBUG")]
        internal static void Verbose(string message)
        {
            UnityEngine.Debug.Log($"{Tag} {message}");
        }

        [Conditional("PXR_IMAGETRACKING_DEBUG")]
        internal static void Verbose(Func<string> messageFactory)
        {
            if (messageFactory == null)
            {
                return;
            }

            UnityEngine.Debug.Log($"{Tag} {messageFactory()}");
        }
    }
}
#endif
