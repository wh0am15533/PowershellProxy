using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowershellProxy
{
    internal static class Utils
    {
        public static void ListPowershellScheduledTasks()
        {
            using (TaskService ts = new TaskService())
            {
                foreach (var task in ts.AllTasks)
                {
                    try
                    {
                        foreach (var action in task.Definition.Actions.OfType<ExecAction>())
                        {
                            if (action.Path.Contains("powershell.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"TASK: {task.Name} [{task.Folder}] - {action.Arguments}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error checking task {task.Name}: {ex.Message}");
                    }
                }
            }
        }





    }
}
