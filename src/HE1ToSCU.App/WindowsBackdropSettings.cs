using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using Avalonia.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace HE1ToSCU.App;

internal static class WindowsBackdropSettings
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static bool IsWindows11 => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    public static bool AllowsMica() => Evaluate(IsWindows11, ReadTransparency, HasComposition);

    internal static bool Evaluate(bool supportsMica, Func<bool> readTransparency, Func<bool> hasComposition)
    {
        try { return supportsMica && readTransparency() && hasComposition(); }
        catch (Exception e) when (IsUnavailable(e)) { return false; }
    }

    public static IDisposable? Watch(Action changed, string registryPath = PersonalizeKey)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return null;
        try
        {
            var key = Registry.CurrentUser.OpenSubKey(registryPath);
            if (key == null) return null;
            var watcher = new RegistryWatcher(key, changed);
            if (watcher.Arm()) return watcher;
            watcher.Dispose();
        }
        catch (Exception e) when (IsUnavailable(e)) { }
        return null;
    }

    private static bool ReadTransparency() => OperatingSystem.IsWindows() &&
        Registry.GetValue(@"HKEY_CURRENT_USER\" + PersonalizeKey, "EnableTransparency", 0) is 1;

    private static bool HasComposition() => DwmIsCompositionEnabled(out bool enabled) >= 0 && enabled;

    internal static bool IsUnavailable(Exception e) => e is IOException or UnauthorizedAccessException
        or SecurityException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException
        or COMException or Win32Exception or NotSupportedException;

    [DllImport("dwmapi.dll")]
    private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

    // Each registry signal is one-shot. Re-arm on the UI thread before reading the latest value.
    [SupportedOSPlatform("windows")]
    private sealed class RegistryWatcher(RegistryKey key, Action changed) : IDisposable
    {
        private const uint LastSetAndThreadAgnostic = 0x10000004;
        private readonly AutoResetEvent signal = new(false);
        private RegisteredWaitHandle? wait;
        private bool disposed;

        public bool Arm()
        {
            try
            {
                if (RegNotifyChangeKeyValue(key.Handle, false, LastSetAndThreadAgnostic, signal.SafeWaitHandle, true) != 0)
                    return false;
                wait = ThreadPool.RegisterWaitForSingleObject(signal,
                    (_, _) => Dispatcher.UIThread.Post(OnChanged), null, Timeout.Infinite, executeOnlyOnce: true);
                return true;
            }
            catch (Exception e) when (IsUnavailable(e)) { return false; }
        }

        private void OnChanged()
        {
            if (disposed) return;
            if (!Arm()) Dispose();
            changed();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            wait?.Unregister(null);
            key.Dispose();
            signal.Dispose();
        }

        [DllImport("advapi32.dll")]
        private static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key,
            [MarshalAs(UnmanagedType.Bool)] bool watchSubtree, uint filter, SafeWaitHandle signal,
            [MarshalAs(UnmanagedType.Bool)] bool asynchronous);
    }
}
