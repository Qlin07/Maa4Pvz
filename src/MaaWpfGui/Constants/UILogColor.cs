using System.Windows.Media;

namespace MaaWpfGui.Constants;

// 日志颜色字符串常量
public static class UILogColor
{
    public const string Message = "Message";
    public const string Error = "Error";
    public const string Warning = "Warning";
    public const string Trace = "Trace";
    public const string Debug = "Debug";
    public const string Done = "Done";
}

// 日志颜色画刷
public static class UiLogColor
{
    public static readonly SolidColorBrush MessageBrush = new(System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60));
    public static readonly SolidColorBrush ErrorBrush = new(System.Windows.Media.Color.FromRgb(0xE0, 0x3E, 0x3E));
    public static readonly SolidColorBrush WarningBrush = new(System.Windows.Media.Color.FromRgb(0xE8, 0xA8, 0x38));
    public static readonly SolidColorBrush DoneBrush = new(System.Windows.Media.Color.FromRgb(0x3E, 0xB4, 0x89));
    public static readonly SolidColorBrush TraceBrush = new(System.Windows.Media.Color.FromRgb(0x90, 0x90, 0x90));

    public static Brush GetBrush(string colorKey)
    {
        return colorKey switch
        {
            UILogColor.Error => ErrorBrush,
            UILogColor.Warning => WarningBrush,
            UILogColor.Done => DoneBrush,
            UILogColor.Trace => TraceBrush,
            _ => MessageBrush,
        };
    }
}
