using Flow.Application.Abstractions;
using Flow.Domain;
using Xunit;

namespace Flow.Application.Tests;

public class HotkeyServiceTests
{
    private class FakeHotkeyService : IHotkeyService
    {
        private readonly HashSet<string> _occupiedHotkeys = new(StringComparer.OrdinalIgnoreCase);
        public bool IsRegistered { get; private set; }
        public string? ActiveHotkey { get; private set; }

        public FakeHotkeyService(params string[] preOccupied)
        {
            foreach (var item in preOccupied) _occupiedHotkeys.Add(item);
        }

        public bool Register(HotkeyCombination combination, Action onPressed)
        {
            return Register(combination.ToString(), onPressed);
        }

        public bool Register(string keyComboString, Action onPressed)
        {
            if (_occupiedHotkeys.Contains(keyComboString))
            {
                IsRegistered = false;
                return false;
            }

            ActiveHotkey = keyComboString;
            IsRegistered = true;
            return true;
        }

        public void Unregister()
        {
            ActiveHotkey = null;
            IsRegistered = false;
        }
    }

    [Fact]
    public void Register_WithValidHotkey_SucceedsAndSetsRegisteredState()
    {
        // Arrange
        var service = new FakeHotkeyService();
        var combination = HotkeyCombination.Parse("Ctrl+Shift+T");

        // Act
        bool success = service.Register(combination, () => { });

        // Assert
        Assert.True(success);
        Assert.True(service.IsRegistered);
        Assert.Equal("Ctrl+Shift+T", service.ActiveHotkey);
    }

    [Fact]
    public void Register_WhenHotkeyConflicted_ReturnsFalseAndStaysUnregistered()
    {
        // Arrange
        var service = new FakeHotkeyService("Ctrl+Shift+T");
        var combination = HotkeyCombination.Parse("Ctrl+Shift+T");

        // Act
        bool success = service.Register(combination, () => { });

        // Assert
        Assert.False(success);
        Assert.False(service.IsRegistered);
    }
}
