using Flow.Application.Models;

namespace Flow.Application.Abstractions;

public interface ITranslationProviderFactory
{
    ITranslationProvider GetActive(AppSettings settings);
}
