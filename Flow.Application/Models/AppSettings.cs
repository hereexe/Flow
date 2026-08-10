using System.Text.Json.Serialization;
using Flow.Domain;

namespace Flow.Application.Models;

public class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Shift+T";
    public Language PrimaryLanguage { get; set; } = Language.En;
    public Language SecondaryLanguage { get; set; } = Language.Ru;
    public string ActiveOnlineProvider { get; set; } = ProviderIdentifiers.Azure;

    [JsonIgnore]
    public HotkeyCombination HotkeyCombination
    {
        get => HotkeyCombination.TryParse(Hotkey, out var combo) ? combo : HotkeyCombination.Default;
        set => Hotkey = value.ToString();
    }

    public bool Validate(out List<string> validationErrors)
    {
        validationErrors = new List<string>();

        if (!HotkeyCombination.TryParse(Hotkey, out _))
        {
            validationErrors.Add($"Invalid hotkey format: '{Hotkey}'. Expected format like 'Ctrl+Shift+T'.");
        }

        if (PrimaryLanguage == SecondaryLanguage)
        {
            validationErrors.Add("Primary and secondary languages must be different.");
        }

        if (!ProviderIdentifiers.IsValidOnlineProvider(ActiveOnlineProvider))
        {
            validationErrors.Add($"Invalid online provider: '{ActiveOnlineProvider}'. Supported providers: Azure, DeepL, Google.");
        }

        return validationErrors.Count == 0;
    }
}

