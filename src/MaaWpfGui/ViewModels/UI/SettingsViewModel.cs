using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MaaWpfGui.Constants;
using MaaWpfGui.Helper;
using MaaWpfGui.Main;
using MaaWpfGui.States;
using PropertyChanged;
using Serilog;
using Stylet;

namespace MaaWpfGui.ViewModels.UI;

[AddINotifyPropertyChangedInterface]
public class SettingsViewModel : Screen
{
    private static readonly ILogger _logger = Log.ForContext<SettingsViewModel>();
    private readonly ConfigurationHelper _config;

    public new string DisplayName { get; set; } = "设置";

    #region 连接设置

    public string AdbPath { get; set; }

    public string ConnectAddress { get; set; }

    public string ConnectConfigName { get; set; } = "通用";

    public List<string> ConnectConfigList { get; } = new()
    {
        "通用",
        "BlueStacks",
        "MuMuEmulator",
        "LDPlayer",
        "NoxPlayer",
    };

    public string ConnectionStatus { get; set; } = "未连接";

    #endregion

    #region 界面设置

    public string SelectedTheme { get; set; } = "亮色";

    public List<string> ThemeList { get; } = new()
    {
        "亮色",
        "暗色",
    };

    public bool WindowTopMost { get; set; }

    public bool MinimizeToTray { get; set; }

    public bool AutoStart { get; set; }

    #endregion

    #region 定时设置（保留接口）

    public bool TimerEnabled { get; set; }

    public int TimerHour { get; set; }

    public int TimerMinute { get; set; }

    #endregion

    public SettingsViewModel(RunningState runningState)
    {
        _config = ConfigurationHelper.Instance;

        // 读取配置
        AdbPath = _config.GetValue(ConfigurationKeys.AdbPath, @"D:\Program Files\Netease\MuMu Player 12\shell\adb.exe");
        ConnectAddress = _config.GetValue(ConfigurationKeys.ConnectAddress, "127.0.0.1:7555");
        ConnectConfigName = _config.GetValue(ConfigurationKeys.ConnectConfig, "通用");
        SelectedTheme = _config.GetValue(ConfigurationKeys.GuiTheme, "亮色");
        WindowTopMost = _config.GetBool(ConfigurationKeys.WindowTopMost);
        MinimizeToTray = _config.GetBool(ConfigurationKeys.MinimizeToTray);
        AutoStart = _config.GetBool(ConfigurationKeys.AutoStart);
        TimerEnabled = _config.GetBool(ConfigurationKeys.TimerEnabled);
        TimerHour = _config.GetInt(ConfigurationKeys.TimerHour, 0);
        TimerMinute = _config.GetInt(ConfigurationKeys.TimerMinute, 0);
    }

    #region 连接测试

    public async Task ConnectTest()
    {
        ConnectionStatus = "正在连接...";

        SaveConfig();

        var proxy = Instances.AsstProxy;
        var result = await Task.Run(() =>
        {
            return proxy.Connect(AdbPath, ConnectAddress, ConnectConfigName);
        });

        ConnectionStatus = result ? "连接成功" : "连接失败";

        if (result)
        {
            Instances.TaskQueueViewModel?.AddLog("设备连接成功", UILogColor.Done);
        }
        else
        {
            Instances.TaskQueueViewModel?.AddLog("设备连接失败", UILogColor.Error);
        }
    }

    #endregion

    #region 主题切换

    public void OnSelectedThemeChanged()
    {
        var theme = SelectedTheme switch
        {
            "亮色" => "Light",
            "暗色" => "Dark",
            _ => "Light",
        };

        _config.SetValue(ConfigurationKeys.GuiTheme, theme);

        // 实时应用主题
        ApplyTheme(theme);
    }

    private void ApplyTheme(string theme)
    {
        try
        {
            var themeUri = theme switch
            {
                "Dark" => new Uri("pack://application:,,,/Res/Themes/Dark.xaml", UriKind.Absolute),
                _ => new Uri("pack://application:,,,/Res/Themes/Light.xaml", UriKind.Absolute),
            };

            var themeDict = new ResourceDictionary { Source = themeUri };

            var existingTheme = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));

            if (existingTheme != null)
            {
                Application.Current.Resources.MergedDictionaries.Remove(existingTheme);
            }

            Application.Current.Resources.MergedDictionaries.Add(themeDict);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to apply theme: {Theme}", theme);
        }
    }

    #endregion

    #region 窗口置顶

    public void OnWindowTopMostChanged()
    {
        _config.SetValue(ConfigurationKeys.WindowTopMost, WindowTopMost);
    }

    #endregion

    #region 最小化到托盘

    public void OnMinimizeToTrayChanged()
    {
        _config.SetValue(ConfigurationKeys.MinimizeToTray, MinimizeToTray);
    }

    #endregion

    #region 自动启动

    public void OnAutoStartChanged()
    {
        _config.SetValue(ConfigurationKeys.AutoStart, AutoStart);
    }

    #endregion

    private void SaveConfig()
    {
        _config.SetValue(ConfigurationKeys.AdbPath, AdbPath);
        _config.SetValue(ConfigurationKeys.ConnectAddress, ConnectAddress);
        _config.SetValue(ConfigurationKeys.ConnectConfig, ConnectConfigName);
    }
}
