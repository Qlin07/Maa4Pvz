using System.Windows;
using MaaWpfGui.ViewModels.UI;

namespace MaaWpfGui.Views.UI;

public partial class RootView : HandyControl.Controls.Window
{
    public RootView()
    {
        InitializeComponent();
    }

    private void TaskQueue_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is RootViewModel vm)
        {
            vm.NavigateToTaskQueue();
        }
    }

    private void Settings_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is RootViewModel vm)
        {
            vm.NavigateToSettings();
        }
    }
}
