# PowershellProxy

A simple Powershell application proxy that logs identifying information on what called Powershell since Windows logging doesn't provide identifying details such as task or service, and parent application. Useful for detecting malware when Powershell is suspect. Works out-of-the-box or modify for personal use. 

## Compiling

Requires .NET 8 Framework installed. Open project in Visual Studio 2022 and build. Move all your build files to whatever path (i.e "C:\PSProxy\"). The executable's name is "Powershell.exe" (Not a typo).

## Install/Uninstall

Open command prompt as Administrator in the folder you placed your build files and use the following commands:

    Install: powershell.exe -psproxyinstall
    Uninstall: powershell.exe -psproxyremove
    
After install any call to Powershell will be intercept and logged in "proxylog.txt" in the folder you install to. Note: there is no blocking, it only logs information valuable in identifying.

## Additional

Since Windows Task Scheduler doesn't have a search method I've added a command to search all tasks for a specific application or keyword (or just print all) whether installed or not, just run one of these commands.

    Print Powershell Tasks: powershell.exe -listtasks
    Print All Tasks: powershell.exe -listtasks *
    By Application Keyword #1: powershell.exe -listtasks explorer
    By Application Keyword #2: powershell.exe -listtasks explorer.exe
    
These commands will find any matches and open a report file.