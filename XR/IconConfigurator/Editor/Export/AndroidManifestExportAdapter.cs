using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;

namespace ByteDance.PICO.IconConfigurator.Editor
{
    public class AndroidManifestExportAdapter : IIconExportAdapter
    {
        private const string k_ApplicationElementName = "application";
        private const string k_MetaDataElementName = "meta-data";
        private const string k_Icon3DListName = "icon.3d.list";
        private const string k_SdfListName = "icon.sdf.list";

        private static readonly XNamespace s_androidNamespace = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace s_toolsNamespace = "http://schemas.android.com/tools";
        private readonly string m_manifestPath;
        private readonly string m_launcherManifestPath;
        private readonly bool m_updatePlayerSettings;

        public AndroidManifestExportAdapter()
            : this(
                IconConfiguratorPaths.AndroidManifestPath,
                IconConfiguratorPaths.AndroidLauncherManifestPath,
                true)
        {
        }

        public AndroidManifestExportAdapter(string manifestPath)
            : this(manifestPath, manifestPath, false)
        {
        }

        public AndroidManifestExportAdapter(
            string manifestPath,
            string launcherManifestPath,
            bool updatePlayerSettings)
        {
            m_manifestPath = manifestPath;
            m_launcherManifestPath = launcherManifestPath;
            m_updatePlayerSettings = updatePlayerSettings;
        }

        public void Apply(IconApplyPayload payload)
        {
            PatchMainManifest();
            PatchLauncherManifest();

            if (m_updatePlayerSettings)
            {
                TrySetAndroidBoolProperty("useCustomMainManifest", true);
                TrySetAndroidBoolProperty("useCustomLauncherManifest", true);
                EnsureProjectSettingsFlag("useCustomMainManifest");
                EnsureProjectSettingsFlag("useCustomLauncherManifest");
            }
        }

        private void PatchMainManifest()
        {
            XDocument document = LoadOrCreateManifest(m_manifestPath);
            XElement manifest = document.Root ?? CreateManifestRoot(document);
            EnsureAndroidNamespace(manifest);
            EnsureApplication(manifest);
            SaveManifest(document, m_manifestPath);
        }

        private void PatchLauncherManifest()
        {
            XDocument document = LoadOrCreateManifest(m_launcherManifestPath);
            XElement manifest = document.Root ?? CreateManifestRoot(document);
            EnsureAndroidNamespace(manifest);
            EnsureToolsNamespace(manifest);

            XElement application = EnsureApplication(manifest);
            application.SetAttributeValue(s_androidNamespace + "icon", "@mipmap/ic_spatial_launcher");
            application.SetAttributeValue(s_androidNamespace + "label", IconConfiguratorAndroidResources.AppNameReference);
            application.SetAttributeValue(s_androidNamespace + "localeConfig", "@xml/locales_config");
            application.SetAttributeValue(s_toolsNamespace + "replace", "android:icon,android:label");

            UpsertMetadata(application, k_Icon3DListName, "@array/icon_3d_list");
            UpsertMetadata(application, k_SdfListName, "@array/icon_sdf_list");

            SaveManifest(document, m_launcherManifestPath);
        }

        private static XDocument LoadOrCreateManifest(string manifestPath)
        {
            EnsureFolder(Path.GetDirectoryName(manifestPath)?.Replace("\\", "/"));
            return File.Exists(manifestPath)
                ? XDocument.Load(manifestPath)
                : CreateDefaultManifest();
        }

        private static XElement EnsureApplication(XElement manifest)
        {
            XElement application = manifest.Element(k_ApplicationElementName);
            if (application == null)
            {
                application = new XElement(k_ApplicationElementName);
                manifest.Add(application);
            }

            return application;
        }

        private static void SaveManifest(XDocument document, string manifestPath)
        {
            EnsureFolder(Path.GetDirectoryName(manifestPath)?.Replace("\\", "/"));
            document.Save(manifestPath);
            AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static XDocument CreateDefaultManifest()
        {
            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(
                    "manifest",
                    new XAttribute(XNamespace.Xmlns + "android", s_androidNamespace.NamespaceName),
                    new XElement(k_ApplicationElementName)));
        }

        private static XElement CreateManifestRoot(XDocument document)
        {
            XElement manifest = new XElement("manifest");
            document.Add(manifest);
            return manifest;
        }

        private static void EnsureAndroidNamespace(XElement manifest)
        {
            XAttribute namespaceAttribute = manifest.Attribute(XNamespace.Xmlns + "android");
            if (namespaceAttribute == null)
            {
                manifest.SetAttributeValue(XNamespace.Xmlns + "android", s_androidNamespace.NamespaceName);
            }
        }

        private static void EnsureToolsNamespace(XElement manifest)
        {
            XAttribute namespaceAttribute = manifest.Attribute(XNamespace.Xmlns + "tools");
            if (namespaceAttribute == null)
            {
                manifest.SetAttributeValue(XNamespace.Xmlns + "tools", s_toolsNamespace.NamespaceName);
            }
        }

        private static void UpsertMetadata(XElement application, string name, string resource)
        {
            XElement metadata = application.Elements(k_MetaDataElementName)
                .FirstOrDefault(element => element.Attribute(s_androidNamespace + "name")?.Value == name);

            if (metadata == null)
            {
                metadata = new XElement(k_MetaDataElementName);
                application.Add(metadata);
            }

            metadata.SetAttributeValue(s_androidNamespace + "name", name);
            metadata.SetAttributeValue(s_androidNamespace + "resource", resource);
        }

        private static void TrySetAndroidBoolProperty(string propertyName, bool value)
        {
            PropertyInfo property = typeof(PlayerSettings.Android).GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Static);

            if (property == null || property.PropertyType != typeof(bool) || !property.CanWrite)
            {
                return;
            }

            property.SetValue(null, value);
        }

        private static void EnsureProjectSettingsFlag(string flagName)
        {
            const string projectSettingsPath = "ProjectSettings/ProjectSettings.asset";
            if (!File.Exists(projectSettingsPath))
            {
                return;
            }

            string content = File.ReadAllText(projectSettingsPath);
            string pattern = $@"(?m)^(\s*{Regex.Escape(flagName)}:\s*)\d+\s*$";

            if (!Regex.IsMatch(content, pattern))
            {
                return;
            }

            string updatedContent = Regex.Replace(content, pattern, match => $"{match.Groups[1].Value}1");
            if (updatedContent != content)
            {
                File.WriteAllText(projectSettingsPath, updatedContent);
                AssetDatabase.ImportAsset(projectSettingsPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (Directory.Exists(path))
            {
                return;
            }

            string normalizedPath = path.Replace("\\", "/");
            string systemPath = Path.GetFullPath(normalizedPath);
            Directory.CreateDirectory(systemPath);
        }
    }
}
