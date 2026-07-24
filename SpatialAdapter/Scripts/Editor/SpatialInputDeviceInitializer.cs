using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Editor
{
    public static class SpatialInputDeviceInitializer
    {
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnValidate()
        {
            SpatialInputDevice.Initialize();
        }
    }
}