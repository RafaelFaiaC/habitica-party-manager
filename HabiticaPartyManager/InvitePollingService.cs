using HabiticaPartyManager.Habitica;
using HabiticaPartyManager.Options;
using Microsoft.Extensions.Options;

namespace HabiticaPartyManager;

public class InvitePollingService : BackgroundService
{
    private readonly ILogger<InvitePollingService> _logger;
    private readonly IOptionsMonitor<HabiticaOptions> _options;
    private readonly HabiticaClient _habiticaClient;

    // In-memory only — resets on restart. A persistent store (file/db) would
    // survive restarts but isn't needed yet: a restart just means a few
    // possible duplicate-invite-attempt API calls, which fail gracefully.
    private readonly HashSet<string> _alreadyInvited = [];

    public InvitePollingService(
        ILogger<InvitePollingService> logger,
        IOptionsMonitor<HabiticaOptions> options,
        HabiticaClient habiticaClient)
    {
        _logger = logger;
        _options = options;
        _habiticaClient = habiticaClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.CurrentValue.InviteCheckIntervalSeconds);
        using var timer = new PeriodicTimer(interval);

        _logger.LogInformation(
            "Invite polling started. Checking every {Interval} second(s).",
            _options.CurrentValue.InviteCheckIntervalSeconds);

        do
        {
            try
            {
                await RunInviteCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running invite cycle.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunInviteCycleAsync(CancellationToken stoppingToken)
    {
        var party = await _habiticaClient.GetPartyAsync(stoppingToken);
        if (party is null)
        {
            _logger.LogWarning("Could not retrieve party data. Skipping invite cycle.");
            return;
        }

        var options = _options.CurrentValue;
        var slotsAvailable = options.MaxPartySize - party.MemberCount;

        if (slotsAvailable <= 0)
        {
            _logger.LogInformation("Party is full ({MemberCount}/{MaxPartySize}). No invites this cycle.",
                party.MemberCount, options.MaxPartySize);
            return;
        }

        var candidates = await _habiticaClient.GetUsersLookingForPartyAsync(stoppingToken);

        var eligible = candidates
            .Where(c => string.IsNullOrEmpty(options.Language) || c.Preferences.Language == options.Language)
            .Where(c => c.Stats.Lvl >= options.MinLevel)
            .Where(c => !_alreadyInvited.Contains(c.Id))
            .Take(slotsAvailable)
            .ToList();

        if (eligible.Count == 0)
        {
            _logger.LogInformation("No eligible candidates found this cycle ({SlotsAvailable} slot(s) available).", slotsAvailable);
            return;
        }

        var ids = eligible.Select(c => c.Id).ToList();
        var success = await _habiticaClient.InviteUsersAsync(party.Id, ids, stoppingToken);

        if (success)
        {
            foreach (var candidate in eligible)
            {
                _alreadyInvited.Add(candidate.Id);
                _logger.LogInformation(
                    "Invited {Name} ({Username}) — level {Level}.",
                    candidate.Profile.Name, candidate.Auth.Local.Username, candidate.Stats.Lvl);
            }
        }
    }
}
