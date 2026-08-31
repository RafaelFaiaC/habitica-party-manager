using System.Text.Json.Serialization;

namespace HabiticaPartyManager.Habitica.Models;

public class PartyDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int MemberCount { get; set; }

    public QuestDto Quest { get; set; } = new();
}

public class QuestDto
{
    public string Key { get; set; } = string.Empty;

    public bool Active { get; set; }

    public Dictionary<string, bool?> Members { get; set; } = new();
}