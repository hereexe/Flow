namespace Flow.Domain;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public record HotkeyCombination
{
    public HotkeyModifiers Modifiers { get; init; }
    public string Key { get; init; }

    public HotkeyCombination(HotkeyModifiers modifiers, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        }

        Modifiers = modifiers;
        Key = key.Trim().ToUpperInvariant();
    }

    public static HotkeyCombination Default => new(HotkeyModifiers.Control | HotkeyModifiers.Shift, "T");

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }

    public static bool TryParse(string? input, out HotkeyCombination result)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            result = Default;
            return false;
        }

        var tokens = input.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            result = Default;
            return false;
        }

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        string? key = null;

        foreach (var token in tokens)
        {
            var normalized = token.ToLowerInvariant();
            switch (normalized)
            {
                case "ctrl":
                case "control":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "win":
                case "windows":
                    modifiers |= HotkeyModifiers.Win;
                    break;
                default:
                    key = token.ToUpperInvariant();
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            result = Default;
            return false;
        }

        result = new HotkeyCombination(modifiers, key);
        return true;
    }

    public static HotkeyCombination Parse(string input)
    {
        if (TryParse(input, out var result))
        {
            return result;
        }

        throw new FormatException($"Invalid hotkey format: '{input}'");
    }
}
