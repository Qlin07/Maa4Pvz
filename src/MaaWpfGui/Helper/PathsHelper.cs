using System;
using System.IO;

namespace MaaWpfGui.Helper;

public static class PathsHelper
{
    public static string DebugDir { get; } = Path.Combine(Environment.CurrentDirectory, "debug");

    public static string LogDir { get; } = Path.Combine(DebugDir, "interface");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DebugDir);
        Directory.CreateDirectory(LogDir);
    }
}
