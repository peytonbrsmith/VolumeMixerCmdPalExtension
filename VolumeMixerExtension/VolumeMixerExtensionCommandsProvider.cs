// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace VolumeMixerExtension;

public partial class VolumeMixerExtensionCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem _dockBand;
    private readonly SettingsManager _settingsManager = new();
    private readonly VolumeMixerExtensionPage _mixerPage = new();
    private readonly VolumeMixerDockPage _dockPage;

    public VolumeMixerExtensionCommandsProvider()
    {
        DisplayName = "Volume Mixer";
        Icon = IconHelpers.FromRelativePath("Assets\\VolumeMixerIcon.png");
        Id = "com.microsoft.powertoys.volume-mixer";
        _commands = [
            new CommandItem(_mixerPage)
            {
                Title = DisplayName,
                MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
            },
        ];
        _dockPage = new VolumeMixerDockPage(_mixerPage);
        _dockBand = new CommandItem(_dockPage)
        {
            Title = DisplayName,
            Icon = Icon,
        };

        Settings = _settingsManager.Settings;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override ICommandItem[]? GetDockBands() => [_dockBand];
}
