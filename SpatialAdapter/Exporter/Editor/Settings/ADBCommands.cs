using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Android;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    public static class ADBCommands
    {
        private static Lazy<string> adbPath = new Lazy<string>(() => GetADBPath());

        private static string GetADBPath()
        {
            string sdkPath = AndroidExternalToolsSettings.sdkRootPath;

            if (string.IsNullOrEmpty(sdkPath))
            {
                UnityEngine.Debug.LogError("Android SDK path is not set in Unity Preferences.");
                return null;
            }

            // Construct the path to the ADB executable
            string path = Path.Combine(sdkPath, "platform-tools", Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb");

            if (File.Exists(path))
            {
                return path;
            }
            else
            {
                UnityEngine.Debug.LogError("ADB executable not found at the expected location: " + adbPath);
                return null;
            }
        }

        public static bool TryGetDevices(out string[] deviceSerials)
        {
            if (string.IsNullOrEmpty(adbPath.Value))
            {
                UnityEngine.Debug.LogError("ADB path not found.");
                deviceSerials = null;
                return false;
            }

            try
            {
                // Execute "adb devices" command to check for connected devices
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath.Value,
                        Arguments = "devices",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                // Read the output to find the list of devices
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                // If there's an error or no devices are listed, return false
                if (!string.IsNullOrEmpty(error))
                {
                    UnityEngine.Debug.LogError("Error checking ADB devices: " + error);
                    deviceSerials = null;
                    return false;
                }

                // Remove the first line from the output
                string prefix = "List of devices attached\n";
                output = output.Substring(prefix.Length);

                string[] words = Regex.Split(output, @"\s+");

                // Check if the output contains the list of devices
                deviceSerials = words.Where(
                    word => word != "device" && !string.IsNullOrEmpty(word)
                ).ToArray();
                return deviceSerials.Length > 0;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("Exception while checking ADB devices: " + ex.Message);
                deviceSerials = null;
                return false;
            }
        }

        public static bool ForwardPort(int localPort, int remotePort, string deviceSerial = "")
        {
            try
            {
                // Build the ADB command
                string adbCommand = deviceSerial == "" 
                    ? $"forward tcp:{localPort} tcp:{remotePort}" 
                    : $"-s {deviceSerial} forward tcp:{localPort} tcp:{remotePort}";

                // Start the process
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath.Value,
                        Arguments = adbCommand,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogError($"ADB Port Forwarding Failed: {error}");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Exception during ADB Port Forwarding: {ex.Message}");
                return false;
            }
        }

        public static bool ResetPortForwarding(string deviceSerial = "")
        {
            try
            {
                // Build the ADB command
                string adbCommand = deviceSerial == "" 
                    ? $"forward --remove-all" 
                    : $"-s {deviceSerial} forward --remove-all";

                // Start the process
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath.Value,
                        Arguments = adbCommand,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogError($"Failed to reset port forward for {deviceSerial}: {error}");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Exception during ADB Port Forwarding: {ex.Message}");
                return false;
            }
        }
        
        public static bool RemovePortForward(int localPort, string deviceSerial = "")
        {
            try
            {
                string adbCommand = deviceSerial == ""
                    ? $"forward --remove tcp:{localPort}"
                    : $"-s {deviceSerial} forward --remove tcp:{localPort}";
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath.Value,
                        Arguments = adbCommand,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogError($"Failed to remove port forward tcp:{localPort} for {deviceSerial}: {error}");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Exception during ADB Port Forward removal: {ex.Message}");
                return false;
            }
        }
        
        public static bool IsServerRunningOnDevice(string deviceSerial = "")
        {
            try
            {
                string package = "com.spatialadapter.server";
                string adbCmd = deviceSerial == ""
                    ? "shell ps"
                    : $"-s {deviceSerial} shell ps";
                
                var psi = new ProcessStartInfo
                {
                    FileName = adbPath.Value,
                    Arguments = adbCmd,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                var matched = output
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(line => line.Contains(package, StringComparison.Ordinal));
                
                return matched;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Exception server check: {ex.Message}");
                return false;
            }
        }
        
        public static bool ListPortForwarding(out string[] entries, string deviceSerial = "")
        {
            if (string.IsNullOrEmpty(adbPath.Value))
            {
                UnityEngine.Debug.LogError("ADB path not found.");
                entries = null;
                return false;
            }
            try
            {
                string adbCommand = deviceSerial == ""
                    ? $"forward --list"
                    : $"-s {deviceSerial} forward --list";
                
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath.Value,
                        Arguments = adbCommand,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    entries = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogError($"Failed to list port forwarding for {deviceSerial}: {error}");
                    entries = Array.Empty<string>();
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Exception during ADB Port Forward list: {ex.Message}");
                entries = null;
                return false;
            }
        }
        
        public static bool KillServer()
        {
            if (string.IsNullOrEmpty(adbPath.Value))
            {
                UnityEngine.Debug.LogError("ADB path not found.");
                return false;
            }
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = adbPath.Value,
                        Arguments = "kill-server",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogError($"Failed to kill ADB server: {error}");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Exception during ADB kill-server: {ex.Message}");
                return false;
            }
        }
    }
}
