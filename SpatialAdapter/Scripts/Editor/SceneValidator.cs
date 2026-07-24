using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Editor {
    public class SceneValidator : IProcessSceneWithReport
    {
        public int callbackOrder => -1;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // only require Spatial Camera if SpatialAdapter is enabled, do not require SpatialCamera for now
            SpatialCamera[] spatialCameras = Object.FindObjectsOfType<SpatialCamera>();
            if (spatialCameras.Length == 0)
            {
                Debug.LogWarning($"SpatialAdapter: Spatial Camera missing in scene {scene.name}!");
            }
            else if (spatialCameras.Length > 1)
            {
                throw new BuildFailedException($"SpatialAdapter: Multiple Spatial Cameras found in scene {scene.name}! Each scene may only contain one Spatial Camera...");
            }
        }
    }
}
