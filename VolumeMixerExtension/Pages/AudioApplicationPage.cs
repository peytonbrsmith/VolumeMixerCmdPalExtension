using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using VolumeMixerExtension.Audio;

namespace VolumeMixerExtension;

internal sealed partial class AudioApplicationPage : ListPage
{
    private AudioSessionInfo _session;
    private readonly VolumeMixerExtensionPage _parentPage;

    public AudioApplicationPage(AudioSessionInfo session, VolumeMixerExtensionPage parentPage)
    {
        _session = session;
        _parentPage = parentPage;
        Icon = string.IsNullOrWhiteSpace(session.IconPath)
            ? IconHelpers.FromRelativePath("Assets\\VolumeMixerIcon.png")
            : new IconInfo(session.IconPath);
        Title = session.DisplayName;
        Name = "Adjust volume";
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>
        {
            new ListItem(new ToggleMuteCommand(_session, this))
            {
                Title = _session.IsMuted ? "Unmute" : "Mute",
                Subtitle = _session.IsMuted ? "Enable audio for this application" : "Silence this application",
            },
        };

        for (var percentage = 100; percentage >= 10; percentage -= 10)
        {
            items.Add(new ListItem(new SetVolumeCommand(_session, percentage, this))
            {
                Title = $"Set volume to {percentage}%",
                Subtitle = percentage == (int)Math.Round(_session.Volume * 100)
                    ? "Current volume"
                    : "",
            });
        }

        return items.ToArray();
    }

    public void Refresh(AudioSessionInfo session)
    {
        _session = session;
        RaiseItemsChanged();
        _parentPage.RefreshItems();
    }
}

internal sealed partial class SetVolumeCommand : InvokableCommand
{
    private readonly AudioSessionInfo _session;
    private readonly AudioSessionService _audio = new();
    private readonly int _percentage;
    private readonly AudioApplicationPage _page;

    public SetVolumeCommand(AudioSessionInfo session, int percentage, AudioApplicationPage page)
    {
        _session = session;
        _percentage = percentage;
        _page = page;
    }

    public override string Name => $"Set volume to {_percentage}%";

    public override CommandResult Invoke()
    {
        if (!AudioSessionService.TrySetVolume(_session.Id, _percentage / 100f, _session.IsMuted))
        {
            return CommandResult.ShowToast($"{_session.DisplayName} is no longer available.");
        }

        RefreshSession();
        return CommandResult.KeepOpen();
    }

    private void RefreshSession()
    {
        var session = AudioSessionService.GetRenderSessions().FirstOrDefault(session => session.Id == _session.Id);
        if (session is not null)
        {
            _page.Refresh(session);
        }
    }
}

internal sealed partial class ToggleMuteCommand : InvokableCommand
{
    private readonly AudioSessionInfo _session;
    private readonly AudioSessionService _audio = new();
    private readonly AudioApplicationPage _page;

    public ToggleMuteCommand(AudioSessionInfo session, AudioApplicationPage page)
    {
        _session = session;
        _page = page;
    }

    public override string Name => _session.IsMuted ? "Unmute" : "Mute";

    public override CommandResult Invoke()
    {
        if (!AudioSessionService.TrySetVolume(_session.Id, _session.Volume, !_session.IsMuted))
        {
            return CommandResult.ShowToast($"{_session.DisplayName} is no longer available.");
        }

        var session = AudioSessionService.GetRenderSessions().FirstOrDefault(session => session.Id == _session.Id);
        if (session is not null)
        {
            _page.Refresh(session);
        }

        return CommandResult.KeepOpen();
    }
}
