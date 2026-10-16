using System;
using System.Collections.Generic;
using System.Linq;
using NekoParty.Server.Core.Models;
using NekoParty.Server.Core.Services;
using NekoParty.Server.GroupEX.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Eft.Ws;
using SPTarkov.Server.Core.Models.Enums;

namespace NekoParty.Server.GroupEX.Services;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoNativeGroupNotifier(NotificationSendHelper notificationSendHelper, NekoProfileResolver profileResolver, ISptLogger<NekoNativeGroupNotifier> logger)
{
	public NekoGroupCharacterDto BuildCharacter(string profileId, bool isLeader, bool isReady, int preferredSide = 0)
	{
		NekoNativeGroupProfile nativeGroupProfile = profileResolver.GetNativeGroupProfile(profileId, preferredSide);
		PlayerVisualRepresentation nativeVisual = profileResolver.GetNativeVisual(profileId, preferredSide);
		int? num;
		if (nativeVisual == (PlayerVisualRepresentation)null)
		{
			num = null;
		}
		else
		{
			Equipment equipment = nativeVisual.Equipment;
			num = ((equipment == (Equipment)null) ? ((int?)null) : equipment.Items?.Count);
		}
		int? num2 = num;
		int valueOrDefault = num2.GetValueOrDefault();
		return new NekoGroupCharacterDto
		{
			Id = profileId,
			Aid = nativeGroupProfile.Aid.ToString(),
			IsLeader = isLeader,
			IsReady = isReady,
			Region = string.Empty,
			LookingGroup = false,
			Info = new NekoGroupInfoDto
			{
				Nickname = nativeGroupProfile.Nickname,
				Side = nativeGroupProfile.Side,
				Level = nativeGroupProfile.Level,
				PrestigeLevel = nativeGroupProfile.PrestigeLevel,
				MemberCategory = nativeGroupProfile.MemberCategory,
				SelectedMemberCategory = nativeGroupProfile.SelectedMemberCategory,
				GameVersion = nativeGroupProfile.GameVersion,
				HasCoopExtension = nativeGroupProfile.HasCoopExtension
			},
			PlayerVisualRepresentation = nativeVisual
		};
	}

	public void PushInviteSend(string targetProfileId, string leaderProfileId, string requestId, IReadOnlyList<NekoGroupCharacterDto> members)
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Expected O, but got Unknown
		try
		{
			NekoGroupCharacterDto nekoGroupCharacterDto = members.FirstOrDefault((NekoGroupCharacterDto member) => string.Equals(member.Id, leaderProfileId, StringComparison.Ordinal)) ?? ((members.Count > 0) ? members[0] : null);
			WsGroupMatchInviteSend wsGroupMatchInviteSend = new WsGroupMatchInviteSend();
			((WsNotificationEvent)wsGroupMatchInviteSend).EventType = (NotificationEventType)6;
			((WsNotificationEvent)wsGroupMatchInviteSend).EventIdentifier = new MongoId();
			wsGroupMatchInviteSend.RequestId = requestId;
			wsGroupMatchInviteSend.From = ParseAid(nekoGroupCharacterDto?.Aid);
			wsGroupMatchInviteSend.Members = members.Select(ToFrameworkCharacter).ToList();
			WsGroupMatchInviteSend wsGroupMatchInviteSend2 = wsGroupMatchInviteSend;
			notificationSendHelper.SendMessageAsync(new MongoId(targetProfileId), (WsNotificationEvent)wsGroupMatchInviteSend2);
			logger.Success($"[NekoNativeGroup] pushed groupMatchInviteSend -> target={targetProfileId} from={leaderProfileId} aid={nekoGroupCharacterDto?.Aid} req={requestId} members={members.Count}", (Exception)null);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoNativeGroup] push invite/send failed: " + ex.Message, (Exception)null);
		}
	}

	public void PushInviteAccept(string targetProfileId, NekoGroupCharacterDto newMember)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		try
		{
			WsGroupMatchInviteAccept wsGroupMatchInviteAccept = new WsGroupMatchInviteAccept();
			((WsNotificationEvent)wsGroupMatchInviteAccept).EventType = (NotificationEventType)3;
			((WsNotificationEvent)wsGroupMatchInviteAccept).EventIdentifier = new MongoId();
			wsGroupMatchInviteAccept.Id = newMember.Id;
			wsGroupMatchInviteAccept.Aid = ParseAid(newMember.Aid);
			wsGroupMatchInviteAccept.Info = ToFrameworkInfo(newMember.Info);
			wsGroupMatchInviteAccept.VisualRepresentation = newMember.PlayerVisualRepresentation;
			wsGroupMatchInviteAccept.IsLeader = newMember.IsLeader;
			wsGroupMatchInviteAccept.IsReady = newMember.IsReady;
			wsGroupMatchInviteAccept.Region = newMember.Region;
			wsGroupMatchInviteAccept.LookingGroup = newMember.LookingGroup;
			WsGroupMatchInviteAccept wsGroupMatchInviteAccept2 = wsGroupMatchInviteAccept;
			notificationSendHelper.SendMessageAsync(new MongoId(targetProfileId), (WsNotificationEvent)wsGroupMatchInviteAccept2);
			logger.Success($"[NekoNativeGroup] pushed groupMatchInviteAccept -> target={targetProfileId} newMember={newMember.Id} aid={newMember.Aid}", (Exception)null);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoNativeGroup] push accept failed: " + ex.Message, (Exception)null);
		}
	}

	public void SyncParty(IReadOnlyList<NekoGroupCharacterDto> members)
	{
		if (members.Count == 0)
		{
			return;
		}
		NekoGroupCharacterDto nekoGroupCharacterDto = members.FirstOrDefault((NekoGroupCharacterDto member) => member.IsLeader);
		foreach (NekoGroupCharacterDto member in members)
		{
			foreach (NekoGroupCharacterDto member2 in members)
			{
				if (!string.Equals(member2.Id, member.Id, StringComparison.Ordinal))
				{
					PushInviteAccept(member.Id, member2);
				}
			}
			if (nekoGroupCharacterDto != null)
			{
				PushLeaderChanged(member.Id, nekoGroupCharacterDto.Aid);
			}
		}
		logger.Success($"[NekoNativeGroup] SyncParty 完成 members={members.Count} leader={nekoGroupCharacterDto?.Id}", (Exception)null);
	}

	public void PushRaidReady(string targetProfileId, NekoGroupCharacterDto readyMember)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		try
		{
			NekoWsGroupMatchRaidReady nekoWsGroupMatchRaidReady = new NekoWsGroupMatchRaidReady();
			((WsNotificationEvent)nekoWsGroupMatchRaidReady).EventType = (NotificationEventType)14;
			((WsNotificationEvent)nekoWsGroupMatchRaidReady).EventIdentifier = new MongoId();
			nekoWsGroupMatchRaidReady.ExtendedProfile = readyMember;
			NekoWsGroupMatchRaidReady nekoWsGroupMatchRaidReady2 = nekoWsGroupMatchRaidReady;
			notificationSendHelper.SendMessageAsync(new MongoId(targetProfileId), (WsNotificationEvent)nekoWsGroupMatchRaidReady2);
			logger.Success($"[NekoNativeGroup] pushed groupMatchRaidReady -> target={targetProfileId} member={readyMember.Id} ready={readyMember.IsReady}", (Exception)null);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoNativeGroup] push raid-ready failed: " + ex.Message, (Exception)null);
		}
	}

	public void PushLeaderChanged(string targetProfileId, string? leaderAid)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected O, but got Unknown
		try
		{
			WsGroupMatchLeaderChanged wsGroupMatchLeaderChanged = new WsGroupMatchLeaderChanged();
			((WsNotificationEvent)wsGroupMatchLeaderChanged).EventType = (NotificationEventType)9;
			((WsNotificationEvent)wsGroupMatchLeaderChanged).EventIdentifier = new MongoId();
			wsGroupMatchLeaderChanged.Owner = ParseAid(leaderAid);
			WsGroupMatchLeaderChanged wsGroupMatchLeaderChanged2 = wsGroupMatchLeaderChanged;
			notificationSendHelper.SendMessageAsync(new MongoId(targetProfileId), (WsNotificationEvent)wsGroupMatchLeaderChanged2);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoNativeGroup] push leader-changed failed: " + ex.Message, (Exception)null);
		}
	}

	public void PushUserLeave(string targetProfileId, string leaverAid, string? leaverNickname)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		try
		{
			NekoWsGroupMatchUserLeave nekoWsGroupMatchUserLeave = new NekoWsGroupMatchUserLeave();
			((WsNotificationEvent)nekoWsGroupMatchUserLeave).EventType = (NotificationEventType)10;
			((WsNotificationEvent)nekoWsGroupMatchUserLeave).EventIdentifier = new MongoId();
			nekoWsGroupMatchUserLeave.Aid = leaverAid;
			nekoWsGroupMatchUserLeave.Nickname = leaverNickname ?? string.Empty;
			NekoWsGroupMatchUserLeave nekoWsGroupMatchUserLeave2 = nekoWsGroupMatchUserLeave;
			notificationSendHelper.SendMessageAsync(new MongoId(targetProfileId), (WsNotificationEvent)nekoWsGroupMatchUserLeave2);
			logger.Success("[NekoNativeGroup] pushed groupMatchUserLeave -> target=" + targetProfileId + " leaverAid=" + leaverAid, (Exception)null);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoNativeGroup] push user-leave failed: " + ex.Message, (Exception)null);
		}
	}

	public void PushGroupRemoved(string targetProfileId)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		try
		{
			NekoWsGroupMatchWasRemoved nekoWsGroupMatchWasRemoved = new NekoWsGroupMatchWasRemoved();
			((WsNotificationEvent)nekoWsGroupMatchWasRemoved).EventType = (NotificationEventType)5;
			((WsNotificationEvent)nekoWsGroupMatchWasRemoved).EventIdentifier = new MongoId();
			NekoWsGroupMatchWasRemoved nekoWsGroupMatchWasRemoved2 = nekoWsGroupMatchWasRemoved;
			notificationSendHelper.SendMessageAsync(new MongoId(targetProfileId), (WsNotificationEvent)nekoWsGroupMatchWasRemoved2);
			logger.Success("[NekoNativeGroup] pushed groupMatchWasRemoved -> target=" + targetProfileId, (Exception)null);
		}
		catch (Exception ex)
		{
			logger.Error("[NekoNativeGroup] push group-removed failed: " + ex.Message, (Exception)null);
		}
	}

	private static int ParseAid(string? aid)
	{
		if (!int.TryParse(aid, out var result))
		{
			return 0;
		}
		return result;
	}

	private static GroupCharacter ToFrameworkCharacter(NekoGroupCharacterDto dto)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected O, but got Unknown
		return new GroupCharacter
		{
			Id = dto.Id,
			Aid = ParseAid(dto.Aid),
			IsLeader = dto.IsLeader,
			IsReady = dto.IsReady,
			Region = dto.Region,
			LookingGroup = dto.LookingGroup,
			Info = ToFrameworkInfo(dto.Info),
			VisualRepresentation = dto.PlayerVisualRepresentation
		};
	}

	private static CharacterInfo? ToFrameworkInfo(NekoGroupInfoDto? info)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		if (info == null)
		{
			return null;
		}
		return new CharacterInfo
		{
			Nickname = info.Nickname,
			Side = info.Side,
			Level = info.Level,
			MemberCategory = (MemberCategory)info.MemberCategory,
			GameVersion = info.GameVersion,
			HasCoopExtension = info.HasCoopExtension
		};
	}
}
