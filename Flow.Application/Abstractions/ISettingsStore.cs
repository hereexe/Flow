using Flow.Application.Models;

namespace Flow.Application.Abstractions;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
