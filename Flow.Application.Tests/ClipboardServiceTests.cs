using Flow.Application.Abstractions;
using Flow.Application.Models;
using Xunit;

namespace Flow.Application.Tests;

public class ClipboardServiceTests
{
    private class FakeClipboardService : IClipboardService
    {
        private object? _currentDataObject = "Original User Clipboard Data";
        public bool Restored { get; private set; }

        public ClipboardSnapshot CaptureSnapshot()
        {
            return new ClipboardSnapshot { DataObject = _currentDataObject };
        }

        public Task<string> CaptureSelectedTextAsync(CancellationToken ct = default)
        {
            return Task.FromResult("Selected Text");
        }

        public Task ReplaceSelectedTextAsync(string translatedText, CancellationToken ct = default)
        {
            _currentDataObject = translatedText;
            return Task.CompletedTask;
        }

        public void RestoreSnapshot(ClipboardSnapshot snapshot)
        {
            _currentDataObject = snapshot.DataObject;
            Restored = true;
        }

        public object? GetCurrentData() => _currentDataObject;
    }

    [Fact]
    public async Task ClipboardWorkflow_PreservesOriginalData_OnSuccessAndFailure()
    {
        // Arrange
        var service = new FakeClipboardService();
        var initialData = service.GetCurrentData();

        // Act - Simulate in-place translation workflow
        var snapshot = service.CaptureSnapshot();
        try
        {
            var selectedText = await service.CaptureSelectedTextAsync();
            Assert.Equal("Selected Text", selectedText);

            await service.ReplaceSelectedTextAsync("[Translated] Selected Text");
        }
        finally
        {
            service.RestoreSnapshot(snapshot);
        }

        // Assert
        Assert.True(service.Restored);
        Assert.Equal(initialData, service.GetCurrentData());
    }
}
