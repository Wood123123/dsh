using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FikaServer.Controllers;
using FikaServer.Helpers;
using FikaServer.Models.Fika.Routes.Raid.Create;
using FikaServer.Services.Headless;
using FikaServer.WebSockets;
using Microsoft.Extensions.DependencyInjection;
using NekoParty.Server.Core.Models;
using NekoParty.Server.GroupEX.Models;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;

namespace NekoParty.Server.GroupEX.Patches;

public class RaidCreatePatch : AbstractPatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(RaidController).GetMethod("HandleRaidCreate");
	}

	[PatchPostfix]
	public static void Postfix(Task<FikaRaidCreateResponse> __result, FikaRaidCreateRequestData request, MongoId sessionId)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		HandlePostfixAsync(__result, request, sessionId);
	}

	private unsafe static async Task HandlePostfixAsync(Task<FikaRaidCreateResponse> resultTask, FikaRaidCreateRequestData request, MongoId sessionId)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		FikaRaidCreateResponse val = await resultTask;
		if (val == (FikaRaidCreateResponse)null || !val.Success)
		{
			return;
		}
		NekoGroupService service = ServiceLocator.ServiceProvider.GetService<NekoGroupService>();
		NotificationWebSocket webSocket = ServiceLocator.ServiceProvider.GetService<NotificationWebSocket>();
		if (service == null || webSocket == null)
		{
			return;
		}
		GetRaidConfigurationRequestData settings = request.Settings;
		if (((settings != (GetRaidConfigurationRequestData)null) ? ((RaidSettings)settings).Location : null) == null)
		{
			return;
		}
		string text = ((object)request.ServerId).ToString();
		ISptLogger<RaidCreatePatch> patchLogger = ServiceLocator.ServiceProvider.GetService<ISptLogger<RaidCreatePatch>>();
		string text2 = ((object)(*(MongoId*)(&sessionId))/*cast due to constrained. prefix*/).ToString();
		string text3 = null;
		string text4 = null;
		try
		{
			MongoId val2 = (MongoId)(object)request.ServerId;
			HeadlessHelper service2 = ServiceLocator.ServiceProvider.GetService<HeadlessHelper>();
			if (service2 != null && service2.IsHeadlessClient(val2))
			{
				text3 = service2.GetRequesterUsername(val2);
				HeadlessService service3 = ServiceLocator.ServiceProvider.GetService<HeadlessService>();
				IEnumerable enumerable = ((service3 != null) ? service3.HeadlessClients : null);
				if (enumerable != null)
				{
					string text5 = ((object)(*(MongoId*)(&val2))/*cast due to constrained. prefix*/).ToString();
					foreach (object item in enumerable)
					{
						object obj = item?.GetType().GetProperty("Key")?.GetValue(item);
						object obj2 = item?.GetType().GetProperty("Value")?.GetValue(item);
						if (!(obj?.ToString() != text5))
						{
							if (obj2 != null)
							{
								text4 = (obj2.GetType().GetProperty("RequesterSessionID")?.GetValue(obj2) ?? obj2.GetType().GetField("RequesterSessionID")?.GetValue(obj2))?.ToString();
							}
							break;
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			patchLogger?.Error("[NekoRaidCreate] 解析 headless requester 失败：" + ex.Message, (Exception)null);
		}
		if (service.GetPartyByPlayer(sessionId) == (NekoPartyData)null)
		{
			if (!string.IsNullOrWhiteSpace(text4) && service.GetPartyByPlayer(new MongoId(text4)) != (NekoPartyData)null)
			{
				text2 = text4;
			}
			else if (!string.IsNullOrWhiteSpace(text3))
			{
				text2 = service.TryResolveLeaderProfileIdByNickname(text3) ?? text2;
			}
		}
		service.SyncPreferredSide(sessionId, (int)request.Side);
		List<NekoRaidInviteData> list = service.CreateRaidInvitesForLeader(text2, text, request.RaidCode, ((RaidSettings)request.Settings).Location, (int)request.Side, (int)request.Time);
		patchLogger?.Info("[NekoRaidCreate] HandleRaidCreate 完成 session=" + MongoId.op_Implicit(sessionId) + " serverId=" + text + " requester=" + text3 + " requesterSession=" + text4 + " 解析队长=" + text2 + " 队伍表=" + service.DebugPartyTable() + " raidCode=" + request.RaidCode + " loc=" + ((RaidSettings)request.Settings).Location + " side=" + (int)request.Side + " time=" + (int)request.Time + " 生成战局邀请数=" + (list?.Count ?? (-1)), (Exception)null);
		foreach (NekoRaidInviteData item2 in list)
		{
			patchLogger?.Info("[NekoRaidCreate] 推送战局邀请(静默触发用) -> target=" + item2.TargetNickname + "/" + item2.TargetProfileId + " serverId=" + item2.ServerId + " raidCode=" + item2.RaidCode + " loc=" + item2.Location, (Exception)null);
			await webSocket.SendAsync<NekoRaidInviteReceivedNotification>(new MongoId(item2.TargetProfileId), new NekoRaidInviteReceivedNotification
			{
				InviteId = item2.InviteId,
				PartyId = item2.PartyId,
				LeaderProfileId = item2.LeaderProfileId,
				LeaderNickname = item2.LeaderNickname,
				TargetProfileId = item2.TargetProfileId,
				TargetNickname = item2.TargetNickname,
				ServerId = item2.ServerId,
				RaidCode = item2.RaidCode,
				Location = item2.Location,
				Side = item2.Side,
				TargetSide = item2.TargetSide,
				Time = item2.Time,
				ExpiresAtUnix = item2.ExpiresAtUnix
			});
		}
	}

	public RaidCreatePatch()
		: base((string)null)
	{
	}
}
