using Comfort.Common;
using EFT;
using EFT.UI;
using Fika.Core.Main.Utils;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.LiteNetLib.Utils;
using NekoParty.GroupEX.CrossRaid;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyCrossRaidRejectNoticePatch
{
	internal static void Prefix(DisconnectInfo disconnectInfo)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Invalid comparison between Unknown and I4
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		if ((int)disconnectInfo.Reason != 6 || !NekoCrossTransitRuntime.ShouldUseCrossTransit())
		{
			return;
		}
		string text = "";
		try
		{
			if (disconnectInfo.AdditionalData != null && ((NetDataReader)disconnectInfo.AdditionalData).AvailableBytes > 0)
			{
				text = ((NetDataReader)disconnectInfo.AdditionalData).PeekString();
			}
		}
		catch
		{
		}
		FikaGlobals.LogError("[NekoCrossRaid] 主机拒绝连接：" + (string.IsNullOrEmpty(text) ? "(无理由)" : text), "Prefix");
		Singleton<PreloaderUI>.Instance.ShowCriticalErrorScreen(LocalizationExtensions.Localized("F_UI_ErrorConnecting", (string)null), "主机拒绝连接：战局可能刚刚开始。请取消加载退出到主菜单，等本局结束后再组队进入。", (EButtonType)0, 15f);
	}
}
