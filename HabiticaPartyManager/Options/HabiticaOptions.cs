namespace HabiticaPartyManager.Options;

public class HabiticaOptions
{
    public const string SectionName = "Habitica";

    public required string UserId { get; init; }
    public required string ApiToken { get; init; }
    public int MinLevel { get; init; } = 1;
    public string Language { get; init; } = string.Empty;
    public int MaxPartySize { get; init; } = 30;
    public int InactivityDays { get; init; } = 14;
    public int MinQuestConfirmations { get; init; } = 15;
    public int InviteCheckIntervalSeconds { get; init; } = 30;
}