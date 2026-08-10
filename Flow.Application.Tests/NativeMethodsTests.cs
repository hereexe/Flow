using Flow.Domain;
using Flow.Infrastructure.Windows;
using Xunit;

namespace Flow.Application.Tests;

public class NativeMethodsTests
{
    [Theory]
    [InlineData(HotkeyModifiers.Alt, 0x0001u)]
    [InlineData(HotkeyModifiers.Control, 0x0002u)]
    [InlineData(HotkeyModifiers.Shift, 0x0004u)]
    [InlineData(HotkeyModifiers.Win, 0x0008u)]
    public void ToModifiers_SingleModifier_MapsToCorrectWin32Constant(HotkeyModifiers modifier, uint expected)
    {
        Assert.Equal(expected, NativeMethods.ToModifiers(modifier));
    }

    [Fact]
    public void ToModifiers_ControlPlusShift_MapsToCorrectCombination()
    {
        var modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift;
        Assert.Equal(0x0006u, NativeMethods.ToModifiers(modifiers));
    }

    [Fact]
    public void ToModifiers_AllModifiers_MapsToCorrectCombination()
    {
        var modifiers = HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win;
        Assert.Equal(0x000Fu, NativeMethods.ToModifiers(modifiers));
    }

    [Fact]
    public void ToModifiers_None_ReturnsZero()
    {
        Assert.Equal(0u, NativeMethods.ToModifiers(HotkeyModifiers.None));
    }

    [Theory]
    [InlineData("T")]
    [InlineData("A")]
    [InlineData("Z")]
    [InlineData("F1")]
    [InlineData("Space")]
    public void ToVirtualKey_ValidKeys_ReturnsNonZero(string key)
    {
        uint vk = NativeMethods.ToVirtualKey(key);
        Assert.NotEqual(0u, vk);
    }

    [Fact]
    public void ToVirtualKey_InvalidKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => NativeMethods.ToVirtualKey("INVALID_KEY_XYZ"));
    }
}
