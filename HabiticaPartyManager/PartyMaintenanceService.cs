using HabiticaPartyManager.Habitica;
using HabiticaPartyManager.Habitica.Models;
using HabiticaPartyManager.Options;
using Microsoft.Extensions.Options;

namespace HabiticaPartyManager;

public class PartyMaintenanceService : BackgroundService
{
    private readonly ILogger<PartyMaintenanceService> _logger;
    private readonly IOptionsMonitor<HabiticaOptions> _options;
    private readonly HabiticaClient _habiticaClient;

    public PartyMaintenanceService(
        ILogger<PartyMaintenanceService> logger,
        IOptionsMonitor<HabiticaOptions> options,
        HabiticaClient habiticaClient)
    {
        _logger = logger;
        _options = options;
        _habiticaClient = habiticaClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // PeriodicTimer fires at a fixed interval from when it started — it has no
            // concept of wall-clock alignment. To align to midnight instead, we compute
            // the delay remaining until the next midnight and await that directly; each
            // loop iteration recomputes the delay, which naturally lands ~24h later.
            var delay = GetDelayUntilNextMidnight();
            _logger.LogInformation("Next maintenance cycle scheduled in {Delay}.", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            try
            {
                await RunMaintenanceCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running maintenance cycle.");
            }
        }
    }

    private static TimeSpan GetDelayUntilNextMidnight()
    {
        var now = DateTimeOffset.Now;
        var nextMidnight = now.Date.AddDays(1);
        return nextMidnight - now;
    }

    private async Task RunMaintenanceCycleAsync(CancellationToken stoppingToken)
    {
        var party = await _habiticaClient.GetPartyAsync(stoppingToken);
        if (party is null)
        {
            _logger.LogWarning("Could not retrieve party data. Skipping maintenance cycle.");
            return;
        }

        await RemoveInactiveMembersAsync(party.Id, stoppingToken);
        await TryForceStartQuestAsync(party, stoppingToken);

        // TODO: cancel invites pending 24h+ — endpoint unconfirmed, needs manual
        // verification via Postman first.
    }

    private async Task TryForceStartQuestAsync(PartyDto party, CancellationToken stoppingToken)
    {
        var quest = party.Quest;
        if (string.IsNullOrEmpty(quest.Key))
        {
            _logger.LogInformation("No pending quest invitation.");
            return;
        }

        if (quest.Active)
        {
            _logger.LogInformation("Quest '{QuestKey}' is already active.", quest.Key);
            return;
        }

        var confirmedCount = quest.Members.Values.Count(response => response == true);
        var required = _options.CurrentValue.MinQuestConfirmations;

        if (confirmedCount < required)
        {
            _logger.LogInformation(
                "Quest '{QuestKey}' has {Confirmed}/{Required} confirmation(s). Waiting.",
                quest.Key, confirmedCount, required);
            return;
        }

        _logger.LogInformation(
            "Quest '{QuestKey}' has {Confirmed}/{Required} confirmation(s). Force-starting.",
            quest.Key, confirmedCount, required);

        var started = await _habiticaClient.ForceStartQuestAsync(party.Id, stoppingToken);

        if (started)
        {
            _logger.LogInformation("Quest '{QuestKey}' force-started.", quest.Key);
        }
    }

    private async Task RemoveInactiveMembersAsync(string groupId, CancellationToken stoppingToken)
    {
        var options = _options.CurrentValue;
        var members = await _habiticaClient.GetPartyMembersAsync(stoppingToken);
        var cutoff = DateTimeOffset.Now.AddDays(-options.InactivityDays);

        var inactiveMembers = members.Where(m => m.Auth.Timestamps.LoggedIn < cutoff).ToList();

        if (inactiveMembers.Count == 0)
        {
            _logger.LogInformation("No inactive members found.");
            return;
        }

        foreach (var member in inactiveMembers)
        {
            var removed = await _habiticaClient.RemoveMemberAsync(groupId, member.Id, stoppingToken);

            if (removed)
            {
                _logger.LogInformation(
                    "Removed inactive member {Name} ({Username}) — last login: {LastLogin}.",
                    member.Profile.Name, member.Auth.Local.Username, member.Auth.Timestamps.LoggedIn);
            }
        }
    }
}
