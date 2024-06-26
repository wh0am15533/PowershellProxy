using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace PowershellProxy
{
    internal static class Utils
    {
        /// <summary>
        /// Doesn't return right entry since we modify the Powershell.exe path, it returns the proxy app. Use GetPowerShellPathUsingPSHome()
        /// </summary>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public static string GetPowerShellPathUsingWMI()
        {
            string query = "SELECT ExecutablePath FROM Win32_Process WHERE Name = 'powershell.exe' OR Name = 'pwsh.exe'";
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    string path = mo["ExecutablePath"]?.ToString();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            throw new FileNotFoundException("PowerShell executable not found using WMI.");
        }

        public static string GetPowerShellPathUsingPSHome()
        {
            // Use a PowerShell command to get the $PSHome variable
            string command = "%SystemRoot%\\system32\\WindowsPowerShell\\v1.0\\powershell -NoProfile -Command \"Write-Output $PSHome\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {command}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string psHome = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(psHome))
                {
                    string pwshPath = Path.Combine(psHome, "pwsh.exe");
                    string powershellPath = Path.Combine(psHome, "powershell.exe");

                    if (File.Exists(pwshPath))
                    {
                        return pwshPath;
                    }
                    else if (File.Exists(powershellPath))
                    {
                        return powershellPath;
                    }
                }
            }

            return string.Empty;
        }

        public static void PrintTasksByActionPathKeyword(string keyword = "*")
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(new string('-', 50));
            sb.AppendLine($"[All Scheduled Tasks With Application Path Containing: {keyword}]");
            sb.AppendLine(new string('-', 50));
            sb.AppendLine("");

            using (TaskService ts = new TaskService())
            {
                foreach (var task in ts.AllTasks)
                {
                    try
                    {
                        foreach (var action in task.Definition.Actions.OfType<ExecAction>())
                        {
                            if (action.Path.Contains(keyword, StringComparison.OrdinalIgnoreCase) || keyword == "*")
                            {
                                //Console.WriteLine($"TASK: {task.Name} [{task.Folder}] - {action.Arguments}");
                                sb.AppendLine($"Task Name: {task.Name}");
                                sb.AppendLine($"   Task Scheduler Folder: [{task.Folder}]");
                                sb.AppendLine($"   App: {action.Path}");
                                sb.AppendLine($"   Args: {action.Arguments}");
                                sb.AppendLine(new string('-', 50));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine($"Error Checking Task: {task.Name} [Error: {ex.Message}]");
                        sb.AppendLine($"Error Checking Task: {task.Name} [Error: {ex.Message}]");
                    }
                }
            }

            string outpath = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
            if (!string.IsNullOrEmpty(outpath) )
            {
                File.WriteAllText(outpath + "\\ScheduledTasks.txt", sb.ToString());
                var pi = new ProcessStartInfo(outpath + "\\ScheduledTasks.txt") { UseShellExecute = true };
                Process.Start(pi);
            }
        }




    }
}
