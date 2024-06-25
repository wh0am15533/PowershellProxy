using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using Microsoft.Win32.TaskScheduler;
using System.Threading;

using Microsoft.Win32;

namespace PowershellProxy
{
    internal class Program
    {
        #region[Declarations]

        private static string realPowershellPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
        private static string logPath = $"{Environment.ProcessPath?.Replace("powershell.exe", "proxylog.txt")}";
        //private static string logPath = Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty;


        #endregion

        static void Main(string[] args)
        {
            Console.WriteLine("Proxy Path: " + Environment.ProcessPath);
            Console.WriteLine("Log Path: " + logPath);

            //RegistryKey rootkey = Registry.CurrentUser.OpenSubKey("Software", true);


            try
            {
                LogArgs(args);

                ProcessInfo? parentProcessInfo = GetParentProcessInfo(args);

                // Start the PowerShell process with the captured arguments
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
                    process.WaitForExit();
                }

                if (parentProcessInfo != null) 
                {
                    LogDetails(parentProcessInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                LogError($"An error occurred: {ex.Message}");
            }

            // [DEBUGGING] Exit Proxy
            //Console.ReadLine();
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
                                        //Console.WriteLine("MATCH");
                                        return $"{task.Name} [Sched. Task Folder: {task.Folder}]";
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error checking task {task.Name}: {ex.Message}");
                    }
                }
            }

            return string.Empty;
        }

        

        static void LogArgs(string[] args)
        {
            string logFilePath = logPath;
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine($"Timestamp: {DateTime.Now}");
                writer.WriteLine("Arguments: " + string.Join(" ", args));
            }
        }

        static void LogDetails(ProcessInfo parentProcessInfo)
        {
            string logFilePath = logPath;
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
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


