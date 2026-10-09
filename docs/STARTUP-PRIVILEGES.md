# Startup privileges

Task Scheduler can start an administrator's application with its elevated token at logon after a one-time elevated registration. `Highest` uses the selected account's available privileges; it does not make a standard user an administrator. Nexus 1.4.2 keeps its existing startup/session options. This guide records an optional future startup route.

For a visible Nexus desktop, the task must belong to the signed-in user:

| Setting | Nexus requirement |
|---|---|
| Account | The specific user's account, rather than SYSTEM |
| Logon type | Interactive; run only while that user is logged on |
| Trigger | That specific user's logon |
| Privilege | Highest only when elevation is deliberately required; registration needs administrator approval |
| Action for temporary desktop mode | `Nexus.DesktopHost.exe --nexus-session` from a complete build folder |
| Working directory | The complete build folder |
| Battery and duration | Allow battery operation; unlimited execution time |

The relevant correction to the supplied PowerShell example is:

```powershell
# For an administrator's own account; elevated PowerShell setup is required.
$nexusUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $nexusUser
$principal = New-ScheduledTaskPrincipal -UserId $nexusUser -LogonType Interactive -RunLevel Highest
```

A SYSTEM/ServiceAccount task runs in a background security context and does not provide the user's interactive desktop. Avoid combining another logon task with Nexus's existing preview startup or persistent desktop selection, which can launch competing sessions.

The current desktop host starts the UI as a child process. Elevating that host also elevates its UI; a separate privileged helper would be needed to keep the desktop UI at normal privilege. Elevated startup does not establish the cause of drawing lag or guarantee access to a registry key with restrictive permissions. Task registration and this PowerShell fragment have not been executed in the authoring environment.

Microsoft references: [task security contexts](https://learn.microsoft.com/en-us/windows/win32/taskschd/security-contexts-for-running-tasks), [scheduled task principals](https://learn.microsoft.com/en-us/powershell/module/scheduledtasks/new-scheduledtaskprincipal), and [interactive services](https://learn.microsoft.com/en-us/windows/win32/services/interactive-services).
