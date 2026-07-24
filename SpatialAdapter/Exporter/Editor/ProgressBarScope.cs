using System;
using UnityEditor;
using UnityEditor.Build;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    internal class ProgressBarScope : IDisposable
    {
        public void Dispose()
        {
            EditorUtility.ClearProgressBar();
        }

        public void Display(string message, float progress)
        {
            EditorUtility.DisplayProgressBar("SpatialAdapter Exporter", message, progress);
        }
    }
}