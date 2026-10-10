[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Inspect Nexus startup on the Windows account running Nexus.' }

function Read-NexusRegistryValue([Microsoft.Win32.RegistryKey]$Root, [string]$Path, [string]$Name) {
    $key = $Root.OpenSubKey($Path)
    if ($null -eq $key) { return $null }
    try {
        if ($key.GetValueNames() -notcontains $Name) { return $null }
        return [pscustomobject]@{ Kind = $key.GetValueKind($Name).ToString(); Value = $key.GetValue($Name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) }
    } finally { $key.Dispose() }
}
$nexusPolicy = 'Software\Microsoft\Windows\CurrentVersion\Policies\System'
$nexusWinlogon = 'Software\Microsoft\Windows NT\CurrentVersion\Winlogon'
$nexusRun = 'Software\Microsoft\Windows\CurrentVersion\Run'
$nexusBackupPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'WhiteDreams\NexusShell\desktop-shell-backup.json'
$nexusBackup = $null
$nexusBackupError = $null
if (Test-Path -LiteralPath $nexusBackupPath) {
    try {
        if ((Get-Item -LiteralPath $nexusBackupPath).Length -gt 65536) { throw 'Recovery record exceeds 64 KiB.' }
        $nexusBackup = Get-Content -LiteralPath $nexusBackupPath -Raw | ConvertFrom-Json
        if ($nexusBackup.Format -ne 1) { throw 'Unknown recovery format.' }
    } catch { $nexusBackupError = $_.Exception.Message }
}
[pscustomobject]@{
    ReadOnly = $true
    UserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    UserPolicyShell = Read-NexusRegistryValue ([Microsoft.Win32.Registry]::CurrentUser) $nexusPolicy 'Shell'
    UserWinlogonShell = Read-NexusRegistryValue ([Microsoft.Win32.Registry]::CurrentUser) $nexusWinlogon 'Shell'
    MachinePolicyShell = Read-NexusRegistryValue ([Microsoft.Win32.Registry]::LocalMachine) $nexusPolicy 'Shell'
    MachineWinlogonShell = Read-NexusRegistryValue ([Microsoft.Win32.Registry]::LocalMachine) $nexusWinlogon 'Shell'
    NexusRunEntry = Read-NexusRegistryValue ([Microsoft.Win32.Registry]::CurrentUser) $nexusRun 'WhiteDreamsNexusShell'
    RecoveryPath = $nexusBackupPath
    RecoveryRecord = $nexusBackup
    RecoveryError = $nexusBackupError
} | ConvertTo-Json -Depth 8
