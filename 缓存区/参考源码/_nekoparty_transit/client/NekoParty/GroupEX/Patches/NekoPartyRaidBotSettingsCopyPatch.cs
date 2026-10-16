using EFT;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyRaidBotSettingsCopyPatch
{
	internal static void Prefix(RaidSettings ___raidSettings_0, RaidSettings ___raidSettings_1)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		if (___raidSettings_0 != null && ___raidSettings_1 != null)
		{
			___raidSettings_0.BotSettings = ___raidSettings_1.BotSettings;
		}
	}
}
