// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using VolumeMixerExtension.Audio;

namespace VolumeMixerExtension;

internal sealed partial class VolumeMixerExtensionPage : ListPage
{
    private readonly AudioSessionService _audio = new();

    public event EventHandler? ItemsRefreshed;

    public VolumeMixerExtensionPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\VolumeMixerIcon.png");
        Title = "Volume Mixer";
        Name = "Open";
        _audio.SessionsChanged += OnSessionsChanged;
    }

    public override IListItem[] GetItems()
    {
        var sessions = AudioSessionService.GetRenderSessions();
        if (sessions.Count == 0)
        {
            return [new ListItem(new RefreshAudioSessionsCommand(this))
            {
                Title = "No active audio sessions",
                Subtitle = "Click to refresh active audio sessions.",
                Icon = IconHelpers.FromRelativePath("Assets\\Refresh.png"),
            }];
        }

        var listItems = new List<ListItem>();

        foreach (var session in sessions)
        {
            listItems.Add(new ListItem(new AudioApplicationPage(session, this))
            {
                Title = session.DisplayName,
                //Subtitle = $"{session.ProcessName} · {Math.Round(session.Volume * 100)}%{(session.IsMuted ? " · Muted" : string.Empty)}",
                Subtitle = $"{Math.Round(session.Volume * 100)}%{(session.IsMuted ? " · Muted" : string.Empty)}",
                Icon = GetSessionIcon(session),
            });
        }

        return [.. listItems];
    }

    public IListItem[] GetSessionItems()
    {
        var sessions = AudioSessionService.GetRenderSessions();
        var items = new List<IListItem>();

        foreach (var session in sessions)
        {
            items.Add(new ListItem(new AudioApplicationPage(session, this))
            {
                Title = session.DisplayName,
                Subtitle = $"{session.ProcessName} · {Math.Round(session.Volume * 100)}%{(session.IsMuted ? " · Muted" : string.Empty)}",
                Icon = GetSessionIcon(session),
            });
        }

        return [.. items];
    }

    public void RefreshItems()
    {
        RaiseItemsChanged();
        ItemsRefreshed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionsChanged(object? sender, EventArgs e)
    {
        RefreshItems();
    }

    private static IconInfo GetSessionIcon(AudioSessionInfo session)
    {
        return string.IsNullOrWhiteSpace(session.IconPath)
            ? IconHelpers.FromRelativePath("Assets\\VolumeMixerIcon.png")
            : new IconInfo(session.IconPath);
    }
}

internal sealed partial class RefreshAudioSessionsCommand : InvokableCommand
{
    private readonly VolumeMixerExtensionPage _page;
    private readonly VolumeMixerDockPage? _dockPage;

    public RefreshAudioSessionsCommand(VolumeMixerExtensionPage page, VolumeMixerDockPage? dockPage = null)
    {
        _page = page;
        _dockPage = dockPage;
    }

    public override string Name => "Refresh audio sessions";

    public override CommandResult Invoke()
    {
        _page.RefreshItems();
        _dockPage?.RefreshItems();
        return CommandResult.KeepOpen();
    }
}
