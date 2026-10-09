using Microsoft.Win32;

namespace BuffAssistant.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BlackCardHelper";
    public static string BuildCommand(string exe, bool background) => $"\"{exe}\"" + (background ? " --background" : "");
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        var exe = Environment.ProcessPath;
        return exe is not null && key?.GetValue(ValueName) is string command &&
            (command == BuildCommand(exe, false) || command == BuildCommand(exe, true));
    }
    public static void SetEnabled(bool enabled, bool background)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(ValueName, BuildCommand(Environment.ProcessPath ?? throw new InvalidOperationException("실행 파일 위치를 확인할 수 없습니다."), background), RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
}
