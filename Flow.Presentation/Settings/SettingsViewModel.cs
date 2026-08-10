using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Flow.Application.Abstractions;
using Flow.Application.Models;
using Flow.Domain;

namespace Flow.Presentation.Settings;

/// <summary>
/// ViewModel for the Settings window. Uses CommunityToolkit.Mvvm source generators
/// for observable properties and relay commands.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private const string MaskedPlaceholder = "••••••••";

    private readonly ISettingsStore _settingsStore;
    private readonly ISecretStore _secretStore;
    private readonly IHotkeyService _hotkeyService;
    private readonly IHudStatusNotifier _hudStatusNotifier;

    /// <summary>
    /// Stores the original hotkey string loaded at window open,
    /// used for rollback if the new hotkey registration fails.
    /// </summary>
    private readonly string _originalHotkey;

    /// <summary>
    /// The callback to re-register on the hotkey service after a successful change.
    /// Captured from the existing hotkey registration flow in App.xaml.cs.
    /// </summary>
    private readonly Action _hotkeyPressedCallback;

    [ObservableProperty]
    private Language _primaryLanguage;

    [ObservableProperty]
    private Language _secondaryLanguage;

    [ObservableProperty]
    private string _hotkey = string.Empty;

    [ObservableProperty]
    private TranslationMode _mode;

    [ObservableProperty]
    private string _activeOnlineProvider = ProviderIdentifiers.Azure;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Raised when the ViewModel wants the window to close.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Indicates whether a save was performed successfully (used by the window to distinguish save vs cancel).
    /// </summary>
    public bool SavedSuccessfully { get; private set; }

    public IReadOnlyList<Language> AvailableLanguages { get; } = Enum.GetValues<Language>();

    public IReadOnlyList<TranslationMode> AvailableModes { get; } = Enum.GetValues<TranslationMode>();

    public IReadOnlyList<string> AvailableProviders { get; } = new[]
    {
        ProviderIdentifiers.Azure,
        ProviderIdentifiers.DeepL,
        ProviderIdentifiers.Google
    };

    public SettingsViewModel(
        ISettingsStore settingsStore,
        ISecretStore secretStore,
        IHotkeyService hotkeyService,
        IHudStatusNotifier hudStatusNotifier,
        Action hotkeyPressedCallback)
    {
        _settingsStore = settingsStore;
        _secretStore = secretStore;
        _hotkeyService = hotkeyService;
        _hudStatusNotifier = hudStatusNotifier;
        _hotkeyPressedCallback = hotkeyPressedCallback;

        // Snapshot current settings into ViewModel properties
        var settings = _settingsStore.Load();
        _primaryLanguage = settings.PrimaryLanguage;
        _secondaryLanguage = settings.SecondaryLanguage;
        _hotkey = settings.Hotkey;
        _mode = settings.Mode;
        _activeOnlineProvider = settings.ActiveOnlineProvider;
        _originalHotkey = settings.Hotkey;

        // Load API key masked indicator for initial provider
        RefreshApiKeyPlaceholder();
    }

    /// <summary>
    /// Called by the source generator when ActiveOnlineProvider changes.
    /// Refreshes the API key field to show the masked placeholder for the new provider.
    /// </summary>
    partial void OnActiveOnlineProviderChanged(string value)
    {
        RefreshApiKeyPlaceholder();
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = string.Empty;

        // Build an AppSettings from ViewModel state and validate
        var newSettings = new AppSettings
        {
            Hotkey = Hotkey,
            PrimaryLanguage = PrimaryLanguage,
            SecondaryLanguage = SecondaryLanguage,
            Mode = Mode,
            ActiveOnlineProvider = ActiveOnlineProvider
        };

        if (!newSettings.Validate(out var errors))
        {
            ErrorMessage = string.Join(Environment.NewLine, errors);
            return;
        }

        // Handle API key: save, delete, or leave unchanged
        HandleApiKey();

        // Attempt hotkey re-registration with rollback
        bool hotkeyChanged = !string.Equals(_originalHotkey, Hotkey, StringComparison.OrdinalIgnoreCase);
        if (hotkeyChanged)
        {
            _hotkeyService.Unregister();

            bool registered = _hotkeyService.Register(Hotkey, _hotkeyPressedCallback);
            if (!registered)
            {
                // Rollback: re-register the original hotkey
                _hotkeyService.Register(_originalHotkey, _hotkeyPressedCallback);
                ErrorMessage = $"Hotkey conflict: '{Hotkey}' is already registered by another application.";
                _hudStatusNotifier.ShowError(ErrorMessage);
                return;
            }
        }

        // Persist settings
        _settingsStore.Save(newSettings);

        SavedSuccessfully = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke();
    }

    private void HandleApiKey()
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            // User cleared the field — delete the stored credential
            _secretStore.DeleteSecret(ActiveOnlineProvider);
        }
        else if (ApiKey != MaskedPlaceholder)
        {
            // User typed a new key — save it
            _secretStore.SaveSecret(ActiveOnlineProvider, ApiKey);
        }
        // If ApiKey == MaskedPlaceholder, the user didn't change it — leave credential as is
    }

    private void RefreshApiKeyPlaceholder()
    {
        if (_secretStore.HasSecret(ActiveOnlineProvider))
        {
            ApiKey = MaskedPlaceholder;
        }
        else
        {
            ApiKey = string.Empty;
        }
    }
}
