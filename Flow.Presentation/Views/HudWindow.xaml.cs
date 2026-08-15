using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Flow.Presentation.Views;

public partial class HudWindow : Window
{
    private readonly DispatcherTimer _hideTimer;
    private Storyboard? _pulseStoryboard;
    private System.Windows.Forms.Screen? _lockedScreen;

    public HudWindow()
    {
        InitializeComponent();

        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += (s, e) => 
        {
            _hideTimer.Stop();
            Hide();
        };

        Loaded += (s, e) =>
        {
            _pulseStoryboard = Resources["DotPulseStoryboard"] as Storyboard;
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// CSS: .hud-v5 .hud-dot { background: var(--accent-blue) = #4d8ef7; animation: pulse }
    /// CSS: .hud-v5 .hud-text { color: #8892ac }
    /// </summary>
    public void ShowTranslating()
    {
        _hideTimer.Stop();
        
        // --accent-blue: #4d8ef7
        StatusDot.Fill = new SolidColorBrush(MediaColor.FromRgb(0x4D, 0x8E, 0xF7));
        // .hud-text color: #8892ac
        MessageText.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x88, 0x92, 0xAC));
        MessageText.Text = "Translating…";

        _pulseStoryboard ??= Resources["DotPulseStoryboard"] as Storyboard;
        _pulseStoryboard?.Begin(this, true);

        ShowAndPosition(null, isNewCycle: true);
    }

    /// <summary>
    /// CSS: .hud-v5.success .hud-dot { background: var(--accent-green) = #34d399; animation: none }
    /// CSS: .hud-v5.success .hud-text { color: #a0b8a8 }
    /// </summary>
    public void ShowSuccess(string? message = null)
    {
        _pulseStoryboard?.Stop(this);
        StatusDot.BeginAnimation(UIElement.OpacityProperty, null);
        StatusDot.Opacity = 1.0;

        // --accent-green: #34d399
        StatusDot.Fill = new SolidColorBrush(MediaColor.FromRgb(0x34, 0xD3, 0x99));
        // .success .hud-text color: #a0b8a8
        MessageText.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xA0, 0xB8, 0xA8));
        MessageText.Text = string.IsNullOrWhiteSpace(message) ? "Done" : message;
        
        ShowAndPosition(TimeSpan.FromSeconds(1.2), isNewCycle: false);
    }

    /// <summary>
    /// CSS: .hud-v5.error .hud-dot { background: var(--accent-red) = #f87171; animation: none }
    /// CSS: .hud-v5.error .hud-text { color: #b8a0a0 }
    /// </summary>
    public void ShowError(string? message = null)
    {
        _pulseStoryboard?.Stop(this);
        StatusDot.BeginAnimation(UIElement.OpacityProperty, null);
        StatusDot.Opacity = 1.0;

        // --accent-red: #f87171
        StatusDot.Fill = new SolidColorBrush(MediaColor.FromRgb(0xF8, 0x71, 0x71));
        // .error .hud-text color: #b8a0a0
        MessageText.Foreground = new SolidColorBrush(MediaColor.FromRgb(0xB8, 0xA0, 0xA0));
        MessageText.Text = string.IsNullOrWhiteSpace(message) ? "Error" : message;
        
        ShowAndPosition(TimeSpan.FromSeconds(2.5), isNewCycle: false);
    }

    public new void Hide()
    {
        _hideTimer.Stop();
        _pulseStoryboard?.Stop(this);
        StatusDot.BeginAnimation(UIElement.OpacityProperty, null);
        _lockedScreen = null;
        base.Hide();
    }

    public void ApplyThemeColors(Flow.Domain.AppTheme theme)
    {
        // HUD colors are hardcoded to match CSS spec exactly, theme switching is N/A for HUD
    }

    private void ShowAndPosition(TimeSpan? hideAfter = null, bool isNewCycle = false)
    {
        PositionWindow(isNewCycle);
        AnimateWidth();
        
        if (!IsVisible)
        {
            Show();
            PositionWindow(isNewCycle: false);
        }

        if (hideAfter.HasValue)
        {
            _hideTimer.Interval = hideAfter.Value;
            _hideTimer.Start();
        }
    }

    private void AnimateWidth()
    {
        ContainerBorder.BeginAnimation(FrameworkElement.WidthProperty, null);

        MessageText.InvalidateMeasure();
        MessageText.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));

        // Fixed overhead: Left padding (10) + Dot (8) + Gap (8) + Right padding (18) + Border stroke (2) = 46
        double targetWidth = Math.Max(Math.Ceiling(MessageText.DesiredSize.Width + 46.0), 64.0);

        if (!IsVisible || double.IsNaN(ContainerBorder.ActualWidth) || ContainerBorder.ActualWidth <= 0)
        {
            ContainerBorder.Width = targetWidth;
            return;
        }

        double currentWidth = ContainerBorder.ActualWidth;
        if (Math.Abs(currentWidth - targetWidth) < 1.0)
        {
            ContainerBorder.Width = targetWidth;
            return;
        }

        ContainerBorder.Width = currentWidth;
        var animation = new DoubleAnimation
        {
            From = currentWidth,
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        ContainerBorder.BeginAnimation(FrameworkElement.WidthProperty, animation);
    }

    private void PositionWindow(bool isNewCycle)
    {
        if (isNewCycle || _lockedScreen == null)
        {
            var cursorPosition = new Win32Point();
            GetCursorPos(ref cursorPosition);

            _lockedScreen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursorPosition.X, cursorPosition.Y))
                            ?? System.Windows.Forms.Screen.PrimaryScreen
                            ?? (System.Windows.Forms.Screen.AllScreens.Length > 0 ? System.Windows.Forms.Screen.AllScreens[0] : null);
        }

        if (_lockedScreen == null) return;

        var workingArea = _lockedScreen.WorkingArea;
        
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        if (source == null)
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            if (dpi.DpiScaleX > 0) dpiX = dpi.DpiScaleX;
            if (dpi.DpiScaleY > 0) dpiY = dpi.DpiScaleY;
        }

        double screenLeft = workingArea.Left / dpiX;
        double screenBottom = workingArea.Bottom / dpiY;
        double screenWidth = workingArea.Width / dpiX;

        double windowWidth = Width > 0 ? Width : 600;
        double windowHeight = Height > 0 ? Height : 64;

        this.Left = screenLeft + (screenWidth - windowWidth) / 2.0;
        this.Top = screenBottom - windowHeight - 32.0;
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(ref Win32Point pt);

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }
}
