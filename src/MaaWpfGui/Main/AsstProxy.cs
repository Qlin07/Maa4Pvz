using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MaaWpfGui.Constants;
using MaaWpfGui.Helper;
using MaaWpfGui.Services;
using MaaWpfGui.States;
using Newtonsoft.Json.Linq;
using Serilog;
using Stylet;
using AsstHandle = System.IntPtr;
using AsstTaskId = System.Int32;

namespace MaaWpfGui.Main;

public class AsstProxy
{
    private static readonly ILogger _logger = Log.ForContext<AsstProxy>();
    private readonly RunningState _runningState;
    private AsstHandle _handle;
    private MaaService.CallbackDelegate _callback;

    public event Action<AsstMsg, JObject> MessageReceived;

    public bool Connected { get; private set; }

    public AsstProxy()
    {
        _runningState = RunningState.Instance;
    }

    private static unsafe byte[] EncodeNullTerminatedUtf8(string s)
    {
        var enc = Encoding.UTF8.GetEncoder();
        fixed (char* c = s)
        {
            var len = enc.GetByteCount(c, s.Length, true);
            var buf = new byte[len + 1];
            fixed (byte* ptr = buf)
            {
                enc.Convert(c, s.Length, ptr, len, true, out _, out _, out _);
            }
            return buf;
        }
    }

    public void Init()
    {
        var userDir = Path.Combine(Environment.CurrentDirectory, "debug");
        Directory.CreateDirectory(userDir);

        unsafe
        {
            fixed (byte* ptr = EncodeNullTerminatedUtf8(userDir))
            {
                MaaService.AsstSetUserDir(ptr);
            }

            fixed (byte* ptr = EncodeNullTerminatedUtf8(Environment.CurrentDirectory))
            {
                if (!MaaService.AsstLoadResource(ptr))
                {
                    _logger.Error("LoadResource failed");
                }
            }
        }

        _callback = CallbackFunction;
        _handle = MaaService.AsstCreateEx(_callback, IntPtr.Zero);

        if (_handle == IntPtr.Zero)
        {
            _logger.Error("AsstCreateEx failed");
        }
    }

    private void CallbackFunction(int msg, IntPtr jsonBuffer, IntPtr customArg)
    {
        if (jsonBuffer == IntPtr.Zero)
            return;

        var json = Marshal.PtrToStringAnsi(jsonBuffer);
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            var details = JObject.Parse(json);
            var asstMsg = (AsstMsg)msg;
            MessageReceived?.Invoke(asstMsg, details);

            switch (asstMsg)
            {
                case AsstMsg.AllTasksCompleted:
                    Execute.OnUIThread(() =>
                    {
                        _runningState.SetIdle(true);
                    });
                    break;

                case AsstMsg.TaskChainError:
                    _logger.Warning("TaskChain error: {Details}", details);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Callback parse error");
        }
    }

    public bool Connect(string adbPath, string address, string config)
    {
        if (_handle == IntPtr.Zero)
        {
            _logger.Error("Connect failed: AsstHandle is null, Init may not have completed");
            return false;
        }

        _logger.Information("Connecting: AdbPath={AdbPath}, Address={Address}, Config={Config}", adbPath, address, config);

        bool ret;
        unsafe
        {
            fixed (byte* adbPtr = EncodeNullTerminatedUtf8(adbPath))
            fixed (byte* addrPtr = EncodeNullTerminatedUtf8(address))
            fixed (byte* cfgPtr = EncodeNullTerminatedUtf8(config))
            {
                ret = MaaService.AsstConnect(_handle, adbPtr, addrPtr, cfgPtr);
            }
        }

        Connected = ret;
        _logger.Information("Connect result: {Result}", ret);
        return ret;
    }

    public AsstTaskId AppendTask(string type, string params_ = "{}")
    {
        if (_handle == IntPtr.Zero)
            return 0;

        unsafe
        {
            fixed (byte* typePtr = EncodeNullTerminatedUtf8(type))
            fixed (byte* paramsPtr = EncodeNullTerminatedUtf8(params_))
            {
                return MaaService.AsstAppendTask(_handle, typePtr, paramsPtr);
            }
        }
    }

    public bool SetTaskParams(AsstTaskId taskId, string params_)
    {
        if (_handle == IntPtr.Zero)
            return false;

        unsafe
        {
            fixed (byte* paramsPtr = EncodeNullTerminatedUtf8(params_))
            {
                return MaaService.AsstSetTaskParams(_handle, taskId, paramsPtr);
            }
        }
    }

    public bool Start()
    {
        if (_handle == IntPtr.Zero)
            return false;

        var ret = MaaService.AsstStart(_handle);
        if (ret)
        {
            _runningState.SetIdle(false);
        }
        return ret;
    }

    public bool Stop()
    {
        if (_handle == IntPtr.Zero)
            return false;

        var ret = MaaService.AsstStop(_handle);
        _runningState.SetIdle(true);
        return ret;
    }

    public bool Running => _handle != IntPtr.Zero && MaaService.AsstRunning(_handle);

    /// <summary>
    /// 扫描 resource/tasks/ 下所有带 @EntryPoint 的纯 JSON 任务
    /// </summary>
    public static string GetJsonTaskList()
    {
        try
        {
            var ptr = MaaService.AsstGetJsonTaskList();
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? "[]" : "[]";
        }
        catch
        {
            return "[]";
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            MaaService.AsstDestroy(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
