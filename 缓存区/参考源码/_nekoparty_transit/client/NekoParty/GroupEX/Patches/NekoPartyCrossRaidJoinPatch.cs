using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Communications;
using EFT.UI;
using Fika.Core.Main.Utils;
using Fika.Core.Networking;
using Fika.Core.Networking.Http;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Models;
using LiteNetLib;
using NekoParty.GroupEX.CrossRaid;
using NekoParty.GroupEX.State;
using UnityEngine;
using UnityEngine.UI;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyCrossRaidJoinPatch
{
	internal static bool Prefix(string profileId, string serverId, Button button, bool reconnect, ref Task<bool> __result)
	{
		if (!NekoCrossTransitRuntime.ShouldUseCrossTransit())
		{
			return true;
		}
		__result = JoinWithRelayFallback(profileId, serverId, button, reconnect);
		return false;
	}

	private static async Task<bool> JoinWithRelayFallback(string profileId, string serverId, Button button, bool reconnect)
	{
		try
		{
			return await JoinWithRelayFallbackCore(profileId, serverId, button, reconnect);
		}
		catch (Exception ex)
		{
			FikaGlobals.LogError($"[NekoCrossRaid] 加入战局异常: {ex}", "JoinWithRelayFallback");
			Singleton<PreloaderUI>.Instance.ShowErrorScreen("ERROR JOINING", ex.Message, (Action)null);
			if ((Object)button != (Object)null)
			{
				((Behaviour)button).enabled = true;
			}
			return false;
		}
	}

	private unsafe static async Task<bool> JoinWithRelayFallbackCore(string profileId, string serverId, Button button, bool reconnect)
	{
		NekoCrossTransitRuntime.ApplyBeaconIfNeeded();
		if ((Object)button != (Object)null)
		{
			((Behaviour)button).enabled = false;
		}
		SetBackendProperty("IsReconnect", reconnect);
		NotificationManager.DisplayMessageNotification(LocalizationExtensions.Localized("F_Client_ConnectingToSession", (string)null), (ENotificationDurationType)0, (ENotificationIconType)7, (Color?)null);
		FikaGlobals.LogInfo($"[NekoCrossRaid] 开始加入 serverId={serverId} reconnect={reconnect}", "JoinWithRelayFallbackCore");
		FikaPingingClient pingingClient = await CreatePingingClientReadyAsync(serverId);
		try
		{
			if (pingingClient == null)
			{
				FikaGlobals.LogError("[NekoCrossRaid] gethost 始终无有效 IP/端口（主机 sethost 未送达），放弃加入。", "JoinWithRelayFallbackCore");
				Singleton<PreloaderUI>.Instance.ShowCriticalErrorScreen(LocalizationExtensions.Localized("F_UI_ErrorConnecting", (string)null), "队长战局尚未就绪（主机还没上报连接地址）。请等队长完全进入等待界面后，稍等几秒再重试加入。", (EButtonType)0, 15f);
				if ((Object)button != (Object)null)
				{
					((Behaviour)button).enabled = true;
				}
				return false;
			}
			string knockMessage = FikaBackendUtils.ServerGuid.ToString();
			bool relayAdded = false;
			if (NekoPartyP2PProbe.SelfDirectFailed && NekoCrossTransitRuntime.TryAllocateRelay(FikaBackendUtils.ServerGuid.ToString(), out var host, out var port))
			{
				FikaGlobals.LogInfo("[NekoCrossRaid] 预打洞显示直连不通，首轮敲门同时携带中继端点。", "JoinWithRelayFallbackCore");
				AddPingEndpoint(pingingClient, new IPEndPoint(IPAddress.Parse(host), port));
				relayAdded = true;
			}
			bool flag;
			if (reconnect)
			{
				flag = await ProbeHostNotStartedAsync(pingingClient, knockMessage);
				if (flag)
				{
					reconnect = false;
					SetBackendProperty("IsReconnect", false);
					FikaGlobals.LogWarning("[NekoCrossRaid] 检测到重连标记但主机未开局（影子残留幽灵重连），已改按全新加入。", "JoinWithRelayFallbackCore");
				}
				else if (pingingClient.InProgress)
				{
					if (!IsGenuineReconnect(profileId, serverId))
					{
						ShowPhantomReconnectBlocked(button);
						return false;
					}
					FikaGlobals.LogInfo("[NekoCrossRaid] 主机已开局且本进程有该局连接记录，按真重连流程敲门。", "JoinWithRelayFallbackCore");
					flag = await pingingClient.AttemptToPingHost(knockMessage, true, default(CancellationToken));
				}
			}
			else
			{
				flag = await pingingClient.AttemptToPingHost(knockMessage, false, default(CancellationToken));
			}
			FikaGlobals.LogInfo($"[NekoCrossRaid] 敲门结果 success={flag} rejected={pingingClient.Rejected} inProgress={pingingClient.InProgress} reconnect={reconnect}", "JoinWithRelayFallbackCore");
			if (!flag && !pingingClient.Rejected && !pingingClient.InProgress && !relayAdded && NekoCrossTransitRuntime.TryAllocateRelay(FikaBackendUtils.ServerGuid.ToString(), out var host2, out var port2))
			{
				FikaGlobals.LogInfo("[NekoCrossRaid] 直连失败，经中继重试...", "JoinWithRelayFallbackCore");
				AddPingEndpoint(pingingClient, new IPEndPoint(IPAddress.Parse(host2), port2));
				if (reconnect)
				{
					flag = await ProbeHostNotStartedAsync(pingingClient, knockMessage);
					if (flag)
					{
						reconnect = false;
						SetBackendProperty("IsReconnect", false);
						FikaGlobals.LogWarning("[NekoCrossRaid] （中继）检测到重连标记但主机未开局，已改按全新加入。", "JoinWithRelayFallbackCore");
					}
					else if (pingingClient.InProgress)
					{
						if (!IsGenuineReconnect(profileId, serverId))
						{
							ShowPhantomReconnectBlocked(button);
							return false;
						}
						flag = await pingingClient.AttemptToPingHost(knockMessage, true, default(CancellationToken));
					}
				}
				else
				{
					flag = await pingingClient.AttemptToPingHost(knockMessage, false, default(CancellationToken));
				}
				FikaGlobals.LogInfo($"[NekoCrossRaid] 中继敲门结果 success={flag}", "JoinWithRelayFallbackCore");
			}
			if (pingingClient.InProgress)
			{
				FikaGlobals.LogWarning("[NekoCrossRaid] 队长战局已开始（fika.inprogress），无法加入。请让队长在等待界面等队员连入后再点开始。", "JoinWithRelayFallbackCore");
			}
			if (!flag)
			{
				Singleton<PreloaderUI>.Instance.ShowCriticalErrorScreen(LocalizationExtensions.Localized("F_UI_ErrorConnecting", (string)null), LocalizationExtensions.Localized("F_UI_UnableToConnect", (string)null), (EButtonType)0, 10f);
				if ((Object)button != (Object)null)
				{
					((Behaviour)button).enabled = true;
				}
				return false;
			}
			CreateMatch val = default(CreateMatch);
			string text = null;
			if (FikaBackendUtils.JoinMatch(profileId, serverId, ref val, ref text))
			{
				FikaGlobals.LogInfo($"[NekoCrossRaid] raid.join 成功 groupId={val.ServerId} raidCode={val.RaidCode}", "JoinWithRelayFallbackCore");
				SetBackendProperty("GroupId", ((object)(*(MongoID*)(&val.ServerId))/*cast due to constrained. prefix*/).ToString());
				SetBackendProperty("ClientType", (object)(EClientType)1);
				if (!reconnect)
				{
					FikaRequestHandler.UpdateAddPlayer(new AddPlayerRequest(FikaBackendUtils.GroupId, profileId, FikaBackendUtils.IsSpectator));
				}
				FikaGlobals.LogInfo("[NekoCrossRaid] " + (reconnect ? "重连（跳过 addplayer）" : "addplayer 已上报") + "，交回原生 Accept 流程进入战局。", "JoinWithRelayFallbackCore");
				return true;
			}
			FikaGlobals.LogError("[NekoCrossRaid] raid.join 失败: " + text, "JoinWithRelayFallbackCore");
			Singleton<PreloaderUI>.Instance.ShowErrorScreen("ERROR JOINING", text, (Action)null);
			return false;
		}
		finally
		{
			((IDisposable)pingingClient)?.Dispose();
		}
	}

	private static async Task<FikaPingingClient> CreatePingingClientReadyAsync(string serverId)
	{
		for (int attempt = 1; attempt <= 5; attempt++)
		{
			FikaPingingClient val = await NetManagerUtils.CreatePingingClient();
			try
			{
				if (val.Init(serverId))
				{
					return val;
				}
				FikaGlobals.LogWarning($"[NekoCrossRaid] gethost 端点无效（{attempt}/{5}），等待主机上报后重试...", "CreatePingingClientReadyAsync");
			}
			catch (IndexOutOfRangeException)
			{
				FikaGlobals.LogWarning($"[NekoCrossRaid] 主机尚未上报 IP，gethost 返回空列表（{attempt}/{5}），等待重试...", "CreatePingingClientReadyAsync");
			}
			val.Dispose();
			if (attempt < 5)
			{
				await Task.Delay(2000);
			}
		}
		return null;
	}

	private static bool IsGenuineReconnect(string profileId, string serverId)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		CreateMatch val = default(CreateMatch);
		string text = null;
		if (!FikaBackendUtils.JoinMatch(profileId, serverId, ref val, ref text))
		{
			return false;
		}
		return NekoGroupState.ConnectedRaidKeys.Contains(serverId + ":" + val.RaidCode);
	}

	private static void ShowPhantomReconnectBlocked(Button button)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		FikaGlobals.LogWarning("[NekoCrossRaid] 已开局且本进程无该局连接记录（影子残留伪重连），已拒绝冒充重连。", "ShowPhantomReconnectBlocked");
		Singleton<PreloaderUI>.Instance.ShowCriticalErrorScreen(LocalizationExtensions.Localized("F_UI_ErrorConnecting", (string)null), "队长战局已开始，无法中途加入；请等本局结束后再一起进入下一局。\n（跨服战局暂不支持重启游戏后重连回局内。）", (EButtonType)0, 15f);
		if ((Object)button != (Object)null)
		{
			((Behaviour)button).enabled = true;
		}
	}

	private static async Task<bool> ProbeHostNotStartedAsync(FikaPingingClient client, string knockMessage)
	{
		int attempts = 0;
		do
		{
			attempts++;
			client.PingEndPoint(knockMessage, false);
			if (client.NetClient != null)
			{
				((LiteNetManager)client.NetClient).PollEvents();
				NatPunchModule natPunchModule = ((LiteNetManager)client.NetClient).NatPunchModule;
				if (natPunchModule != null)
				{
					natPunchModule.PollEvents();
				}
			}
			await Task.Delay(250);
		}
		while (!client.Rejected && !client.Received && !client.InProgress && attempts < 50);
		return client.Received;
	}

	private static void SetBackendProperty(string propertyName, object value)
	{
		PropertyInfo property = typeof(FikaBackendUtils).GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
		if (property == null)
		{
			FikaGlobals.LogError("[NekoCrossRaid] FikaBackendUtils." + propertyName + " 属性不存在，无法赋值。", "SetBackendProperty");
		}
		else
		{
			property.SetValue(null, value);
		}
	}

	private static void AddPingEndpoint(FikaPingingClient client, IPEndPoint ep)
	{
		if (!(typeof(FikaPingingClient).GetField("_endPoints", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(client) is List<IPEndPoint> list))
		{
			FikaGlobals.LogError("[NekoCrossRaid] 无法访问 FikaPingingClient._endPoints，放弃中继重试。", "AddPingEndpoint");
		}
		else if (!list.Contains(ep))
		{
			list.Add(ep);
		}
	}
}
