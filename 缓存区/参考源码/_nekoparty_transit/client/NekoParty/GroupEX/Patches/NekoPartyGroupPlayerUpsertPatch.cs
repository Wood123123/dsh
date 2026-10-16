using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.UI.Matchmaker;
using Fika.Core.Main.Utils;
using JsonType;
using NekoParty.GroupEX.UI;
using Newtonsoft.Json;

namespace NekoParty.GroupEX.Patches;

public static class NekoPartyGroupPlayerUpsertPatch
{
	public static bool Prefix(MatchmakerPlayersController __instance, RaidPlayer player)
	{
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Expected O, but got Unknown
		if (!NekoGroupExConfig.UseNativePartyInvite || __instance == null || player == null)
		{
			return true;
		}
		try
		{
			LogIncoming(player, "ADD");
			RaidPlayer val = ((IEnumerable<RaidPlayer>)((BaseMatchmakerController<RaidSettings>)(object)__instance).GroupPlayers)?.FirstOrDefault((RaidPlayer p) => p != null && ((GroupPlayer)p).AccountId == ((GroupPlayer)player).AccountId);
			if (val == null || val == player)
			{
				return true;
			}
			bool num = ((GroupPlayer)val).Info?.Side != ((GroupPlayer)player).Info?.Side;
			bool flag = !string.Equals(((GroupPlayer)val).Info?.Nickname, ((GroupPlayer)player).Info?.Nickname, StringComparison.Ordinal);
			if (!num && !flag)
			{
				return false;
			}
			val.UpdateFromAnotherItem((GroupPlayer)player);
			NekoPartyMatchMakerController.Instance?.RequestGroupPreviewRebuild();
			return false;
		}
		catch (Exception arg)
		{
			FikaGlobals.LogError($"[NekoParty] method_39 队友外观就地刷新失败：{arg}", "Prefix");
			return true;
		}
	}

	private static void LogIncoming(RaidPlayer player, string tag)
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			PlayerVisualRepresentationDescriptor playerVisualRepresentation = ((GroupPlayer)player)._playerVisualRepresentation;
			string text = ((playerVisualRepresentation == null) ? "null" : JsonConvert.SerializeObject((object)playerVisualRepresentation));
			if (text != null && text.Length > 4000)
			{
				text = text.Substring(0, 4000);
			}
			PlayerInfo info = ((GroupPlayer)player).Info;
			FikaGlobals.LogWarning($"[NekoP2P] {tag}AddPlayer id={((GroupPlayer)player).Id} accountId={((GroupPlayer)player).AccountId} nick={info?.Nickname} savage={info?.SavageNickname} side={info?.Side} cat={info?.MemberCategory} selCat={info?.SelectedMemberCategory} gameVer={info?.GameVersion} visual={text}", "UpsertDiag");
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("[NekoP2P] diag serialize fail: " + ex.Message, "UpsertDiag");
		}
	}
}
