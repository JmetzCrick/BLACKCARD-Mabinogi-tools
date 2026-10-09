using System;
using System.Diagnostics;
using System.IO;

namespace BuffAssistant.Services;

public static class FastPingService
{
    public static bool IsApplied()
    {
        using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
        using var parameters = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters");
        // The supplied install script uses this exact value to detect prior application.
        return IsAppliedValue(parameters?.GetValue("TcpAckFrequency"));
    }
    public static bool IsAppliedValue(object? value) => value is int number && number == 1;
    public static ProcessStartInfo CreateStartInfo(bool apply)
    {
        var script = Path.Combine(AppContext.BaseDirectory, "Assets", "FastPing", apply ? "Apply.bat" : "Revert.bat");
        if (!File.Exists(script)) throw new FileNotFoundException("패스트핑 배치파일을 찾을 수 없습니다.", script);
        return new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/d /c \"\"{script}\"\"",
            WorkingDirectory = Path.GetDirectoryName(script)!,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Normal
        };
    }
}
