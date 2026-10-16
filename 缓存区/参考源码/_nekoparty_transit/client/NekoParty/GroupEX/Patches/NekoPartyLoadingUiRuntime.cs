using System;
using EFT;
using EFT.UI.Matchmaker;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyLoadingUiRuntime
{
	private const string LoadingLootPrefix = "Loading loot... ";

	private static readonly (string TitleKey, string BodyKey)[] LoadingTips = new(string, string)[6]
	{
		("NKP_LoadingTipTitleParty", "NKP_LoadingTipBodyParty"),
		("NKP_LoadingTipTitleMixedSide", "NKP_LoadingTipBodyMixedSide"),
		("NKP_LoadingTipTitleInviteFlow", "NKP_LoadingTipBodyInviteFlow"),
		("NKP_LoadingTipTitleRecovery", "NKP_LoadingTipBodyRecovery"),
		("NKP_LoadingTipTitleRewind", "NKP_LoadingTipBodyRewind"),
		("NKP_LoadingTipTitleStatus", "NKP_LoadingTipBodyStatus")
	};

	public static string RemapStatus(string status)
	{
		if (string.IsNullOrWhiteSpace(status))
		{
			return status;
		}
		status = status.Trim();
		if (status.StartsWith("Loading loot... ", StringComparison.Ordinal))
		{
			return status.Substring("Loading loot... ".Length).Trim() switch
			{
				"LoadingBundles" => "NKP_LoadingStatusLoadingLootBundles", 
				"CreatingPools" => "NKP_LoadingStatusLoadingLootPools", 
				"LoadingMap" => "NKP_LoadingStatusLoadingLootMap", 
				_ => "NKP_LoadingStageLoot", 
			};
		}
		return status switch
		{
			"Joining coop game..." => "NKP_LoadingStatusJoiningCoop", 
			"Hosting coop game..." => "NKP_LoadingStatusHostingCoop", 
			"Coop game joined" => "NKP_LoadingStatusJoined", 
			"Coop game created" => "NKP_LoadingStatusCreated", 
			"Loading objects..." => "NKP_LoadingStatusLoadingObjects", 
			"Map loading..." => "NKP_LoadingStatusMapLoading", 
			"Caching data..." => "NKP_LoadingStatusCachingData", 
			"LoadingBundles" => "NKP_LoadingStatusLoadingBundles", 
			"CreatingPools" => "NKP_LoadingStatusCreatingPools", 
			"LoadingMap" => "NKP_LoadingStatusLoadingMap", 
			_ => status, 
		};
	}

	public static void ApplyBannerTip(MatchmakerBannersPanel panel)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Expected O, but got Unknown
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		if (!((Object)panel == (Object)null) && LoadingTips.Length != 0)
		{
			TextMeshProUGUI value = Traverse.Create((object)panel).Field("_bannerHeader").GetValue<TextMeshProUGUI>();
			TextMeshProUGUI value2 = Traverse.Create((object)panel).Field("_bannerDescription").GetValue<TextMeshProUGUI>();
			int value3 = Traverse.Create((object)panel).Field("_currentSelectedBanner").GetValue<int>();
			(string, string) tuple = LoadingTips[Mathf.Abs(value3) % LoadingTips.Length];
			if ((Object)value != (Object)null)
			{
				((TMP_Text)value).text = LocalizationExtensions.Localized(tuple.Item1, (string)null);
			}
			if ((Object)value2 != (Object)null)
			{
				((Component)value2).gameObject.SetActive(true);
				((TMP_Text)value2).enableWordWrapping = true;
				((TMP_Text)value2).text = LocalizationExtensions.Localized(tuple.Item2, (string)null);
			}
		}
	}
}
