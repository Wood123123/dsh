using System;
using System.IO;
using BepInEx;
using Fika.Core.Main.Utils;
using HarmonyLib;
using NekoParty.GroupEX.Compat;
using NekoParty.GroupEX.CrossRaid;
using NekoParty.GroupEX.MixedSide;
using NekoParty.GroupEX.Notifications;
using NekoParty.GroupEX.Patches;
using NekoParty.GroupEX.State;
using UnityEngine;

namespace NekoParty.GroupEX;

[BepInPlugin("com.nekoparty.groupex", "NekoParty.GroupEX", "1.0.0")]
[BepInProcess("EscapeFromTarkov.exe")]
[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInDependency(/*Could not decode attribute arguments.*/)]
public class NekoGroupExPlugin : BaseUnityPlugin
{
	private const long StaleFlowGraceSeconds = 60L;

	private const float StaleFlowMaxSeconds = 300f;

	private bool _wasInRaid;

	private float _staleCheckTimer;

	private float _flowStuckSeconds;

	public static NekoGroupExPlugin Instance { get; private set; }

	public Harmony Harmony { get; private set; }

	protected void Awake()
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		Instance = this;
		NekoFikaCompat.RunStartupCheck(((BaseUnityPlugin)this).Logger);
		Harmony = new Harmony("com.nekoparty.groupex");
		NekoPartyConditionalHarmonyPatches.Apply(Harmony);
		PatchAllIsolated(Harmony);
		((BaseUnityPlugin)this).Logger.LogInfo((object)("NekoParty.GroupEX loaded. Core=" + ((AccessTools.TypeByName("NekoParty.Core.NekoPartyCorePlugin") != null) ? "OK" : "MISSING!!")));
		try
		{
			File.WriteAllText(Path.Combine(Path.GetDirectoryName(typeof(NekoGroupExPlugin).Assembly.Location), "NekoParty.GroupEX.loaded.ok"), DateTimeOffset.UtcNow.ToString("O"));
		}
		catch
		{
		}
	}

	private void PatchAllIsolated(Harmony harmony)
	{
		Type[] types = typeof(NekoGroupExPlugin).Assembly.GetTypes();
		foreach (Type type in types)
		{
			try
			{
				harmony.CreateClassProcessor(type).Patch();
			}
			catch (Exception ex)
			{
				((BaseUnityPlugin)this).Logger.LogWarning((object)("补丁类 " + type.Name + " 应用失败（当前 Fika/EFT 版本不兼容此功能点，已跳过）: " + ex.Message));
			}
		}
	}

	protected void Update()
	{
		NekoPartyP2PProbe.Tick();
		bool isInRaid;
		try
		{
			isInRaid = FikaGlobals.IsInRaid;
		}
		catch
		{
			return;
		}
		if (isInRaid != _wasInRaid)
		{
			_wasInRaid = isInRaid;
			_flowStuckSeconds = 0f;
			if (!isInRaid)
			{
				ResetRaidInviteFlow("出局回菜单");
			}
		}
		else
		{
			if (isInRaid)
			{
				return;
			}
			_staleCheckTimer += Time.deltaTime;
			if (_staleCheckTimer < 5f)
			{
				return;
			}
			_staleCheckTimer = 0f;
			if (string.IsNullOrEmpty(NekoGroupState.RaidInviteInProgressId) && NekoGroupState.PendingAcceptedRaidInvite == null && NekoGroupState.SuppressedRaidInviteIds.Count <= 0)
			{
				_flowStuckSeconds = 0f;
				return;
			}
			_flowStuckSeconds += 5f;
			NekoRaidInviteReceivedNotification pendingAcceptedRaidInvite = NekoGroupState.PendingAcceptedRaidInvite;
			long num = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			if ((pendingAcceptedRaidInvite != null && pendingAcceptedRaidInvite.ExpiresAtUnix > 0 && num > pendingAcceptedRaidInvite.ExpiresAtUnix + 60) || _flowStuckSeconds >= 300f)
			{
				ResetRaidInviteFlow("邀请流程滞留超时");
			}
		}
	}

	private void ResetRaidInviteFlow(string reason)
	{
		_flowStuckSeconds = 0f;
		string text = NekoGroupState.RaidInviteInProgressId;
		if (string.IsNullOrEmpty(text))
		{
			text = NekoGroupState.PendingAcceptedRaidInvite?.InviteId;
		}
		if (!string.IsNullOrEmpty(text))
		{
			try
			{
				NekoPartyMixedSideRuntime.ClearFikaInviteInProgress(text);
				NekoPartyMixedSideRuntime.ClearFikaPendingAcceptedRaidInvite(text);
			}
			catch
			{
			}
		}
		bool num = !string.IsNullOrEmpty(NekoGroupState.RaidInviteInProgressId) || NekoGroupState.PendingAcceptedRaidInvite != null || NekoGroupState.SuppressedRaidInviteIds.Count > 0 || NekoGroupState.PendingRaidInvites.Count > 0;
		NekoGroupState.ResetRaidInviteFlow();
		if (num)
		{
			((BaseUnityPlugin)this).Logger.LogInfo((object)("[NekoParty.GroupEX] 战局邀请流程状态已复位（" + reason + "）。"));
		}
	}
}
