using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI;
using Fika.Core.Main.Utils;
using HarmonyLib;
using NekoParty.GroupEX.State;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch]
internal static class NekoPartyNativeInviteWindowPatch
{
	[HarmonyTargetMethod]
	private static MethodBase TargetMethod()
	{
		List<MethodInfo> list = (from m in typeof(ItemUiContext).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			where m.Name == "ShowInviteInRaidWindow"
			select m).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return list.FirstOrDefault((MethodInfo m) => m.GetParameters().Length == 4) ?? list[0];
	}

	[HarmonyPrefix]
	private static bool Prefix()
	{
		try
		{
			NekoGroupState.PurgeExpiredRaidInvites(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
			if (NekoGroupState.PendingRaidInvites.Count > 0 || NekoGroupState.PendingAcceptedRaidInvite != null)
			{
				return true;
			}
		}
		catch
		{
			return true;
		}
		FikaGlobals.LogWarning("[NekoP2P][Diag] 已屏蔽原生战局邀请窗（当前没有待处理的 NekoParty 战局邀请，来源=队长就绪推送）", "RaidInviteDiag");
		return false;
	}
}
