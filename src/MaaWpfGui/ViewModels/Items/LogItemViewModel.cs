using System;
using System.Windows.Media;
using MaaWpfGui.Constants;

namespace MaaWpfGui.ViewModels.Items;

public class LogItemViewModel
{
    public string Time { get; set; }
    public string Content { get; set; }
    public string ColorKey { get; set; } = UILogColor.Message;
    public Brush Foreground => UiLogColor.GetBrush(ColorKey);

    public LogItemViewModel(string content, string colorKey = UILogColor.Message)
    {
        Time = DateTime.Now.ToString("HH:mm:ss");
        Content = content;
        ColorKey = colorKey;
    }
}
