using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace ByteDance.PICO.IconConfigurator.Editor.Tests
{
    public class AndroidManifestExportAdapterTests
    {
        private const string k_TestManifestPath = "Assets/IconConfigurator/ManifestTests/AndroidManifest.xml";
        private const string k_TestLauncherManifestPath = "Assets/IconConfigurator/ManifestTests/launcherManifest.xml";
        private static readonly XNamespace s_androidNamespace = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace s_toolsNamespace = "http://schemas.android.com/tools";

        [SetUp]
        public void SetUp()
        {
            DeleteTestManifestDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            DeleteTestManifestDirectory();
        }

        [Test]
        public void Apply_WhenManifestMissing_CreatesMainAndLauncherManifestsWithoutIconConflict()
        {
            AndroidManifestExportAdapter adapter = new AndroidManifestExportAdapter(
                k_TestManifestPath,
                k_TestLauncherManifestPath,
                false);

            adapter.Apply(new IconApplyPayload());

            XElement mainApplication = XDocument.Load(k_TestManifestPath).Root.Element("application");
            Assert.That(mainApplication, Is.Not.Null);
            Assert.That(mainApplication.Attribute(s_androidNamespace + "icon"), Is.Null);

            XElement launcherApplication = XDocument.Load(k_TestLauncherManifestPath).Root.Element("application");
            Assert.That(launcherApplication.Attribute(s_androidNamespace + "icon")?.Value, Is.EqualTo("@mipmap/ic_spatial_launcher"));
            Assert.That(launcherApplication.Attribute(s_androidNamespace + "label")?.Value, Is.EqualTo("@string/icon_configurator_app_name"));
            Assert.That(launcherApplication.Attribute(s_androidNamespace + "localeConfig")?.Value, Is.EqualTo("@xml/locales_config"));
            Assert.That(launcherApplication.Attribute(s_toolsNamespace + "replace")?.Value, Is.EqualTo("android:icon,android:label"));
            AssertMetadata(launcherApplication, "icon.3d.list", "@array/icon_3d_list");
            AssertMetadata(launcherApplication, "icon.sdf.list", "@array/icon_sdf_list");
        }

        [Test]
        public void Apply_WhenCustomLauncherManifestExists_PreservesCustomNodesAndAvoidsDuplicateMetadata()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(k_TestManifestPath));
            File.WriteAllText(
                k_TestLauncherManifestPath,
@"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"">
    <uses-permission android:name=""android.permission.INTERNET"" />
    <application android:theme=""@style/CustomTheme"">
        <activity android:name=""com.example.CustomActivity"" />
        <meta-data android:name=""icon.3d.list"" android:resource=""@array/old_icon_list"" />
    </application>
</manifest>");

            AndroidManifestExportAdapter adapter = new AndroidManifestExportAdapter(
                k_TestManifestPath,
                k_TestLauncherManifestPath,
                false);

            adapter.Apply(new IconApplyPayload());

            XDocument document = XDocument.Load(k_TestLauncherManifestPath);
            XElement application = document.Root.Element("application");
            Assert.That(document.Root.Elements("uses-permission").Any(), Is.True);
            Assert.That(application.Elements("activity").Any(), Is.True);
            Assert.That(application.Attribute(s_androidNamespace + "theme")?.Value, Is.EqualTo("@style/CustomTheme"));
            Assert.That(application.Attribute(s_androidNamespace + "icon")?.Value, Is.EqualTo("@mipmap/ic_spatial_launcher"));
            Assert.That(application.Attribute(s_androidNamespace + "label")?.Value, Is.EqualTo("@string/icon_configurator_app_name"));
            Assert.That(application.Attribute(s_toolsNamespace + "replace")?.Value, Is.EqualTo("android:icon,android:label"));
            Assert.That(application.Elements("meta-data")
                .Count(element => element.Attribute(s_androidNamespace + "name")?.Value == "icon.3d.list"), Is.EqualTo(1));
            AssertMetadata(application, "icon.3d.list", "@array/icon_3d_list");
            AssertMetadata(application, "icon.sdf.list", "@array/icon_sdf_list");
        }

        private static void AssertMetadata(XElement application, string name, string resource)
        {
            XElement metadata = application.Elements("meta-data")
                .FirstOrDefault(element => element.Attribute(s_androidNamespace + "name")?.Value == name);
            Assert.That(metadata, Is.Not.Null);
            Assert.That(metadata.Attribute(s_androidNamespace + "resource")?.Value, Is.EqualTo(resource));
        }

        private static void DeleteTestManifestDirectory()
        {
            string directory = Path.GetDirectoryName(k_TestManifestPath);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
