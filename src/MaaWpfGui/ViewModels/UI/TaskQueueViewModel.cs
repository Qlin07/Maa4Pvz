using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MaaWpfGui.Constants;
using MaaWpfGui.Helper;
using MaaWpfGui.Main;
using MaaWpfGui.Services;
using MaaWpfGui.States;
using MaaWpfGui.ViewModels.Items;
using Newtonsoft.Json.Linq;
using PropertyChanged;
using Serilog;
using Stylet;

namespace MaaWpfGui.ViewModels.UI;

[AddINotifyPropertyChangedInterface]
public class TaskQueueViewModel : Screen
{
    private static readonly ILogger _logger = Log.ForContext<TaskQueueViewModel>();
    private readonly RunningState _runningState;
    private readonly ConfigurationHelper _config;

    /// <summary>
    /// 任务列表项
    /// </summary>
    public ObservableCollection<TaskItemViewModel> TaskItemViewModels { get; } = new();

    /// <summary>
    /// 日志列表
    /// </summary>
    public ObservableCollection<LogItemViewModel> LogItemViewModels { get; } = new();

    /// <summary>
    /// 任务状态
    /// </summary>
    public string TaskStatusText { get; set; } = "空闲";

    public Brush TaskStatusBrush { get; set; } = UiLogColor.MessageBrush;

    /// <summary>
    /// 是否空闲
    /// </summary>
    public bool Idle => _runningState.Idle;

    /// <summary>
    /// 是否已初始化（连接成功后才可开始任务）
    /// </summary>
    public bool Inited { get; set; }

    public TaskQueueViewModel(RunningState runningState)
    {
        _runningState = runningState;
        _config = ConfigurationHelper.Instance;

        // 注册运行状态变化
        _runningState.RunningStateChanged += OnIdleStateChanged;

        // 初始化任务列表（从配置读取顺序，没配置就用默认顺序）
        InitTaskList();

        // 注册 MaaCore 回调
        if (Instances.AsstProxy != null)
        {
            Instances.AsstProxy.MessageReceived += OnMessageReceived;
        }
    }

    private void InitTaskList()
    {
        // 当前只有一个任务：戴夫杯
        // 后续每新增一个 InterfaceTask，在这里加一行
        var savedOrder = _config.GetValue(ConfigurationKeys.TaskOrder);
        var savedEnabled = _config.GetValue(ConfigurationKeys.TaskEnabled, "");

        var allTasks = new List<TaskItemViewModel>
        {
            new("戴夫杯", "FtCrickets"),
        };

        if (!string.IsNullOrEmpty(savedOrder))
        {
            try
            {
                var orderArr = JArray.Parse(savedOrder);
                var ordered = new List<TaskItemViewModel>();
                foreach (var type in orderArr.Select(t => t.ToString()))
                {
                    var task = allTasks.FirstOrDefault(t => t.TaskType == type);
                    if (task != null)
                    {
                        ordered.Add(task);
                    }
                }

                // 新增的任务加到末尾
                foreach (var task in allTasks.Where(t => !ordered.Contains(t)))
                {
                    ordered.Add(task);
                }

                allTasks = ordered;
            }
            catch
            {
                // 解析失败使用默认顺序
            }
        }

        // 恢复启用状态
        if (!string.IsNullOrEmpty(savedEnabled))
        {
            try
            {
                var enabledArr = JArray.Parse(savedEnabled);
                foreach (var task in allTasks)
                {
                    task.IsEnabled = enabledArr.Any(t => t.ToString() == task.TaskType);
                }
            }
            catch { }
        }

        TaskItemViewModels.Clear();
        foreach (var task in allTasks)
        {
            TaskItemViewModels.Add(task);
        }
    }

    private void OnIdleStateChanged(bool idle)
    {
        if (idle)
        {
            TaskStatusText = "空闲";
            TaskStatusBrush = UiLogColor.MessageBrush;
        }
        else
        {
            TaskStatusText = "运行中";
            TaskStatusBrush = UiLogColor.DoneBrush;
        }
    }

    private void OnMessageReceived(AsstMsg msg, JObject details)
    {
        switch (msg)
        {
            case AsstMsg.TaskChainStart:
                var startChain = details["taskchain"]?.ToString();
                AddLog($"[{startChain}] 任务链开始", UILogColor.Trace);
                break;

            case AsstMsg.TaskChainCompleted:
                var completedChain = details["taskchain"]?.ToString();
                AddLog($"[{completedChain}] 任务链完成", UILogColor.Done);
                break;

            case AsstMsg.TaskChainError:
                var errorChain = details["taskchain"]?.ToString();
                AddLog($"[{errorChain}] 任务链出错", UILogColor.Error);
                break;

            case AsstMsg.SubTaskStart:
                var subTask = details["subtask"]?.ToString();
                var taskName = details["details"]?["task"]?.ToString();
                if (!string.IsNullOrEmpty(taskName) && subTask == "ProcessTask")
                {
                    AddLog($"  -> {taskName}", UILogColor.Trace);
                }
                break;

            case AsstMsg.SubTaskExtraInfo:
                var what = details["what"]?.ToString();
                if (what == "ScreencapCost")
                {
                    // 截图性能信息，不显示
                }
                break;

            case AsstMsg.AllTasksCompleted:
                AddLog("所有任务已完成", UILogColor.Done);
                break;
        }
    }

    /// <summary>
    /// 添加日志
    /// </summary>
    public void AddLog(string content, string colorKey = UILogColor.Message)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogItemViewModels.Add(new LogItemViewModel(content, colorKey));

            // 保持日志不超过 2000 条
            while (LogItemViewModels.Count > 2000)
            {
                LogItemViewModels.RemoveAt(0);
            }
        });
    }

    /// <summary>
    /// 开始任务
    /// </summary>
    public async Task LinkStart()
    {
        if (!_runningState.Idle)
        {
            AddLog("任务正在运行中", UILogColor.Warning);
            return;
        }

        var proxy = Instances.AsstProxy;

        // 如果未连接，尝试连接
        if (!proxy.Connected)
        {
            AddLog("正在连接设备...", UILogColor.Message);

            var adbPath = _config.GetValue(ConfigurationKeys.AdbPath, @"D:\Program Files\Netease\MuMu Player 12\shell\adb.exe");
            var address = _config.GetValue(ConfigurationKeys.ConnectAddress, "127.0.0.1:7555");
            var cfg = _config.GetValue(ConfigurationKeys.ConnectConfig, "General");

            var connected = await Task.Run(() => proxy.Connect(adbPath, address, cfg));

            if (!connected)
            {
                AddLog("设备连接失败", UILogColor.Error);
                return;
            }

            AddLog("设备已连接", UILogColor.Done);
            Inited = true;
        }

        // 添加任务
        var hasEnabled = false;
        foreach (var task in TaskItemViewModels)
        {
            if (!task.IsEnabled)
                continue;

            hasEnabled = true;
            proxy.AppendTask(task.TaskType);
            AddLog($"已添加任务：{task.Name}", UILogColor.Message);
        }

        if (!hasEnabled)
        {
            AddLog("没有启用的任务", UILogColor.Warning);
            return;
        }

        // 保存任务启用状态
        SaveTaskState();

        // 启动
        if (proxy.Start())
        {
            AddLog("任务已启动", UILogColor.Done);
        }
        else
        {
            AddLog("任务启动失败", UILogColor.Error);
        }
    }

    /// <summary>
    /// 停止任务
    /// </summary>
    public void LinkStop()
    {
        if (_runningState.Idle)
            return;

        Instances.AsstProxy.Stop();
        AddLog("任务已停止", UILogColor.Warning);
    }

    private void SaveTaskState()
    {
        var order = new JArray(TaskItemViewModels.Select(t => t.TaskType));
        _config.SetValue(ConfigurationKeys.TaskOrder, order.ToString());

        var enabled = new JArray(TaskItemViewModels.Where(t => t.IsEnabled).Select(t => t.TaskType));
        _config.SetValue(ConfigurationKeys.TaskEnabled, enabled.ToString());
    }
}
