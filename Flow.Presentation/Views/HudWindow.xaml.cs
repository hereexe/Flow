using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;

namespace Flow.Presentation.Views;

public partial class HudWindow : Window
{
    private readonly DispatcherTimer _hideTimer;

    public HudWindow()
    {
        InitializeComponent();

        _hideTimer = new DispatcherTimer();
        _hideTimer.Tick += (s, e) => 
        {
            _hideTimer.Stop();
            Hide();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    public void ShowTranslating()
    {
        _hideTimer.Stop();
        
        Spinner.Visibility = Visibility.Visible;
        SuccessIcon.Visibility = Visibility.Collapsed;
        ErrorIcon.Visibility = Visibility.Collapsed;
        MessageText.Text = "Translating...";
        
        ShowAndPosition();
    }

    public void ShowSuccess(string? message = null)
    {
        Spinner.Visibility = Visibility.Collapsed;
        SuccessIcon.Visibility = Visibility.Visible;
        ErrorIcon.Visibility = Visibility.Collapsed;
        MessageText.Text = string.IsNullOrWhiteSpace(message) ? "Done" : message;
        
        ShowAndPosition(TimeSpan.FromSeconds(1));
    }

    public void ShowError(string? message = null)
    {
        Spinner.Visibility = Visibility.Collapsed;
        SuccessIcon.Visibility = Visibility.Collapsed;
        ErrorIcon.Visibility = Visibility.Visible;
        MessageText.Text = string.IsNullOrWhiteSpace(message) ? "Error" : message;
        
        ShowAndPosition(TimeSpan.FromSeconds(2.5));
    }

    public void ApplyThemeColors(Flow.Domain.AppTheme theme)
    {
        switch (theme)
        {
            case Flow.Domain.AppTheme.Light:
                ContainerBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1e, 0x29, 0x3b));
                ContainerBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xff, 0xff, 0xff));
                MessageText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xff, 0xff, 0xff));
                Spinner.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3b, 0x82, 0xf6));
                GlowEffect.Opacity = 0;
                ContainerBorder.CornerRadius = new CornerRadius(24);
                break;
            case Flow.Domain.AppTheme.Dark:
                ContainerBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0f, 0x17, 0x2a));
                ContainerBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x41, 0x55));
                MessageText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xf8, 0xfa, 0xfc));
                Spinner.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x38, 0xbd, 0xf8));
                GlowEffect.Opacity = 0;
                ContainerBorder.CornerRadius = new CornerRadius(30);
                break;
        }
    }

    private void ShowAndPosition(TimeSpan? hideAfter = null)
    {
        PositionWindow();
        
        if (!IsVisible)
        {
            Show();
        }

        if (hideAfter.HasValue)
        {
            _hideTimer.Interval = hideAfter.Value;
            _hideTimer.Start();
        }
    }

    private void PositionWindow()
    {
        Win32Point cursorPosition = new Win32Point();
        GetCursorPos(ref cursorPosition);

        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursorPosition.X, cursorPosition.Y));
        var workingArea = screen.WorkingArea;
        
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double screenLeft = workingArea.Left / dpiX;
        double screenBottom = workingArea.Bottom / dpiY;
        double screenWidth = workingArea.Width / dpiX;

        this.Left = screenLeft + (screenWidth - this.Width) / 2;
        this.Top = screenBottom - this.Height - 40;
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
