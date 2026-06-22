using MaaWpfGui.Main;
using MaaWpfGui.Services;
using MaaWpfGui.States;
using MaaWpfGui.ViewModels.UI;
using Stylet;

namespace MaaWpfGui.Helper;

public static class Instances
{
    public static IWindowManager WindowManager { get; set; }
    public static TaskQueueViewModel TaskQueueViewModel { get; set; }
    public static SettingsViewModel SettingsViewModel { get; set; }
    public static AsstProxy AsstProxy { get; set; }
}
