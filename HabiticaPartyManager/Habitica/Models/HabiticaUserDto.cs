using System.Text.Json.Serialization;

namespace HabiticaPartyManager.Habitica.Models;

public class HabiticaUserDto
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    public ProfileDto Profile { get; set; } = new();
    public AuthDto Auth { get; set; } = new();
    public PreferencesDto Preferences { get; set; } = new();
    public StatsDto Stats { get; set; } = new();
}

public class ProfileDto
{
    public string Name { get; set; } = string.Empty;
}

public class AuthDto
{
    public AuthLocalDto Local { get; set; } = new();
    public AuthTimestampsDto Timestamps { get; set; } = new();
}

public class AuthLocalDto
{
    public string Username { get; set; } = string.Empty;
}

public class AuthTimestampsDto
{
    public DateTimeOffset LoggedIn { get; set; }
}

public class PreferencesDto
{
    public string Language { get; set; } = string.Empty;
}

public class StatsDto
{
    public int Lvl { get; set; }
}