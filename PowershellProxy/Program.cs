using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.Win32.TaskScheduler;
using System.Threading;
using Microsoft.Win32;
using System.Globalization;

namespace PowershellProxy
{
    internal class Program
    {
        #region[Declarations]

        private static string realPowershellPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\Powershell.exe";
        private static string logPath = $"{Environment.ProcessPath?.Replace("powershell.exe", "proxylog.txt")}";
        


        #endregion

        static void Main(string[] args)
        {
            // Powershell Proxy install/uninstall
            if (args[0].ToLower().Trim() == "-psproxyinstall" || args[0].ToLower().Trim() == "-psproxyremove")
            {
                // Set the environment PATH for powershell
                try
                {
                    string pathToPrepend = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;
                    if (!string.IsNullOrEmpty(pathToPrepend))
                    {
                        // Set the updated User PATH environment variable
                        string? currentUserPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User);
                        string newUserPath = $"{pathToPrepend};{currentUserPath}";                    
                        Environment.SetEnvironmentVariable("PATH", newUserPath, EnvironmentVariableTarget.User);

                        // Set the updated Machine PATH environment variable
                        string? currentMachinePath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                        string newMachinePath = $"{pathToPrepend};{currentMachinePath}";
                        Environment.SetEnvironmentVariable("PATH", newMachinePath, EnvironmentVariableTarget.Machine);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error setting Powershell Path: {e.Message}");
                    Console.WriteLine(" ");
                    Console.WriteLine("[Press any key to exit.]");
                    Console.ReadLine();
                    return;
                }

                // Set the registry path for powershell
                try 
                {
                    RegistryKey? rootkey = Registry.LocalMachine.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\App Paths\\PowerShell.exe", true);
                    if (rootkey != null)
                    {
                        string? val = (rootkey.GetValue("") as string); // (Default)
                        if (val != null)
                        {
#if !DEBUG
                            string proxypath = Environment.ProcessPath ?? string.Empty;
                            if (args[0].ToLower().Trim() == "-psproxyinstall" && val.ToLower() != proxypath.ToLower())
                            {
                                if (string.IsNullOrEmpty(proxypath)) 
                                {
                                    rootkey.SetValue("", realPowershellPath);
                                    Console.WriteLine("Powershell Proxy NOT Installed.");
                                }
                                else
                                {
                                    rootkey.SetValue("", proxypath);
                                    Console.WriteLine("Powershell Proxy Installed.");
                                }
                                
                                rootkey.Close();
                                rootkey.Dispose();                                
                                Console.WriteLine(" ");
                                Console.WriteLine("[Press any key to exit.]");
                                Console.ReadLine();
                                return;
                            }
                            else if (args[0].ToLower().Trim() == "-psproxyremove" && val.ToLower() == proxypath.ToLower())
                            {
                                // Since $PsHome uses another Reg key that's Restricted this should be the real path since we don't mod that key.
                                var existingPsHome = Utils.GetPowerShellPathUsingPSHome();
                                var defaultHome = realPowershellPath;
                                if (!string.IsNullOrEmpty(existingPsHome)) { defaultHome = existingPsHome; }

                                rootkey.SetValue("", defaultHome);
                                rootkey.Close();
                                rootkey.Dispose();
                                Console.WriteLine("Powershell Proxy Uninstalled.");
                                Console.WriteLine(" ");
                                Console.WriteLine("[Press any key to exit.]");
                                Console.ReadLine();
                                return;
                            }
                            else
                            {
                                rootkey.Close();
                                rootkey.Dispose();
                                Console.WriteLine("[Press any key to exit.]");
                                Console.ReadLine();
                                return;
                            }
#endif
                        }
                        else
                        {
                            rootkey.Close();
                            rootkey.Dispose();
                            Console.WriteLine("[Press any key to exit.]");
                            Console.ReadLine();
                            return;
                        }
                    }                    
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error setting Powershell registry: {e.Message}");
                    Console.WriteLine(" ");
                    Console.WriteLine("[Press any key to exit.]");
                    Console.ReadLine();
                    return;
                }

            }
            // List any scheduled tasks who's action path (application to run) contains specified keyword
            else if (args[0].ToLower().Trim() == "-listtasks")
            {
                string keyword = "powershell.exe";
                if (args.Length == 2 && !string.IsNullOrEmpty(args[1].Trim())) { keyword = args[1].ToLower().Trim(); }
                Utils.PrintTasksByActionPathKeyword(keyword);
                return;
            }
            // Start the real powershell.exe with original arguments
            else
            {
                try
                {
                    ProcessInfo? parentProcessInfo = GetParentProcessInfo(args);

                    string arguments = string.Join(" ", args);
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = realPowershellPath,
                        Arguments = arguments,
                        UseShellExecute = true, // UseShellExecute must be true to start PowerShell independently
                        CreateNoWindow = false
                    };

                    using (var process = new Process { StartInfo = startInfo })
                    {
                        process.Start();
                        //process.WaitForExit();
                    }

                    if (parentProcessInfo != null) 
                    {
                        LogDetails(args, parentProcessInfo);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }

            }

        }

        static ProcessInfo? GetParentProcessInfo(string[] args)
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            using (var query = new ManagementObjectSearcher($"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {currentProcessId}"))
            {
                foreach (ManagementObject mo in query.Get())
                {
                    int parentId = Convert.ToInt32(mo["ParentProcessId"]);
                    Process parentProcess = Process.GetProcessById(parentId);

                    string serviceName = GetServiceNameForProcess(parentId);
                    string taskName = string.Empty;

                    // If the parent process is svchost.exe, check for the task
                    if (parentProcess.ProcessName.Equals("svchost", StringComparison.OrdinalIgnoreCase))
                    {
                        taskName = GetScheduledTaskForProcess(args);
                    }

                    return new ProcessInfo
                    {
                        ProcessName = parentProcess.ProcessName,
                        ProcessId = parentId,
                        ExecutablePath = parentProcess.MainModule?.FileName ?? string.Empty,
                        ServiceName = serviceName,
                        ScheduledTaskName = taskName ?? string.Empty
                    };
                }
            }
            return null;
        }

        static string GetServiceNameForProcess(int processId)
        {
            string serviceName = "";
            using (var query = new ManagementObjectSearcher($"SELECT Name FROM Win32_Service WHERE ProcessId = {processId}"))
            {
                foreach (ManagementObject mo in query.Get())
                {
                    serviceName = mo["Name"]?.ToString() ?? string.Empty;
                    break;
                }
            }
            return serviceName;
        }

        static string GetScheduledTaskForProcess(string[] args)
        {
            using (TaskService ts = new TaskService())
            {
                DateTime currentTime = DateTime.Now;

                foreach (var task in ts.AllTasks)
                {
                    try
                    {
                        // Check if the task's last run time is close to the current time
                        if ((currentTime - task.LastRunTime).TotalMinutes <= 5)
                        {
                            foreach (var action in task.Definition.Actions.OfType<ExecAction>())
                            {
                                if (action.Path.Contains("powershell.exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    //Console.WriteLine("Task Name: " + task.Name);
                                    //Console.WriteLine("Proxy Args: " + string.Join(" ", args));
                                    //Console.WriteLine("Action Args: " + action.Arguments.Replace("\"", ""));

                                    // Check if the task arguments match the current arguments
                                    if (args.SequenceEqual(action.Arguments.Replace("\"", "").Split(' ')))
                                    {
                                        return $"{task.Name} [Sched. Task Folder: {task.Folder}]";
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error Checking Task: {task.Name} [Error: {ex.Message}]");
                    }
                }
            }

            return string.Empty;
        }

        static void LogDetails(string[] args, ProcessInfo parentProcessInfo)
        {
            string logFilePath = logPath;
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine($"Timestamp: {DateTime.Now}");
                writer.WriteLine("Arguments: " + string.Join(" ", args));
                writer.WriteLine($"Parent Process: {parentProcessInfo.ProcessName} (ID: {parentProcessInfo.ProcessId}) - {parentProcessInfo.ExecutablePath}");
                if (!string.IsNullOrEmpty(parentProcessInfo.ServiceName))
                {
                    writer.WriteLine($"Service: {parentProcessInfo.ServiceName}");
                }
                if (!string.IsNullOrEmpty(parentProcessInfo.ScheduledTaskName))
                {
                    writer.WriteLine($"Scheduled Task: {parentProcessInfo.ScheduledTaskName}");
                }
                writer.WriteLine(new string('-', 50));
            }
        }

        static void LogError(string errorMsg)
        {
            string logFilePath = logPath;
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine(errorMsg);
                writer.WriteLine(new string('-', 50));
            }
        }
    }

    class ProcessInfo
    {
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string ExecutablePath { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string ScheduledTaskName { get; set; } = string.Empty;
    }
}


