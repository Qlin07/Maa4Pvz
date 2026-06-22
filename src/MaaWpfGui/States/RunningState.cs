using System;
using PropertyChanged;

namespace MaaWpfGui.States;

[AddINotifyPropertyChangedInterface]
public class RunningState
{
    public static RunningState Instance { get; } = new();

    public bool Idle { get; set; } = true;

    public bool Running => !Idle;

    public event Action<bool> RunningStateChanged;

    public void SetIdle(bool idle)
    {
        Idle = idle;
        RunningStateChanged?.Invoke(idle);
    }
}
