using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Sudoku.Shared.PythonIncluded
{
    public static class MacInstaller
    {

        public static string LibFileName { get; set; } = "libpython3.7.dylib";
        public static string InstallPath { get; set; } = "/Library/Frameworks/Python.framework/Versions"; //Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static string PythonDirectoryName { get; set; } = "3.7";// = (string) null;

        public static InstallationSource Source { get; set; } = new DownloadInstallationSource()
        {
            DownloadUrl = "https://www.python.org/ftp/python/3.7.3/python-3.7.3-embed-amd64.zip"
        };

        public static string InstallPythonHome => !string.IsNullOrWhiteSpace(PythonDirectoryName) ? Path.Combine(InstallPath, PythonDirectoryName) : Path.Combine(InstallPath, Source.GetPythonDistributionName());

        public static event Action<string> LogMessage;

        private static void Log(string message)
        {
            Action<string> logMessage = LogMessage;
            if (logMessage == null)
                return;
            logMessage(message);
        }

        public static async Task SetupPython(bool force = false)
        {
            Environment.SetEnvironmentVariable("PATH", InstallPythonHome + ";" + Environment.GetEnvironmentVariable("PATH"));
            if (!force && Directory.Exists(InstallPythonHome) && File.Exists(Path.Combine(InstallPythonHome, "python.exe")))
                ;
            else
            {
                string zip = await Source.RetrievePythonZip(InstallPath);
                if (string.IsNullOrWhiteSpace(zip))
                    Log("SetupPython: Error obtaining zip file from installation source");
                else
                    await Task.Run(() =>
                    {
                        try
                        {
                            ZipFile.ExtractToDirectory(zip, zip.Replace(".zip", ""));
                            File.Delete(Path.Combine(InstallPythonHome, Source.GetPythonVersion() + "._pth"));
                        }
                        catch (Exception ex)
                        {
                            Log("SetupPython: Error extracting zip file: " + zip);
                        }
                    });
            }
        }

        public static async Task InstallWheel(Assembly assembly, string resource_name, bool force = false)
        {
            string key = GetResourceKey(assembly, resource_name);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("The resource '" + resource_name + "' was not found in assembly '" + assembly.FullName + "'");
            string path2 = resource_name.Split(new char[1]
            {
                '-'
            }).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(path2))
                throw new ArgumentException("The resource name '" + resource_name + "' did not contain a valid module name");
            string lib = GetLibDirectory();
            string path = Path.Combine(lib, path2);
            if (!force && Directory.Exists(path))
            {
                lib = null;
            }
            else
            {
                string wheelPath = Path.Combine(lib, key);
                await Task.Run(() => CopyEmbeddedResourceToFile(assembly, key, wheelPath, force)).ConfigureAwait(false);
                await InstallLocalWheel(wheelPath, lib).ConfigureAwait(false);
                lib = null;
            }
        }

        public static async Task InstallWheel(string wheelPath, bool force = false)
        {
            string nameFromWheelFile = GetModuleNameFromWheelFile(wheelPath);
            string libDirectory = GetLibDirectory();
            string path = Path.Combine(libDirectory, nameFromWheelFile);
            if (!force && Directory.Exists(path))
                return;
            await InstallLocalWheel(wheelPath, libDirectory).ConfigureAwait(false);
        }

        private static string GetModuleNameFromWheelFile(string wheelPath)
        {
            string fileName = Path.GetFileName(wheelPath);
            string str = fileName.Split(new char[1]
            {
                '-'
            }).FirstOrDefault();
            return !string.IsNullOrWhiteSpace(str) ? str : throw new ArgumentException("The file name '" + fileName + "' did not contain a valid module name");
        }

        private static string GetLibDirectory()
        {
            string path = Path.Combine(InstallPythonHome, "Lib");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        private static async Task InstallLocalWheel(string wheelPath, string lib) => await Task.Run(() =>
        {
            try
            {
                using (ZipArchive zipArchive = ZipFile.OpenRead(wheelPath))
                {
                    if (!AreAllFilesAlreadyPresent(zipArchive, lib))
                        zipArchive.ExtractToDirectory(lib);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error extracting zip file: " + wheelPath);
            }
            File.Delete(wheelPath);
            string path = Path.Combine(InstallPythonHome, Source.GetPythonVersion() + "._pth");
            if (!File.Exists(path) || File.ReadAllLines(path).Contains("./Lib"))
                return;
            File.AppendAllLines(path, (IEnumerable<string>)new string[1]
            {
                "./Lib"
            });
        }).ConfigureAwait(false);

        public static void PipInstallWheel(Assembly assembly, string resource_name, bool force = false)
        {
            string resourceKey = GetResourceKey(assembly, resource_name);
            if (string.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException("The resource '" + resource_name + "' was not found in assembly '" + assembly.FullName + "'");
            string path2 = resource_name.Split(new char[1]
            {
                '-'
            }).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(path2))
                throw new ArgumentException("The resource name '" + resource_name + "' did not contain a valid module name");
            string str1 = Path.Combine(InstallPythonHome, "Lib");
            if (!Directory.Exists(str1))
                Directory.CreateDirectory(str1);
            string path = Path.Combine(str1, path2);
            if (!force && Directory.Exists(path))
                return;
            string filePath = Path.Combine(str1, resourceKey);
            string str2 = Path.Combine(InstallPythonHome, "Scripts", "pip3");
            CopyEmbeddedResourceToFile(assembly, resourceKey, filePath, force);
            TryInstallPip();
            string str3 = filePath;
            RunCommand(str2 + " install " + str3);
        }

        private static void CopyEmbeddedResourceToFile(
            Assembly assembly,
            string resourceName,
            string filePath,
            bool force = false)
        {
            if (!force && File.Exists(filePath))
                return;
            string resourceKey = GetResourceKey(assembly, resourceName);
            if (resourceKey == null)
                Log("Error: Resource name '" + resourceName + "' not found in assembly " + assembly.FullName + "!");
            try
            {
                using (Stream manifestResourceStream = assembly.GetManifestResourceStream(resourceKey))
                {
                    using (FileStream destination = new FileStream(filePath, FileMode.Create))
                    {
                        if (manifestResourceStream == null)
                        {
                            Log("CopyEmbeddedResourceToFile: Resource name '" + resourceName + "' not found!");
                            throw new ArgumentException("Resource name '" + resourceName + "' not found!");
                        }
                        manifestResourceStream.CopyTo(destination);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Error: unable to extract embedded resource '" + resourceName + "' from  " + assembly.FullName + ": " + ex.Message);
            }
        }

        public static string GetResourceKey(Assembly assembly, string embedded_file) => assembly.GetManifestResourceNames().FirstOrDefault(x => x.Contains(embedded_file));

        public static void PipInstallModule(string module_name, string version = "", bool force = false)
        {
            TryInstallPip();
            if (IsModuleInstalled(module_name) && !force)
                return;
            // string str1 = Path.Combine(MacInstaller.EmbeddedPythonHome, "Scripts", "pip");
            string str1 = Path.Combine(InstallPythonHome, "bin", "pip3");
            // string str1 = "pip3";
            string str2 = force ? " --force-reinstall" : "";
            if (version.Length > 0)
                version = "==" + version;
            RunCommand($"{str1} --version");
            RunCommand($"{str1} install {module_name}{version} {str2}");
        }

        public static void InstallPip()
        {
            string path = Path.Combine(InstallPythonHome, "Lib");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            RunCommand("cd " + path + " && curl https://bootstrap.pypa.io/get-pip.py -o get-pip.py");
            // MacInstaller.RunCommand("cd " + MacInstaller.EmbeddedPythonHome + " && python.exe Lib\\get-pip.py");
            RunCommand("cd " + InstallPythonHome + " && python get-pip.py");
        }

        public static bool TryInstallPip(bool force = false)
        {
            if (!(!IsPipInstalled() | force))
                return false;
            try
            {
                InstallPip();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception trying to install pip: {ex}");
                return false;
            }
        }

        public static bool IsPythonInstalled() => File.Exists(Path.Combine(InstallPythonHome, "Python"));

        public static bool IsPipInstalled() => File.Exists(Path.Combine(InstallPythonHome, "bin", "pip3"));

        public static bool IsModuleInstalled(string module)
        {
            if (!IsPythonInstalled())
                return false;
            string str = Path.Combine(InstallPythonHome, "Lib", "site-packages", module);
            return Directory.Exists(str) && File.Exists(Path.Combine(str, "__init__.py"));
        }

        public static void RunCommand(string command) => RunCommand(command, CancellationToken.None).Wait();

        public static async Task RunCommand(string command, CancellationToken token)
        {
            Process process = new Process();
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                string str1;
                string str2;
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    str1 = "/bin/bash";
                    str2 = "-c " + $"\"{command}\"";
                }
                else
                {
                    str1 = "cmd.exe";
                    str2 = "/C " + command;
                }
                Log("> " + str1 + " " + str2);
                process.StartInfo = new ProcessStartInfo()
                {
                    FileName = str1,
                    WorkingDirectory = InstallPythonHome,
                    Arguments = str2,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                process.OutputDataReceived += (x, y) => Log(y.Data);
                process.ErrorDataReceived += (x, y) => Log(y.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                token.Register(() =>
                {
                    try
                    {
                        if (process.HasExited)
                            return;
                        process.Kill();
                    }
                    catch (Exception ex)
                    {
                    }
                });
                await Task.Run(() => process.WaitForExit(), token);
                // if (process.ExitCode == 0)
                //   ;
                // else
                Log(" => exit code " + process.ExitCode.ToString());
            }
            catch (Exception ex)
            {
                Log("RunCommand: Error with command: '" + command + "'\r\n" + ex.Message);
            }
            finally
            {
                process?.Dispose();
            }
        }

        private static bool AreAllFilesAlreadyPresent(ZipArchive zip, string lib)
        {
            bool flag = true;
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (!File.Exists(Path.Combine(lib, entry.FullName)))
                {
                    flag = false;
                    break;
                }
            }
            return flag;
        }

        public class DownloadInstallationSource : InstallationSource
        {
            public string DownloadUrl { get; set; }

            public override async Task<string> RetrievePythonZip(string destinationDirectory)
            {
                DownloadInstallationSource installationSource = this;
                string zipFile = Path.Combine(destinationDirectory, installationSource.GetPythonZipFileName());
                if (!installationSource.Force && File.Exists(zipFile))
                    return zipFile;
                await RunCommand("curl " + installationSource.DownloadUrl + " -o " + zipFile, CancellationToken.None);
                return zipFile;
            }

            public override string GetPythonZipFileName() => Path.GetFileName(new Uri(DownloadUrl).LocalPath);

            public static async Task RunCommand(string command, CancellationToken token)
            {
                Process process = new Process();
                try
                {
                    ProcessStartInfo processStartInfo = new ProcessStartInfo();
                    string str1;
                    string str2;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        str1 = "/bin/bash";
                        str2 = "-c " + command;
                    }
                    else
                    {
                        str1 = "cmd.exe";
                        str2 = "/C " + command;
                    }
                    Log("> " + str1 + " " + str2);
                    process.StartInfo = new ProcessStartInfo()
                    {
                        FileName = str1,
                        WorkingDirectory = Directory.GetCurrentDirectory(),
                        Arguments = str2,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    process.OutputDataReceived += (x, y) => Log(y.Data);
                    process.ErrorDataReceived += (x, y) => Log(y.Data);
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    token.Register(() =>
                    {
                        try
                        {
                            if (process.HasExited)
                                return;
                            process.Kill();
                        }
                        catch (Exception ex)
                        {
                        }
                    });
                    await Task.Run(() => process.WaitForExit(), token);
                    if (process.ExitCode == 0)
                        ;
                    else
                        Log(" => exit code " + process.ExitCode.ToString());
                }
                catch (Exception ex)
                {
                    Log("RunCommand: Error with command: '" + command + "'\r\n" + ex.Message);
                }
                finally
                {
                    process?.Dispose();
                }
            }
        }

        public class EmbeddedResourceInstallationSource : InstallationSource
        {
            public Assembly Assembly { get; set; }

            public string ResourceName { get; set; }

            public override async Task<string> RetrievePythonZip(string destinationDirectory)
            {
                EmbeddedResourceInstallationSource installationSource = this;
                string str = Path.Combine(destinationDirectory, installationSource.ResourceName);
                if (!installationSource.Force && File.Exists(str))
                    return str;
                CopyEmbeddedResourceToFile(installationSource.Assembly, installationSource.GetPythonDistributionName(), str);
                return str;
            }

            public override string GetPythonZipFileName() => Path.GetFileName(ResourceName);
        }

        public abstract class InstallationSource
        {
            public abstract Task<string> RetrievePythonZip(string destinationDirectory);

            public bool Force { get; set; }

            public virtual string GetPythonDistributionName()
            {
                string pythonZipFileName = GetPythonZipFileName();
                return pythonZipFileName == null ? null : Path.GetFileNameWithoutExtension(pythonZipFileName);
            }

            public abstract string GetPythonZipFileName();

            public virtual string GetPythonVersion()
            {
                Match match = Regex.Match(GetPythonDistributionName(), "python-(?<major>\\d)\\.(?<minor>\\d+)");
                if (match.Success)
                    return string.Format("python{0}{1}", match.Groups["major"], match.Groups["minor"]);
                Log("Unable to get python version from distribution name.");
                return null;
            }
        }
    }
}