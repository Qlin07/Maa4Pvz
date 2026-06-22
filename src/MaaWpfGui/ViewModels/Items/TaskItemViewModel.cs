using PropertyChanged;

namespace MaaWpfGui.ViewModels.Items;

[AddINotifyPropertyChangedInterface]
public class TaskItemViewModel
{
    public string Name { get; set; }
    public string TaskType { get; set; }
    public string Icon { get; set; }
    public bool IsEnabled { get; set; } = true;

    public TaskItemViewModel(string name, string taskType)
    {
        Name = name;
        TaskType = taskType;
    }
}
