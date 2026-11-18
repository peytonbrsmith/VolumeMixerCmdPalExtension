using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace VolumeMixerExtension.Audio;

internal sealed record AudioSessionInfo(
    string Id,
    string DisplayName,
    string ProcessName,
    float Volume,
    bool IsMuted,
    string? IconPath);

internal sealed class AudioSessionService : IDisposable
{
    private static readonly Guid AudioSessionManager2Id = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private readonly Timer _sessionTimer;
    private readonly object _sessionLock = new();
    private HashSet<string>? _sessionIds;
    private bool _disposed;

    public event EventHandler? SessionsChanged;

    public AudioSessionService()
    {
        _sessionTimer = new Timer(CheckForSessionChanges, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public static IReadOnlyList<AudioSessionInfo> GetRenderSessions()
    {
        var sessions = new List<AudioSessionInfo>();
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? sessionEnumerator = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
            device.Activate(AudioSessionManager2Id, CLSCTX.All, IntPtr.Zero, out var managerObject);
            manager = (IAudioSessionManager2)managerObject;
            manager.GetSessionEnumerator(out sessionEnumerator);
            sessionEnumerator.GetCount(out var count);

            for (var index = 0; index < count; index++)
            {
                IAudioSessionControl? control = null;
                IAudioSessionControl2? control2 = null;
                ISimpleAudioVolume? volume = null;

                try
                {
                    sessionEnumerator.GetSession(index, out control);
                    control2 = (IAudioSessionControl2)control;
                    control.GetState(out var state);
                    if (state != 1)
                    {
                        continue;
                    }

                    control2.GetProcessId(out var processId);
                    if (processId == 0)
                    {
                        continue;
                    }

                    control2.GetSessionInstanceIdentifier(out var id);
                    control2.GetDisplayName(out var displayName);
                    volume = (ISimpleAudioVolume)control;
                    volume.GetMasterVolume(out var masterVolume);
                    volume.GetMute(out var muted);

                    var process = GetProcessMetadata(processId);
                    var title = string.IsNullOrWhiteSpace(displayName) ? process.Name : displayName;
                    sessions.Add(new AudioSessionInfo(id, title, process.Name, masterVolume, muted, process.IconPath));
                }
                catch (COMException)
                {
                }
                finally
                {
                    Release(volume);
                    Release(control2);
                    Release(control);
                }
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            Release(sessionEnumerator);
            Release(manager);
            Release(device);
            Release(enumerator);
        }

        return sessions
            .OrderBy(session => session.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static bool TrySetVolume(string sessionId, float volume, bool muted)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? sessionEnumerator = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device);
            device.Activate(AudioSessionManager2Id, CLSCTX.All, IntPtr.Zero, out var managerObject);
            manager = (IAudioSessionManager2)managerObject;
            manager.GetSessionEnumerator(out sessionEnumerator);
            sessionEnumerator.GetCount(out var count);

            for (var index = 0; index < count; index++)
            {
                IAudioSessionControl? control = null;
                IAudioSessionControl2? control2 = null;
                ISimpleAudioVolume? simpleVolume = null;

                try
                {
                    sessionEnumerator.GetSession(index, out control);
                    control2 = (IAudioSessionControl2)control;
                    control2.GetSessionInstanceIdentifier(out var currentId);
                    if (!string.Equals(currentId, sessionId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    simpleVolume = (ISimpleAudioVolume)control;
                    simpleVolume.SetMasterVolume(Math.Clamp(volume, 0f, 1f), Guid.Empty);
                    simpleVolume.SetMute(muted, Guid.Empty);
                    return true;
                }
                catch (COMException)
                {
                    return false;
                }
                finally
                {
                    Release(simpleVolume);
                    Release(control2);
                    Release(control);
                }
            }
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            Release(sessionEnumerator);
            Release(manager);
            Release(device);
            Release(enumerator);
        }

        return false;
    }

    public void Dispose()
    {
        lock (_sessionLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _sessionTimer.Dispose();
        }
    }

    private void CheckForSessionChanges(object? state)
    {
        lock (_sessionLock)
        {
            if (_disposed)
            {
                return;
            }

            var currentIds = GetRenderSessions()
                .Select(session => session.Id)
                .ToHashSet(StringComparer.Ordinal);

            if (_sessionIds is null)
            {
                _sessionIds = currentIds;
                return;
            }

            if (_sessionIds.SetEquals(currentIds))
            {
                return;
            }

            _sessionIds = currentIds;
        }

        SessionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static (string Name, string? IconPath) GetProcessMetadata(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var executablePath = process.MainModule?.FileName;
            var iconPath = string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath)
                ? null
                : $"{executablePath},0";

            return (process.ProcessName, iconPath);
        }
        catch (ArgumentException)
        {
            return ($"Process {processId}", null);
        }
        catch (InvalidOperationException)
        {
            return ($"Process {processId}", null);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return ($"Process {processId}", null);
        }
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

}

internal enum EDataFlow
{
    Render,
    Capture,
    All,
}

internal enum ERole
{
    Console,
    Multimedia,
    Communications,
}

[Flags]
internal enum CLSCTX : uint
{
    InprocServer = 0x1,
    InprocHandler = 0x2,
    LocalServer = 0x4,
    All = InprocServer | InprocHandler | LocalServer,
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumerator
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(EDataFlow dataFlow, uint stateMask, out object devices);
    void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    void RegisterEndpointNotificationCallback(IntPtr client);
    void UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    void Activate([In] Guid iid, CLSCTX context, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    void OpenPropertyStore(uint access, out object properties);
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetState(out uint state);
}

[ComImport]
[Guid("BFA971F1-4D5E-40BB-935E-967039BFBEE4")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionManager2
{
    void GetAudioSessionControl(ref Guid audioSessionGuid, uint streamFlags, out IAudioSessionControl sessionControl);
    void GetSimpleAudioVolume(ref Guid audioSessionGuid, uint streamFlags, out ISimpleAudioVolume audioVolume);
    void GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
    void RegisterSessionNotification(IntPtr client);
    void UnregisterSessionNotification(IntPtr client);
    void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr client);
    void UnregisterDuckNotification(IntPtr client);
}

[ComImport]
[Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionEnumerator
{
    void GetCount(out int count);
    void GetSession(int sessionCount, out IAudioSessionControl session);
}

[ComImport]
[Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl
{
    void GetState(out int state);
    void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
    void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
    void GetGroupingParam(out Guid groupingParam);
    void SetGroupingParam(ref Guid groupingParam, ref Guid eventContext);
    void RegisterAudioSessionNotification(IntPtr client);
    void UnregisterAudioSessionNotification(IntPtr client);
}

[ComImport]
[Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioSessionControl2
{
    void GetState(out int state);
    void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string displayName);
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string displayName, ref Guid eventContext);
    void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string iconPath);
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string iconPath, ref Guid eventContext);
    void GetGroupingParam(out Guid groupingParam);
    void SetGroupingParam(ref Guid groupingParam, ref Guid eventContext);
    void RegisterAudioSessionNotification(IntPtr client);
    void UnregisterAudioSessionNotification(IntPtr client);
    void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionIdentifier);
    void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string sessionInstanceIdentifier);
    void GetProcessId(out uint processId);
    void IsSystemSoundsSession();
    void SetDuckingPreference(bool optOut);
}

[ComImport]
[Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ISimpleAudioVolume
{
    void SetMasterVolume(float level, Guid eventContext);
    void GetMasterVolume(out float level);
    void SetMute(bool mute, Guid eventContext);
    void GetMute(out bool mute);
}
