$ErrorActionPreference = 'Stop'
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -Path $key -Name 'WhiteDreamsNexusShell' -ErrorAction SilentlyContinue
Write-Host 'Nexus login startup is disabled for this user.'
