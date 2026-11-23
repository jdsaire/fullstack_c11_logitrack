namespace LogiTrack.Services;

/// <summary>The cached inventory list: one key, a 30-second lifetime, cleared by every write that changes it.</summary>
public static class InventoryCache
{
    public const string Key = "inventory:all";

    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
}
