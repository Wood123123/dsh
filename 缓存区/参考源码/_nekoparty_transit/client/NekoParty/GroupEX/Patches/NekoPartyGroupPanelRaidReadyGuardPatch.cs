using System;
using EFT;
using EFT.Communications;
using Fika.Core.Main.Utils;
using NekoParty.GroupEX.MixedSide;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.Networking;
using NekoParty.GroupEX.State;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyGroupPanelRaidReadyGuardPatch
{
	internal static bool Prefix()
	{
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		try
		{
			Profile profile = FikaBackendUtils.Profile;
			if (string.IsNullOrEmpty((profile != null) ? profile.ProfileId : null))
			{
				return true;
			}
			NekoPartyData? nekoPartyData = NekoGroupState.CurrentParty;
			try
			{
				NekoPartyOperationResponse currentParty = NekoGroupRequestHandler.GetCurrentParty();
				if (currentParty.Success && currentParty.Party.HasValue)
				{
					nekoPartyData = currentParty.Party;
					NekoGroupState.CurrentParty = currentParty.Party;
				}
			}
			catch (Exception ex)
			{
				FikaGlobals.LogWarning("[NekoParty] 加入战局守卫：实时拉取队伍失败，改用缓存判断：" + ex.Message, "Prefix");
			}
			if (!nekoPartyData.HasValue)
			{
				return true;
			}
			if (NekoPartyMixedSideRuntime.IsLocalProfileId(nekoPartyData.Value.LeaderProfileId))
			{
				return true;
			}
			if (string.IsNullOrWhiteSpace(nekoPartyData.Value.Location))
			{
				NotificationManager.DisplayWarningNotification("队长尚未准备就绪，请等待队长准备完成后再加入战局", (ENotificationDurationType)0);
				return false;
			}
			return true;
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty] 加入战局守卫异常，放行原生行为：{arg}", "Prefix");
			return true;
		}
	}
}
