using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ChatShared;
using Diz.Binding;
using EFT;
using EFT.UI.Matchmaker;
using Fika.Core.Main.Utils;
using HarmonyLib;
using JsonType;
using NekoParty.GroupEX.Compat;
using NekoParty.GroupEX.Models;
using NekoParty.GroupEX.Networking;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyPreparePreview
{
	private static FieldInfo _controllerField;

	private static readonly HashSet<string> _bundleWarmedTeammateAids = new HashSet<string>();

	private static readonly HashSet<string> _visualUploadedAids = new HashSet<string>();

	private static readonly Dictionary<string, int> _peerVisualFetchCount = new Dictionary<string, int>();

	internal static MatchmakerPlayersController GetController(MatchMakerAcceptScreen screen)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Expected O, but got Unknown
		if ((Object)screen == (Object)null)
		{
			return null;
		}
		if (_controllerField == null)
		{
			_controllerField = AccessTools.Field(typeof(MatchMakerAcceptScreen), "MatchmakerPlayersController");
		}
		object obj = _controllerField?.GetValue(screen);
		MatchmakerPlayersController val = (MatchmakerPlayersController)((obj is MatchmakerPlayersController) ? obj : null);
		if (val == null)
		{
			val = Traverse.Create((object)screen).Field("MatchmakerPlayersController").GetValue<MatchmakerPlayersController>();
		}
		return val;
	}

	internal static void EnsurePartyRaidLocation(RaidSettings raidSettings)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Invalid comparison between Unknown and I4
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected I4, but got Unknown
		if (raidSettings == null)
		{
			return;
		}
		try
		{
			NekoPartyOperationResponse nekoPartyOperationResponse = NekoGroupRequestHandler.SyncPartyRaidSettings(raidSettings.SelectedLocation?.Id ?? string.Empty, time: ((int)raidSettings.SelectedDateTime == 1) ? 1 : 0, side: (int)raidSettings.Side);
			if (raidSettings.SelectedLocation == null)
			{
				NekoPartyData? party = nekoPartyOperationResponse.Party;
				if (!party.HasValue || string.IsNullOrEmpty(party.Value.Location))
				{
					FikaGlobals.LogWarning("[NekoParty诊断] EnsurePartyRaidLocation: 自己无选图且 party 未登记队长地图" + $"(party!=null={party.HasValue})→ SelectedLocation 仍为 null，准备页可能缺图（队长尚未进过准备页？）。", "EnsurePartyRaidLocation");
				}
				else
				{
					ApplyLocationToRaidSettings(raidSettings, party.Value.Location, party.Value.Time);
				}
			}
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty诊断] EnsurePartyRaidLocation 异常：{arg}", "EnsurePartyRaidLocation");
		}
	}

	private static void ApplyLocationToRaidSettings(RaidSettings raidSettings, string locationId, int time)
	{
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		if (raidSettings != null && !string.IsNullOrEmpty(locationId))
		{
			raidSettings.LocationId = locationId;
			Dictionary<string, Location> dictionary = NekoSessionCompat.GetLocationSettings()?.locations;
			Location val = dictionary?.Values.FirstOrDefault((Location x) => x != null && string.Equals(x.Id, locationId, StringComparison.OrdinalIgnoreCase));
			if (val != null)
			{
				raidSettings.SelectedLocation = val;
				raidSettings.SelectedDateTime = (EDateTime)(time == 1);
				FikaGlobals.LogWarning($"[NekoParty诊断] EnsurePartyRaidLocation: 已把队长地图注入队员 RaidSettings location={locationId} time={time}", "ApplyLocationToRaidSettings");
			}
			else
			{
				FikaGlobals.LogWarning("[NekoParty诊断] EnsurePartyRaidLocation: 队长地图 id=" + locationId + " 在 LocationSettings 未解析到 Location" + $"(locations!=null={dictionary != null})→ SelectedLocation 仍 null。", "ApplyLocationToRaidSettings");
			}
		}
	}

	internal static PlayerVisualRepresentation BuildSelfVisual(MatchmakerPlayersController pc, RaidSettings raidSettings)
	{
		if (pc == null)
		{
			FikaGlobals.LogWarning("[NekoParty诊断] EnsureSelfVisual: controller=null（GetController 反射失败）→ 中央自己无法补外观", "EnsureSelfVisual");
			return null;
		}
		if (((BaseMatchmakerController<RaidSettings>)(object)pc).CurrentPlayer == null)
		{
			FikaGlobals.LogWarning("[NekoParty诊断] EnsureSelfVisual: CurrentPlayer=null → 中央自己无法补外观", "EnsureSelfVisual");
			return null;
		}
		try
		{
			IMatchmakerSession<RaidSettings> value = Traverse.Create((object)pc).Field("_backEndSession").GetValue<IMatchmakerSession<RaidSettings>>();
			if (value == null)
			{
				FikaGlobals.LogWarning("[NekoParty诊断] EnsureSelfVisual: Ginterface225_0=null → 取不到 profile", "EnsureSelfVisual");
				return null;
			}
			Profile val = ((raidSettings != null && raidSettings.IsScav) ? ((IProfileSession)value).ProfileOfPet : ((IProfileSession)value).Profile);
			if (val == null)
			{
				FikaGlobals.LogWarning("[NekoParty诊断] EnsureSelfVisual: profile=null → 无法生成外观", "EnsureSelfVisual");
				return null;
			}
			return val.GetVisualEquipmentState(false);
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty诊断] EnsureSelfVisual 异常：{arg}", "EnsureSelfVisual");
			return null;
		}
	}

	internal static void EnsureSelfVisual(MatchmakerPlayersController pc, RaidSettings raidSettings)
	{
		RaidPlayer val = ((pc != null) ? ((BaseMatchmakerController<RaidSettings>)(object)pc).CurrentPlayer : null);
		if (val == null)
		{
			BuildSelfVisual(pc, raidSettings);
			return;
		}
		PlayerVisualRepresentation val2 = BuildSelfVisual(pc, raidSettings);
		if (val2 == null)
		{
			return;
		}
		try
		{
			val.UpdateVisualRepresentation(val2);
			FikaGlobals.LogWarning($"[NekoParty诊断] EnsureSelfVisual: 已【强制重建】中央自己外观 aid={((GroupPlayer)val).AccountId} newVisual!=null={val2 != null} IsEmpty={val2.IsEmpty()}", "EnsureSelfVisual");
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty诊断] EnsureSelfVisual 应用异常：{arg}", "EnsureSelfVisual");
		}
	}

	internal static void ForceGroupStatusRefresh(MatchmakerPlayersController pc)
	{
		if (pc == null)
		{
			FikaGlobals.LogWarning("[NekoParty诊断] ForceGroupStatusRefresh: controller=null（GetController 反射失败）→ 不拉取 group/status，队员端必空", "ForceGroupStatusRefresh");
			return;
		}
		try
		{
			FikaGlobals.LogWarning("[NekoParty诊断] ForceGroupStatusRefresh: 调 method_2() 拉 group/status " + $"Initialized={((BaseMatchmakerController<RaidSettings>)(object)pc).Initialized} 拉取前GroupPlayers.Count={((BaseMatchmakerController<RaidSettings>)(object)pc).GroupPlayers?.Count ?? (-1)}", "ForceGroupStatusRefresh");
			((BaseMatchmakerController<RaidSettings>)(object)pc).GetGroupStatus();
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty] 准备页强制拉取小队状态失败：{arg}", "ForceGroupStatusRefresh");
		}
	}

	internal static void TryRender(MatchMakerAcceptScreen screen)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		MatchmakerPlayersController controller = GetController(screen);
		if (controller != null)
		{
			TryRender(screen, ((BaseMatchmakerController<RaidSettings>)(object)controller).CurrentProfileMatchingStatus);
		}
	}

	internal static void TryRender(MatchMakerAcceptScreen screen, EMatchingType matchingType)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		MatchmakerPlayersController controller = GetController(screen);
		if (controller == null)
		{
			FikaGlobals.LogWarning("[NekoParty诊断] TryRender: controller=null（GetController 反射失败）→ 跳过", "TryRender");
			return;
		}
		FikaGlobals.LogWarning($"[NekoParty诊断] TryRender: GroupPlayers.Count={((BaseMatchmakerController<RaidSettings>)(object)controller).GroupPlayers?.Count ?? (-1)} matchingType={matchingType} → 进入接管渲染", "TryRender");
		Render(screen, controller, matchingType);
	}

	private static void Render(MatchMakerAcceptScreen screen, MatchmakerPlayersController pc, EMatchingType matchingType)
	{
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Invalid comparison between Unknown and I4
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Expected O, but got Unknown
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Expected O, but got Unknown
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Expected O, but got Unknown
		//IL_01e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Expected O, but got Unknown
		//IL_024f: Unknown result type (might be due to invalid IL or missing references)
		//IL_025a: Expected O, but got Unknown
		//IL_0261: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Expected O, but got Unknown
		//IL_027d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0288: Expected O, but got Unknown
		try
		{
			Traverse val = Traverse.Create((object)screen);
			RaidSettings value = val.Field("_raidSettings").GetValue<RaidSettings>();
			IEftSession value2 = val.Field("_backEndSession").GetValue<IEftSession>();
			string selfAid = val.Field("_currentProfileAid").GetValue<string>();
			if (string.IsNullOrEmpty(selfAid))
			{
				selfAid = ((GroupPlayer)((BaseMatchmakerController<RaidSettings>)(object)pc).CurrentPlayer).AccountId;
			}
			MatchMakerGroupPreview value3 = val.Field("_groupPreview").GetValue<MatchMakerGroupPreview>();
			PlayersRaidReadyPanel value4 = val.Field("_playersRaidReadyPanel").GetValue<PlayersRaidReadyPanel>();
			TextMeshProUGUI value5 = val.Field("_groupMembersCountLabel").GetValue<TextMeshProUGUI>();
			Button value6 = val.Field("_updateListButton").GetValue<Button>();
			EnsureSelfVisual(pc, value);
			int num = ((IEnumerable<RaidPlayer>)((BaseMatchmakerController<RaidSettings>)(object)pc).GroupPlayers)?.Count((RaidPlayer p) => p != null && ((GroupPlayer)p).AccountId != selfAid) ?? 0;
			if (num <= 0)
			{
				FikaGlobals.LogWarning("[NekoParty诊断] Render: 排除自己后队友数=0（仅自己/数据未到）→ 只保留中央，等待队友数据到达再铺。" + $" 原始GroupPlayers.Count={((BaseMatchmakerController<RaidSettings>)(object)pc).GroupPlayers?.Count ?? (-1)} selfAid={selfAid}", "Render");
				return;
			}
			bool flag = (int)matchingType == 2;
			if ((Object)value3 != (Object)null)
			{
				value3.Show(selfAid, pc, value, (Func<RaidPlayer, bool, bool, RaidGroupContextInteractions>)((BaseMatchmakerController<RaidSettings>)(object)pc).GetContextInteractions);
				value3.UpdateGroup();
			}
			if ((Object)value4 != (Object)null && value2 != null)
			{
				value4.Show(selfAid, (BindableList<UpdatableChatMember>)(object)((ISocial)value2).SocialNetwork.FriendsList, pc, flag);
			}
			if ((Object)value5 != (Object)null)
			{
				((TMP_Text)value5).text = $"{((BaseMatchmakerController<RaidSettings>)(object)pc).GroupPlayers.Count}<color=#C7C5B4FF>/{((BaseBindable<int>)(object)((BaseMatchmakerController<RaidSettings>)(object)pc).MaxGroupCount).Value}</color>";
			}
			if ((Object)value6 != (Object)null)
			{
				((Component)value6).gameObject.SetActive(flag);
			}
			FikaGlobals.LogWarning($"[NekoParty诊断] 接管渲染完成: 人数={((BaseMatchmakerController<RaidSettings>)(object)pc).GroupPlayers.Count}/{((BaseBindable<int>)(object)((BaseMatchmakerController<RaidSettings>)(object)pc).MaxGroupCount).Value} 队友(不含自己)={num} " + $"isLeader={flag} groupPreview!=null={(Object)value3 != (Object)null} readyPanel!=null={(Object)value4 != (Object)null} " + $"countLabel!=null={(Object)value5 != (Object)null} selfAid={selfAid} session!=null={value2 != null}", "Render");
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty诊断] 准备页接管渲染失败：{arg}", "Render");
		}
	}

	internal static bool GroupPreviewNeedsWarmup(MatchmakerPlayersController pc)
	{
		if (((BaseMatchmakerController<RaidSettings>)(object)pc)?.GroupPlayers == null)
		{
			return false;
		}
		string selfAid = ((GroupPlayer)((BaseMatchmakerController<RaidSettings>)(object)pc).CurrentPlayer).AccountId;
		return ((IEnumerable<RaidPlayer>)((BaseMatchmakerController<RaidSettings>)(object)pc).GroupPlayers).Any((RaidPlayer p) => p != null && ((GroupPlayer)p).AccountId != selfAid && !string.IsNullOrEmpty(((GroupPlayer)p).AccountId) && !_bundleWarmedTeammateAids.Contains(((GroupPlayer)p).AccountId));
	}

	internal static void MarkGroupPreviewWarmed(MatchmakerPlayersController pc)
	{
		if (((BaseMatchmakerController<RaidSettings>)(object)pc)?.GroupPlayers == null)
		{
			return;
		}
		string accountId = ((GroupPlayer)((BaseMatchmakerController<RaidSettings>)(object)pc).CurrentPlayer).AccountId;
		foreach (RaidPlayer groupPlayer in ((BaseMatchmakerController<RaidSettings>)(object)pc).GroupPlayers)
		{
			if (groupPlayer != null && ((GroupPlayer)groupPlayer).AccountId != accountId && !string.IsNullOrEmpty(((GroupPlayer)groupPlayer).AccountId))
			{
				_bundleWarmedTeammateAids.Add(((GroupPlayer)groupPlayer).AccountId);
			}
		}
	}

	internal static void RebuildGroupPreview(MatchMakerAcceptScreen screen)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		MatchmakerPlayersController controller = GetController(screen);
		if (controller == null)
		{
			return;
		}
		try
		{
			TextMeshProUGUI value = Traverse.Create((object)screen).Field("_groupMembersCountLabel").GetValue<TextMeshProUGUI>();
			if ((Object)value != (Object)null)
			{
				((TMP_Text)value).text = $"{((BaseMatchmakerController<RaidSettings>)(object)controller).GroupPlayers.Count}<color=#C7C5B4FF>/{((BaseBindable<int>)(object)((BaseMatchmakerController<RaidSettings>)(object)controller).MaxGroupCount).Value}</color>";
			}
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty诊断] RebuildGroupPreview(轻量) 异常：{arg}", "RebuildGroupPreview");
		}
	}

	private static bool MemberHasVisual(RaidPlayer member)
	{
		if (member == null)
		{
			return true;
		}
		if (member == null)
		{
			return true;
		}
		if (((GroupPlayer)member)._playerVisualRepresentation != null)
		{
			return true;
		}
		if (((GroupPlayer)member).PlayerVisualRepresentation != null)
		{
			try
			{
				return !((GroupPlayer)member).PlayerVisualRepresentation.IsEmpty();
			}
			catch
			{
				return true;
			}
		}
		return false;
	}

	private static bool TryUploadOwnVisual(MatchmakerPlayersController pc, RaidSettings raidSettings, string selfAid)
	{
		try
		{
			PlayerVisualRepresentation val = BuildSelfVisual(pc, raidSettings);
			if (val == null)
			{
				FikaGlobals.LogWarning("[NekoP2P][Diag] 上传自己外观失败: 无法构建 aid=" + selfAid, "VisualEnsureDiag");
				return false;
			}
			string text = JsonConvert.SerializeObject((object)val);
			if (NekoGroupRequestHandler.UploadPlayerVisual(selfAid, text))
			{
				_visualUploadedAids.Add(selfAid);
				FikaGlobals.LogWarning("[NekoP2P][Diag] 已上传自己外观 aid=" + selfAid + " len=" + text.Length, "VisualEnsureDiag");
				return true;
			}
			FikaGlobals.LogWarning("[NekoP2P][Diag] 上传自己外观失败(服务器拒绝) aid=" + selfAid, "VisualEnsureDiag");
			return false;
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("[NekoP2P][Diag] 上传自己外观异常: " + ex.Message, "VisualEnsureDiag");
			return false;
		}
	}

	private static bool TryFetchPeerVisual(RaidPlayer member)
	{
		try
		{
			if (member == null || string.IsNullOrEmpty(((GroupPlayer)member).AccountId))
			{
				return false;
			}
			string accountId = ((GroupPlayer)member).AccountId;
			int value = 0;
			if (_peerVisualFetchCount.TryGetValue(accountId, out value) && value >= 4)
			{
				return false;
			}
			_peerVisualFetchCount[accountId] = value + 1;
			string playerVisual = NekoGroupRequestHandler.GetPlayerVisual(accountId);
			if (string.IsNullOrEmpty(playerVisual))
			{
				FikaGlobals.LogWarning("[NekoP2P][Diag] 取队友外观为空 aid=" + accountId, "VisualEnsureDiag");
				return false;
			}
			PlayerVisualRepresentation val = JsonConvert.DeserializeObject<PlayerVisualRepresentation>(playerVisual);
			if (val == null)
			{
				FikaGlobals.LogWarning("[NekoP2P][Diag] 反序列化队友外观失败 aid=" + accountId, "VisualEnsureDiag");
				return false;
			}
			member.UpdateVisualRepresentation(val);
			FikaGlobals.LogWarning("[NekoP2P][Diag] 已应用队友外观 aid=" + accountId + " nick=" + ((GroupPlayer)member).Info?.Nickname + " len=" + playerVisual.Length, "VisualEnsureDiag");
			return true;
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("[NekoP2P][Diag] 取/应用队友外观异常: " + ex.Message, "VisualEnsureDiag");
			return false;
		}
	}

	internal static void SafeEnsureVisuals(MatchMakerAcceptScreen screen, int attempt)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_02d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02db: Expected O, but got Unknown
		if ((Object)screen == (Object)null)
		{
			return;
		}
		MatchmakerPlayersController controller = GetController(screen);
		if (controller == null)
		{
			FikaGlobals.LogWarning("[NekoP2P][Diag] SafeEnsureVisuals attempt=" + attempt + " controller=null（反射失败）", "VisualEnsureDiag");
			return;
		}
		try
		{
			RaidSettings value = Traverse.Create((object)screen).Field("_raidSettings").GetValue<RaidSettings>();
			string text = Traverse.Create((object)screen).Field("_currentProfileAid").GetValue<string>();
			if (string.IsNullOrEmpty(text))
			{
				RaidPlayer currentPlayer = ((BaseMatchmakerController<RaidSettings>)(object)controller).CurrentPlayer;
				if (currentPlayer != null)
				{
					text = ((GroupPlayer)currentPlayer).AccountId;
				}
			}
			bool flag = false;
			bool flag2 = false;
			int num = 0;
			int num2 = 0;
			bool flag3 = true;
			List<RaidPlayer> list = new List<RaidPlayer>();
			foreach (RaidPlayer item in (IEnumerable<RaidPlayer>)((BaseMatchmakerController<RaidSettings>)(object)controller).GroupPlayers)
			{
				if (item == null)
				{
					continue;
				}
				GroupPlayer val = (GroupPlayer)(object)item;
				if (string.IsNullOrEmpty(val.AccountId))
				{
					continue;
				}
				bool flag4 = MemberHasVisual(item);
				if (!string.IsNullOrEmpty(text) && val.AccountId == text)
				{
					if (!flag4)
					{
						flag = true;
						flag3 = false;
					}
					continue;
				}
				num++;
				if (!flag4)
				{
					num2++;
					flag3 = false;
					list.Add(item);
					if (attempt == 0 && !flag2)
					{
						flag2 = true;
						ForceGroupStatusRefresh(controller);
					}
				}
			}
			if (flag)
			{
				EnsureSelfVisual(controller, value);
			}
			if (!string.IsNullOrEmpty(text) && !_visualUploadedAids.Contains(text))
			{
				TryUploadOwnVisual(controller, value, text);
			}
			if (list.Count > 0)
			{
				foreach (RaidPlayer item2 in list)
				{
					TryFetchPeerVisual(item2);
				}
			}
			int num3 = 0;
			foreach (RaidPlayer item3 in (IEnumerable<RaidPlayer>)((BaseMatchmakerController<RaidSettings>)(object)controller).GroupPlayers)
			{
				if (item3 != null)
				{
					GroupPlayer val2 = (GroupPlayer)(object)item3;
					if (!string.IsNullOrEmpty(val2.AccountId) && (string.IsNullOrEmpty(text) || !(val2.AccountId == text)) && !MemberHasVisual(item3))
					{
						num3++;
					}
				}
			}
			num2 = num3;
			flag3 = !flag && num3 == 0 && num > 0;
			FikaGlobals.LogWarning("[NekoP2P][Diag] SafeEnsureVisuals attempt=" + attempt + " selfAid=" + text + " selfMissing=" + flag + " peersTotal=" + num + " peersMissing=" + num2 + " allReady=" + flag3, "VisualEnsureDiag");
			if (flag3 && num > 0)
			{
				try
				{
					MatchMakerGroupPreview value2 = Traverse.Create((object)screen).Field("_groupPreview").GetValue<MatchMakerGroupPreview>();
					if ((Object)value2 != (Object)null)
					{
						value2.UpdateGroup();
						FikaGlobals.LogWarning("[NekoP2P][Diag] SafeEnsureVisuals attempt=" + attempt + " 全员外观就绪→已轻量刷新原生组预览", "VisualEnsureDiag");
					}
					return;
				}
				catch (Exception ex)
				{
					FikaGlobals.LogWarning("[NekoP2P][Diag] SafeEnsureVisuals 预览刷新异常(忽略): " + ex.Message, "VisualEnsureDiag");
					return;
				}
			}
			RebuildGroupPreview(screen);
		}
		catch (Exception ex2)
		{
			FikaGlobals.LogWarning("[NekoP2P][Diag] SafeEnsureVisuals 异常(忽略): " + ex2.Message, "VisualEnsureDiag");
		}
	}
}
