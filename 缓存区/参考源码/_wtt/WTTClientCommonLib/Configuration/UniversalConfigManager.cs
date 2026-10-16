using BepInEx.Configuration;
using WTTClientCommonLib.Attributes;

namespace WTTClientCommonLib.Configuration;

public static class UniversalConfigManager
{
	internal static ConfigEntry<bool> DeveloperMode { get; private set; }

	public static void Initialize(ConfigFile config)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		DeveloperMode = config.Bind<bool>("General", "Developer Mode", false, new ConfigDescription("Enable Developer Mode and RESTART YOUR GAME to access zone editor and static spawn system tools", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 0
			}
		}));
	}
}
