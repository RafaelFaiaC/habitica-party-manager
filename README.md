# Habitica Party Manager

A .NET 10 Worker Service that automates management of a [Habitica](https://habitica.com) party:

- **Auto-invite** users looking for a party (filtered by minimum level and language) while the party has open slots.
- **Auto-remove** members who have been inactive for a configurable number of days.
- **Auto-start quests**: once enough party members have confirmed a pending quest invitation, force-start it instead of waiting for everyone to respond.

It runs as two independent background services with different polling rhythms — one checking for invite candidates every ~30 seconds, the other running maintenance once a day at midnight.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) — only needed on the machine used to build/publish the app, not on the machine that runs it (see [Deploying as a Windows Service](#deploying-as-a-windows-service) for a self-contained publish that needs no .NET install at all on the target machine)
- Windows (the project targets `net10.0-windows` and uses Windows Service hosting)
- A Habitica account that is the **leader** of the party you want to manage (quest force-start and member removal require leader permissions), plus its API credentials (find them at [habitica.com/user/settings/api](https://habitica.com/user/settings/api))

## Project structure

```
HabiticaPartyManager/
├── Program.cs                     # Host setup, DI, HttpClient configuration
├── InvitePollingService.cs        # BackgroundService: invite polling (~30s)
├── PartyMaintenanceService.cs     # BackgroundService: daily maintenance (midnight)
├── Options/
│   └── HabiticaOptions.cs         # Strongly-typed configuration
├── Habitica/
│   ├── HabiticaClient.cs          # Typed HTTP client for the Habitica API
│   └── Models/                    # Request/response DTOs
└── appsettings.json                # Configuration (all environments)
deploy/
├── install-service.ps1            # Registers the published app as a Windows Service
└── uninstall-service.ps1          # Removes the Windows Service
```

## Configuration

All settings live under the `Habitica` section of `appsettings.json`:

| Key | Default | Description |
|---|---|---|
| `UserId` | *(required, secret)* | Your Habitica User ID |
| `ApiToken` | *(required, secret)* | Your Habitica API Token |
| `MinLevel` | `1` | Minimum character level for invite candidates |
| `Language` | `""` (empty) | Habitica locale code to filter candidates by (e.g. `pt_BR`, `en`). **Note the underscore format** — Habitica uses `pt_BR`, not `pt-BR` or `pt-br`. Leave empty to skip the language filter entirely |
| `MaxPartySize` | `30` | Party capacity; invites stop once this is reached (Habitica counts pending invites toward this total) |
| `InactivityDays` | `7` | Members inactive for this many days or more are removed |
| `MinQuestConfirmations` | `15` | Once this many members confirm a pending quest, it's force-started |
| `InviteCheckIntervalSeconds` | `30` | How often to poll for invite candidates. Habitica's API usage guidelines ask that background scripts not poll faster than every 30 seconds — don't lower this |

`UserId` and `ApiToken` are never stored in `appsettings.json` — see below for how to configure them per environment.

## Running locally

1. Clone the repo and restore/build:

   ```powershell
   dotnet build
   ```

2. Set your Habitica credentials as User Secrets (these are stored outside the repo, per-user, and are only loaded when `DOTNET_ENVIRONMENT=Development`):

   ```powershell
   cd HabiticaPartyManager
   dotnet user-secrets set "Habitica:UserId" "<your-user-id>"
   dotnet user-secrets set "Habitica:ApiToken" "<your-api-token>"
   ```

3. Run it:

   ```powershell
   dotnet run
   ```

   The included `launchSettings.json` sets `DOTNET_ENVIRONMENT=Development` automatically, so User Secrets load and you'll see console logs for both services starting up. If you invoke the binary directly (e.g. `dotnet run --no-launch-profile`), set `DOTNET_ENVIRONMENT=Development` yourself or the app runs in `Production` mode and your secrets won't be picked up (you'll get `401 Unauthorized` from the Habitica API).

## Deploying as a Windows Service

1. Publish a release build. `install-service.ps1` defaults to a `publish/` folder at the repo root, so that's the simplest target:

   ```powershell
   dotnet publish HabiticaPartyManager/HabiticaPartyManager.csproj -c Release -o publish
   ```

   This produces a *framework-dependent* build: the target machine needs the [.NET 10 Runtime](https://dotnet.microsoft.com/download) installed, but nothing else (no SDK, no Visual Studio/VS Code).

   To hand the app to a machine that has **no .NET installed at all** — e.g. a non-technical user's PC — publish *self-contained* instead. This bundles the runtime into a single `.exe` (~75 MB) so the target machine needs nothing beyond Windows itself:

   ```powershell
   dotnet publish HabiticaPartyManager/HabiticaPartyManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
   ```

   Either way, the output lands in `publish/` and `install-service.ps1` works unchanged — it just looks for `HabiticaPartyManager.exe`.

2. From an **elevated** PowerShell (Run as Administrator — no other tooling required, PowerShell ships with Windows), install the service. Credentials are passed as parameters and stored as a service-scoped environment variable in the registry — never written to a file:

   ```powershell
   cd deploy
   .\install-service.ps1 -HabiticaUserId "<your-user-id>" -HabiticaApiToken "<your-api-token>"
   ```

   (Pass `-PublishPath "<some other folder>"` if you published somewhere other than the default `publish/`.)

3. Check it's running:

   ```powershell
   Get-Service HabiticaPartyManager
   ```

   Logs are written to a rolling text file at `<PublishPath>\logs\log-YYYYMMDD.txt` (one file per day, 14 days retained) since a service has no console.

4. To remove it later:

   ```powershell
   cd deploy
   .\uninstall-service.ps1
   ```

After any code change, re-run `dotnet publish` to the same folder and restart the service (`Restart-Service HabiticaPartyManager`) to pick up the new build.

## Known limitations

- **Quest invitation itself is manual.** The bot force-starts a quest that already has a pending invitation and enough confirmations — it does not create the quest invitation.
- **Cancelling stale pending invites (24h+) is not implemented.** `groups/party/members` was confirmed (via live testing) to only return accepted members, not pending invitees, so a different endpoint would be needed to track invite age. This was deliberately left out of scope.
- **In-memory invite dedup.** `InvitePollingService` tracks already-invited candidates in memory to avoid repeat invite attempts within the same run; this resets on restart, which just means a few possible duplicate-invite API calls (Habitica rejects them harmlessly) rather than a real bug.
