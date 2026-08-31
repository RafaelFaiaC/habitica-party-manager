namespace HabiticaPartyManager.Habitica.Models;

public class HabiticaApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
}