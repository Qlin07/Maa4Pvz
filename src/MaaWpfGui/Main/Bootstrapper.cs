using System;
using System.IO;
using System.Linq;
using System.Windows;
using MaaWpfGui.Constants;
using MaaWpfGui.Helper;
using MaaWpfGui.Services;
using MaaWpfGui.States;
using MaaWpfGui.ViewModels.UI;
using Serilog;
using Stylet;
using StyletIoC;

namespace MaaWpfGui.Main;

public class Bootstrapper : Bootstrapper<RootViewModel>
{
    private static ILogger _logger;

    protected override void OnStart()
    {
        base.OnStart();

        PathsHelper.EnsureDirectories();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Debug()
            .WriteTo.File(
                Path.Combine(PathsHelper.DebugDir, "gui.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        _logger = Log.ForContext<Bootstrapper>();
        _logger.Information("MaaPvz GUI starting...");
    }

    protected override void ConfigureIoC(IStyletIoCBuilder builder)
    {
        base.ConfigureIoC(builder);

        // 注册全局单例
        builder.Bind<RunningState>().ToSelf().InSingletonScope();
        builder.Bind<AsstProxy>().ToSelf().InSingletonScope();
    }

    protected override void Configure()
    {
        base.Configure();

        // 初始化 Instances
        Instances.WindowManager = Container.Get<Stylet.IWindowManager>();
        Instances.AsstProxy = Container.Get<AsstProxy>();

        // 加载语言
        var config = ConfigurationHelper.Instance;
        var language = config.GetValue(ConfigurationKeys.GuiLanguage, "zh-cn");
        LocalizationHelper.LoadLanguage(language);

        // 应用主题
        var theme = config.GetValue(ConfigurationKeys.GuiTheme, "Light");
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

            // 替换现有主题
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
            _logger?.Error(ex, "Failed to apply theme: {Theme}", theme);
        }
    }

    protected override void OnLaunch()
    {
        base.OnLaunch();

        // 初始化 ViewModel 实例到 Instances
        Instances.TaskQueueViewModel = Container.Get<TaskQueueViewModel>();
        Instances.SettingsViewModel = Container.Get<SettingsViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Information("MaaPvz GUI exiting...");

        // 保存配置
        ConfigurationHelper.Instance.Save();

        // 销毁 AsstProxy
        Instances.AsstProxy?.Dispose();

        // 关闭日志
        Log.CloseAndFlush();

        base.OnExit(e);
    }
}
