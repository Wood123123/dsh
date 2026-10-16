using Diz.Binding;
using EFT;
using EFT.UI.Matchmaker;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyGroupMaxCountPatch
{
	internal static void Postfix(MatchmakerPlayersController __instance)
	{
		if (NekoGroupExConfig.UseNativePartyInvite && ((BaseMatchmakerController<RaidSettings>)(object)__instance)?.MaxGroupCount != null)
		{
			((BaseBindable<int>)(object)((BaseMatchmakerController<RaidSettings>)(object)__instance).MaxGroupCount).Value = 99;
		}
	}
}
