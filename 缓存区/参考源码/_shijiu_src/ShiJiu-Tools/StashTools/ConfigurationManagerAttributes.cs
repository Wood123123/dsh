using System;
using BepInEx.Configuration;

namespace StashTools;

public sealed class ConfigurationManagerAttributes
{
	public delegate void HotkeyDrawer(ConfigEntryBase entry, ref bool accepting);

	public HotkeyDrawer? CustomHotkeyDrawer;

	public Action<ConfigEntryBase>? CustomDrawer;
}
