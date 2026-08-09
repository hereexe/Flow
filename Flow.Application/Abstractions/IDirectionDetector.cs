using Flow.Domain;

namespace Flow.Application.Abstractions;

public interface IDirectionDetector
{
    (Language Source, Language Target) Detect(string text, Language primary, Language secondary);
}
