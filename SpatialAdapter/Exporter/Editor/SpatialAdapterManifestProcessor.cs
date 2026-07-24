using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Android;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    public class SpatialAdapterManifestProcessor : IPostGenerateGradleAndroidProject
    {
        public enum WindowGroupStyle 
        {
            Plain = 1,
            Volumetric = 2
        }

        public enum StageStyle
        {
            Automatic = 0,
            Mixed = 1,
            Progressive = 2,
            Full = 3
        }

        public int callbackOrder => 0;

        private const string customMetaDataStartDelimiter = "<meta-data android:name=\"pxr.spatial.MetaDataStart\" android:value=\"0\" />";
        private const string customMetaDataEndDelimiter = "<meta-data android:name=\"pxr.spatial.MetaDataEnd\" android:value=\"0\" />";

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Path to the AndroidManifest.xml
            string manifestPath = Path.Combine(path, "src/main/AndroidManifest.xml");

            var runtimeSettings = SpatialAdapterRuntimeSettings.GetOrCreateSettings();
            if (!runtimeSettings.m_enableSpatialAdapter)
            {
                return;
            }

            var customMetaData = ToXml(runtimeSettings.m_defaultSpatialCameraConfiguration.GetCustomMetaData());
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("No Android Manifest found! PICO Spatial requires an Android Manifest to properly work on Spatial Engine...");
                return;
            }

            // Inject custom metadata into manifest
            string manifestText = File.ReadAllText(manifestPath);
            manifestText = InjectCustomMetaData(manifestText, customMetaData);
            File.WriteAllText(manifestPath, manifestText);
        }

        private static string ToXml(Dictionary<string, string> customMetaData)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var pair in customMetaData)
            {
                builder.AppendLine($"<meta-data android:name=\"{pair.Key}\" android:value=\"{pair.Value}\" />");
            }

            return builder.ToString();
        }

        private static string InjectCustomMetaData(string manifestText, string customMetaData)
        {
            int startIndex = manifestText.IndexOf(customMetaDataStartDelimiter);
            int endIndex = manifestText.IndexOf(customMetaDataEndDelimiter, startIndex + customMetaDataStartDelimiter.Length);

            if (startIndex == -1 || endIndex == -1) 
            {
                Debug.LogError("Could not inject custom SpatialAdapter meta-data into AndroidManifest.xml. Unable to find delimiters!");
                return manifestText;
            }

            string before = manifestText.Substring(0, startIndex + customMetaDataStartDelimiter.Length);
            string after = manifestText.Substring(endIndex);
            return before + customMetaData + after;
        }
    }
}
