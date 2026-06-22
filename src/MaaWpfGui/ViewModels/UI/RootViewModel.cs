using System;
using System.Threading.Tasks;
using System.Windows;
using MaaWpfGui.Constants;
using MaaWpfGui.Helper;
using MaaWpfGui.Main;
using Serilog;
using Stylet;

namespace MaaWpfGui.ViewModels.UI;

public class RootViewModel : Conductor<Screen>.Collection.OneActive
{
    private static readonly ILogger _logger = Log.ForContext<RootViewModel>();

    public string WindowTitle { get; } = "Maa4Pvz";

    public string WindowVersionInfo { get; } = "v0.1.0";

    public bool IsWindowTopMost { get; set; }

    protected override void OnViewLoaded()
    {
        base.OnViewLoaded();

        // 加载子页面
        var taskQueueVm = Instances.TaskQueueViewModel;
        var settingsVm = Instances.SettingsViewModel;

        if (taskQueueVm != null)
            Items.Add(taskQueueVm);

        if (settingsVm != null)
            Items.Add(settingsVm);

        // 默认选中任务队列页
        ActiveItem = (Screen)taskQueueVm ?? settingsVm;

        // 读取配置
        IsWindowTopMost = ConfigurationHelper.Instance.GetBool(ConfigurationKeys.WindowTopMost);

        // 初始化 AsstProxy（后台线程，避免阻塞UI）
        _ = Task.Run(() =>
        {
            try
            {
                Instances.AsstProxy.Init();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "AsstProxy init failed");
            }
        });
    }

    // 导航切换
    public void NavigateToTaskQueue()
    {
        ActiveItem = Instances.TaskQueueViewModel;
    }

    public void NavigateToSettings()
    {
        ActiveItem = Instances.SettingsViewModel;
    }

    // 窗口关闭
    protected override void OnClose()
    {
        ConfigurationHelper.Instance.Save();
        Instances.AsstProxy?.Dispose();
        Log.CloseAndFlush();
    }
}
