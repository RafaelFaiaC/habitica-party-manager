using System.Net.Http.Json;
using HabiticaPartyManager.Habitica.Models;

namespace HabiticaPartyManager.Habitica;

public class HabiticaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HabiticaClient> _logger;

    public HabiticaClient(HttpClient httpClient, ILogger<HabiticaClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PartyDto?> GetPartyAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient
            .GetFromJsonAsync<HabiticaApiResponse<PartyDto>>("groups/party", cancellationToken);

        if (response is null || !response.Success)
        {
            _logger.LogWarning("Unexpected response when fetching party data.");
            return null;
        }

        return response.Data;
    }

    public async Task<List<HabiticaUserDto>> GetPartyMembersAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient
            .GetFromJsonAsync<HabiticaApiResponse<List<HabiticaUserDto>>>(
                "groups/party/members?includeAllPublicFields=true",
                cancellationToken);

        if (response is null || !response.Success || response.Data is null)
        {
            _logger.LogWarning("Unexpected response when fetching party members.");
            return [];
        }

        return response.Data;
    }

    public async Task<List<HabiticaUserDto>> GetUsersLookingForPartyAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient
            .GetFromJsonAsync<HabiticaApiResponse<List<HabiticaUserDto>>>("looking-for-party", cancellationToken);

        if (response is null || !response.Success || response.Data is null)
        {
            _logger.LogWarning("Unexpected response when fetching users looking for a party.");
            return [];
        }

        return response.Data;
    }

    public async Task<bool> InviteUsersAsync(string groupId, IReadOnlyCollection<string> userIds, CancellationToken cancellationToken)
    {
        var request = new InviteUsersRequest { Uuids = userIds.ToList() };

        var response = await _httpClient.PostAsJsonAsync($"groups/{groupId}/invite", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // Expected/self-correcting (MemberCount lags pending invites); no error code
            // for it, so detected via the Portuguese message text.
            if (body.Contains("máximo", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Could not invite users this cycle: group is already at capacity. Will retry next cycle.");
            }
            else
            {
                _logger.LogWarning(
                    "Failed to invite users. Status: {StatusCode}. Body: {Body}",
                    response.StatusCode, body);
            }

            return false;
        }

        return true;
    }

    public async Task<bool> RemoveMemberAsync(string groupId, string memberId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsync($"groups/{groupId}/removeMember/{memberId}", null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Failed to remove member {MemberId}. Status: {StatusCode}. Body: {Body}",
                memberId, response.StatusCode, body);
            return false;
        }

        return true;
    }

    public async Task<bool> ForceStartQuestAsync(string groupId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsync($"groups/{groupId}/quests/force-start", null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Failed to force-start quest. Status: {StatusCode}. Body: {Body}",
                response.StatusCode, body);
            return false;
        }

        return true;
    }
}