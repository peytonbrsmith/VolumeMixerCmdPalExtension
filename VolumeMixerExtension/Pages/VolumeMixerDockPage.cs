using System.Collections.Generic;
using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VolumeMixerExtension;

internal sealed partial class VolumeMixerDockPage : ListPage
{
    private readonly VolumeMixerExtensionPage _mixerPage;

    public VolumeMixerDockPage(VolumeMixerExtensionPage mixerPage)
    {
        _mixerPage = mixerPage;
        _mixerPage.ItemsRefreshed += OnMixerItemsRefreshed;
        Title = "Volume Mixer";
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        var sessionItems = _mixerPage.GetSessionItems();

        var items = new List<IListItem>
        {
            new ListItem(_mixerPage)
            {
                Title = "Mixer",
                Icon = IconHelpers.FromRelativePath("Assets\\VolumeMixerIcon.png"),
            },
        };

        items.AddRange(sessionItems);

        return [.. items];
    }

    public void RefreshItems()
    {
        RaiseItemsChanged();
    }

    private void OnMixerItemsRefreshed(object? sender, EventArgs e)
    {
        RaiseItemsChanged();
    }
}
