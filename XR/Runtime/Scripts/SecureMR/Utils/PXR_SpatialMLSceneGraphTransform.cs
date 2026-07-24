#if !ENABLE_PICO_OPENXR_SDK
using UnityEngine;

namespace ByteDance.PICO.SecureMR
{
    public static class SpatialMLSceneGraphTransform
    {
        public static readonly Quaternion UnityToSpatialEngineRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);

        public static Vector3 UnityToSpatialEnginePosition(Vector3 unityPosition)
        {
            return UnityToSpatialEngineRotation * unityPosition;
        }

        public static Quaternion UnityToSpatialEngineRotationQuaternion(Quaternion unityRotation)
        {
            return UnityToSpatialEngineRotation * unityRotation;
        }

        public static Quaternion UnityEulerToSpatialEngineRotation(Vector3 unityEulerAngles)
        {
            return UnityToSpatialEngineRotationQuaternion(Quaternion.Euler(unityEulerAngles));
        }

        public static Matrix4x4 UnityTRSToSpatialEngineLocalMatrix(
            Vector3 unityPosition,
            Quaternion unityRotation,
            Vector3 unityScale)
        {
            var position = UnityToSpatialEnginePosition(unityPosition);
            var rotation = UnityToSpatialEngineRotationQuaternion(unityRotation);
            return Matrix4x4.TRS(position, rotation, unityScale);
        }
    }
}
#endif
