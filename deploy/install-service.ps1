#Requires -RunAsAdministrator
<#
Creates and starts the HabiticaPartyManager Windows Service, pointed at an
already-published output folder. Habitica credentials are passed as
parameters (never written to a file) and stored as a service-scoped
environment variable in the registry.

Usage:
  .\install-service.ps1 -HabiticaUserId <id> -HabiticaApiToken <token>
#>
param(
    [string]$ServiceName = "HabiticaPartyManager",
    [string]$PublishPath = (Join-Path $PSScriptRoot "..\publish"),
    [Parameter(Mandatory = $true)][string]$HabiticaUserId,
    [Parameter(Mandatory = $true)][string]$HabiticaApiToken
)

$exePath = Join-Path $PublishPath "HabiticaPartyManager.exe"
if (-not (Test-Path $exePath)) {
    throw "Executable not found at $exePath. Run 'dotnet publish' first."
}

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    throw "Service '$ServiceName' already exists. Run uninstall-service.ps1 first if you want to reinstall."
}

New-Service -Name $ServiceName `
    -BinaryPathName $exePath `
    -DisplayName "Habitica Party Manager" `
    -Description "Automates Habitica party invites, inactive-member removal, and quest auto-start." `
    -StartupType Automatic

Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName" -Name Environment -Value ([string[]]@(
    "Habitica__UserId=$HabiticaUserId",
    "Habitica__ApiToken=$HabiticaApiToken",
    "DOTNET_ENVIRONMENT=Production"
))

Start-Service -Name $ServiceName
Get-Service -Name $ServiceName
