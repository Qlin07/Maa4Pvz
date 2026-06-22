using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using AsstHandle = System.IntPtr;
using AsstInstanceOptionKey = System.Int32;
using AsstTaskId = System.Int32;

namespace MaaWpfGui.Services;

internal static partial class MaaService
{
    internal delegate void CallbackDelegate(int msg, IntPtr jsonBuffer, IntPtr customArg);

    internal delegate void ProcCallbackMsg(AsstMsg msg, JObject details);

    [LibraryImport("MaaCore.dll")]
    internal static partial AsstHandle AsstCreateEx(CallbackDelegate callback, IntPtr customArg);

    [LibraryImport("MaaCore.dll")]
    internal static partial void AsstDestroy(AsstHandle handle);

    [LibraryImport("MaaCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool AsstSetInstanceOption(AsstHandle handle, AsstInstanceOptionKey key, byte* value);

    [LibraryImport("MaaCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool AsstSetUserDir(byte* dirname);

    [LibraryImport("MaaCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool AsstLoadResource(byte* dirname);

    [LibraryImport("MaaCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool AsstConnect(AsstHandle handle, byte* adbPath, byte* address, byte* config);

    [LibraryImport("MaaCore.dll")]
    internal static unsafe partial AsstTaskId AsstAppendTask(AsstHandle handle, byte* type, byte* taskParams);

    [LibraryImport("MaaCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool AsstSetTaskParams(AsstHandle handle, AsstTaskId id, byte* taskParams);

    [LibraryImport("MaaCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AsstStart(AsstHandle handle);

    [LibraryImport("MaaCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AsstRunning(AsstHandle handle);

    [LibraryImport("MaaCore.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AsstStop(AsstHandle handle);

    [LibraryImport("MaaCore.dll")]
    internal static partial nint AsstGetJsonTaskList();
}

public enum AsstMsg : int
{
    /* Global Info */
    InternalError = 0,
    InitFailed,
    ConnectionInfo,
    AllTasksCompleted,
    AsyncCallInfo,
    Destroyed,

    /* TaskChain Info */
    TaskChainError = 10000,
    TaskChainStart,
    TaskChainCompleted,
    TaskChainExtraInfo,
    TaskChainStopped,

    /* SubTask Info */
    SubTaskError = 20000,
    SubTaskStart,
    SubTaskCompleted,
    SubTaskExtraInfo,
    SubTaskStopped,
}
