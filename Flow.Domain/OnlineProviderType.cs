namespace Flow.Domain;

public enum OnlineProviderType
{
    Azure,
    DeepL,
    Google
}

public static class ProviderIdentifiers
{
    public const string Azure = "Azure";
    public const string DeepL = "DeepL";
    public const string Google = "Google";

    public static bool IsValidOnlineProvider(string providerId) =>
        providerId is Azure or DeepL or Google;
}
