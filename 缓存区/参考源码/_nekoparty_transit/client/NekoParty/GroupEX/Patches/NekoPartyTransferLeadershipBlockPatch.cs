using System;
using EFT.UI;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyTransferLeadershipBlockPatch
{
	internal static bool Prefix()
	{
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		PreloaderUI instance = MonoBehaviourSingleton<PreloaderUI>.Instance;
		if ((Object)(object)instance != (Object)null)
		{
			instance.ShowErrorScreen("无法转交队长", "功能尚未实现", (Action)null);
		}
		return false;
	}
}
