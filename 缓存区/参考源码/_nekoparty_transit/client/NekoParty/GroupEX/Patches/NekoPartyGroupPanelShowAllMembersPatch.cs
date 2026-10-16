using System;
using System.Reflection;
using Diz.Binding;
using EFT;
using EFT.UI;
using EFT.UI.Matchmaker;
using Fika.Core.Main.Utils;
using HarmonyLib;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyGroupPanelShowAllMembersPatch
{
	private static readonly FieldInfo MirrorListField = AccessTools.Field(typeof(BaseGroupPanel), "_visibleGroupPlayers");

	internal static bool Prefix(BaseGroupPanel __instance, RaidPlayer raidPlayer)
	{
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		try
		{
			BindableList<RaidPlayer> val = ((MirrorListField != null) ? ((BindableList<RaidPlayer>)MirrorListField.GetValue(__instance)) : null);
			if (val == null || raidPlayer == null)
			{
				return true;
			}
			foreach (RaidPlayer item in val)
			{
				if (item != null && ((GroupPlayer)item).AccountId == ((GroupPlayer)raidPlayer).AccountId)
				{
					return false;
				}
			}
			val.Add(raidPlayer);
			return false;
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty] 小队面板显示全员失败，回退原版（仍受 5 人显示限制）：{arg}", "Prefix");
			return true;
		}
	}
}
