namespace Flow.Application.Abstractions;

public interface ITranslationHud
{
    void ShowTranslating();
    void ShowSuccess();
    void ShowError();
    void Hide();
}
