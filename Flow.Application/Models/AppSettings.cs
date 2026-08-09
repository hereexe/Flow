using Flow.Domain;

namespace Flow.Application.Models;

public class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Shift+T";
    public Language PrimaryLanguage { get; set; } = Language.En;
    public Language SecondaryLanguage { get; set; } = Language.Ru;
    public TranslationMode Mode { get; set; } = TranslationMode.Online;
    public string ActiveOnlineProvider { get; set; } = "Azure";
}
