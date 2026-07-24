using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;
using ZipFile = System.IO.Compression.ZipFile;


namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    public enum BundleExportFormat
    {
        usda,
        usdc,
        usdz,
        usd,
        gltf,
        glb
    }

    //
    public static class BundleToolExportManager
    {
        private static readonly string k_toolsRootDirectory = Path.GetFullPath("../../Tools~/", GetCurrentFileDir());
        private static readonly string k_bundleToolRootDirectory =  Path.Combine(k_toolsRootDirectory, "SpatialBundle/");
#if UNITY_EDITOR_WIN
        private static readonly string k_bundleToolPlatformDir =  Path.Combine(k_bundleToolRootDirectory, "win/");
        private static readonly string k_bundleToolExe = Path.Combine(k_bundleToolPlatformDir, "spatialbundle.exe"); //
#elif UNITY_EDITOR_OSX
        private static readonly string k_bundleToolPlatformDir =  Path.Combine(k_bundleToolRootDirectory, "osx/");
        private static readonly string k_bundleToolExe = Path.Combine(k_bundleToolPlatformDir, "spatialbundle");
        private static readonly string k_matcExe = Path.Combine(k_bundleToolPlatformDir, "prebuilt/darwin/arm64/matc");
#endif
        private static readonly string k_bundleToolZipFilename = "SpatialBundle.zip"; 
        
        private static readonly string k_bundleToolCommand = "pack";
        private static readonly string k_bundleToolInputDirFlag = "-s";
        private static readonly string k_bundleToolOutputBundleFileFlag = "-o";
        private static readonly string k_bundleToolExportFormatsFlag = "--formats";
        private static readonly string k_bundleToolOverrideFlag = "--override-output";
        private static readonly string k_bundleToolRecursiveFlag = "--recursive";
        private static readonly string k_bundleToolExportTexturesFlag = "--texture-dir";
        
        public static bool ExportSpatialBundle(string inDir, string outFile, IEnumerable<BundleExportFormat> exportFormats)
        {
            if (!ValidateBundleTool())
            {
                Debug.LogError("Spatial Bundle executable failed smoke test, please check Bundle tool directories.");
                return false;
            }

            string fileExportRoot = AssetExportHelpers.GetExportDirectory();
            
            string exportFormatString = String.Join(" ", exportFormats.Select(x => x.ToString()));

            string exportFormatArg = $"{k_bundleToolExportFormatsFlag} {exportFormatString}";
            string inputDirArg = $"{k_bundleToolInputDirFlag} \"{inDir}\"";
            string outputFileArg = $"{k_bundleToolOutputBundleFileFlag} \"{outFile}\".bundle";
            string textureExportArg = $"{k_bundleToolExportTexturesFlag} \"{fileExportRoot}\"";
            
            Process bundleProcess = new Process();
            bundleProcess.StartInfo.FileName = k_bundleToolExe;
            bundleProcess.StartInfo.Arguments = String.Join(" ", 
                k_bundleToolCommand, 
                inputDirArg, 
                outputFileArg, 
                exportFormatArg,
                k_bundleToolOverrideFlag,
                k_bundleToolRecursiveFlag,
                textureExportArg);
            
            bundleProcess.StartInfo.WorkingDirectory = k_bundleToolPlatformDir;

            bundleProcess.StartInfo.RedirectStandardOutput = true;
            bundleProcess.StartInfo.RedirectStandardError = true;         
            bundleProcess.StartInfo.UseShellExecute = false;
            
            Debug.Log("Beginning SpatialBundle Export...");

            bundleProcess.Start();
            
            string stdOut = bundleProcess.StandardOutput.ReadToEnd();
            string stdError = bundleProcess.StandardError.ReadToEnd();
            
            bundleProcess.WaitForExit();

            if (stdOut.Length > 0) Debug.Log($"SpatialBundle Output: {stdOut}");
            if (stdError.Length > 0) Debug.LogError($"SpatialBundle Error: {stdError}");
            
            Debug.Log($"SpatialBundle exited with code {bundleProcess.ExitCode}");
            
            return (bundleProcess.ExitCode == 0);
        }
        
        //  Extracts the Bundle Tool from the distributed .zip if it hasn't already
        //  Does smoke test to ensure the tool is in one piece
        //
        private static bool ValidateBundleTool()
        {

            if (!Directory.Exists(k_bundleToolRootDirectory))
            {
                Debug.LogWarning("Spatial Bundle Tool not installed, extracting!");
                //  Create requisite home directories for the executables
                Directory.CreateDirectory(k_bundleToolRootDirectory);
                string zipFile  = Path.Combine(k_toolsRootDirectory, k_bundleToolZipFilename);
                ZipFile.ExtractToDirectory(zipFile, k_bundleToolRootDirectory);

#if UNITY_EDITOR_OSX
                //  Delete any strange artifacts that MacOS creates
                string macosxArtifactsDir = Path.Combine(k_bundleToolRootDirectory, "__MACOSX");
                if (Directory.Exists(macosxArtifactsDir))
                {
                    Directory.Delete(macosxArtifactsDir, true);
                }
#endif
            }

#if UNITY_EDITOR_OSX
            EnsureExecutable(k_bundleToolExe);
            EnsureExecutable(k_matcExe);
#endif
            
            //  Ensure the integrity of the tool and libraries by running the bundle tool help command and checking for a 0 exit code.
            //
            Process bundleProcess = new Process();
            bundleProcess.StartInfo.FileName = k_bundleToolExe;
            bundleProcess.StartInfo.Arguments = "--help";
            bundleProcess.StartInfo.UseShellExecute = false;
            bundleProcess.StartInfo.WorkingDirectory = k_bundleToolPlatformDir;
            
            bundleProcess.StartInfo.RedirectStandardOutput = true;
            bundleProcess.StartInfo.RedirectStandardError = true;
            
            bundleProcess.Start();
            bundleProcess.WaitForExit();
            Debug.Log("Spatial Bundle Tool smoke test complete with exit code " + bundleProcess.ExitCode);

            return (bundleProcess.ExitCode == 0);
        }

#if UNITY_EDITOR_OSX

        //  Set requisite executable permissions for spatialbundle
        //  TODO: this function can be removed after bundleTool exe signing
        private static void EnsureExecutable(string executablePath)
        {
            if (!File.Exists(executablePath))
            {
                Debug.LogError($"Required executable missing: {executablePath}");
                return;
            }

            Process chmodProcess = new Process();
            chmodProcess.StartInfo.FileName = "/bin/chmod";
            chmodProcess.StartInfo.Arguments = $"+x \"{executablePath}\"";
            chmodProcess.StartInfo.UseShellExecute = false;
            chmodProcess.StartInfo.RedirectStandardOutput = true;
            chmodProcess.StartInfo.RedirectStandardError = true;

            chmodProcess.Start();
            string stdOut = chmodProcess.StandardOutput.ReadToEnd();
            string stdError = chmodProcess.StandardError.ReadToEnd();
            chmodProcess.WaitForExit();

            if (chmodProcess.ExitCode != 0)
            {
                Debug.LogError($"Failed to mark executable as runnable (chmod +x). ExitCode={chmodProcess.ExitCode} Path={executablePath} Error={stdError} Output={stdOut}");
            }
        }
#endif

        private static string GetCurrentFileDir([CallerFilePath] string filePath = "")
        {
            return Path.GetDirectoryName(filePath);
        }

    }
}
