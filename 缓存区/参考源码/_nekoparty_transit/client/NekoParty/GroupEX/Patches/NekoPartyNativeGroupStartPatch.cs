using System.Threading.Tasks;
using EFT;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyNativeGroupStartPatch
{
	internal static bool Prefix(TarkovApplication __instance, ref Task __result)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		__result = __instance.LocalGameMatching(__instance.CurrentRaidSettings.TimeAndWeatherSettings, false);
		return false;
	}
}
