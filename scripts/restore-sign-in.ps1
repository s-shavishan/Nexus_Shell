$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw 'Run this from the full Nexus folder on Windows.' }
$hostPath = Join-Path $PSScriptRoot 'Nexus.DesktopHost.exe'
if (-not (Test-Path $hostPath -PathType Leaf)) { throw 'Nexus.DesktopHost.exe is missing. Keep the full release folder together.' }
$userSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
# Elevate only this policy action. The host verifies the originating account
# before touching HKCU; supplying another administrator cannot target its hive.
$helper = Start-Process -FilePath $hostPath -Verb RunAs -ArgumentList @('--policy-action','restore','--user-sid',$userSid) -PassThru
$helper.WaitForExit()
if ($helper.ExitCode -ne 0) { throw 'Sign-in restoration did not finish. The recovery record is retained.' }
Write-Host 'The sign-in recovery action completed. Your current desktop session is unchanged.'
