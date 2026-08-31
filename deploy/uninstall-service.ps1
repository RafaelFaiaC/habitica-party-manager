#Requires -RunAsAdministrator
<#
Stops and removes the HabiticaPartyManager Windows Service.
#>
param(
    [string]$ServiceName = "HabiticaPartyManager"
)

if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "Service '$ServiceName' does not exist. Nothing to do."
    return
}

Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
sc.exe delete $ServiceName
