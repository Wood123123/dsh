using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.Communications;
using Newtonsoft.Json;
using SPT.Reflection.Patching;
using UnityEngine;

namespace RAID_REVIEW;

public class RAID_REVIEW_Player_OnGameSessionEndPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(Player).GetMethod("OnGameSessionEnd");
	}

	[PatchPostfix]
	private static void PatchPostFix(ref Player __instance, ExitStatus exitStatus, float pastTime, string locationId, string exitName)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Invalid comparison between Unknown and I4
		if (locationId == "hideout")
		{
			return;
		}
		try
		{
			ModulePatch.Logger.LogInfo((object)"RAID_REVIEW :::: INFO :::: RAID Completed, Saving Tracking Data");
			RAID_REVIEW.tracking = false;
			RAID_REVIEW.inRaid = false;
			RAID_REVIEW.stopwatch.Stop();
			RAID_REVIEW.trackingRaid.exitStatus = exitStatus;
			RAID_REVIEW.trackingRaid.exitName = exitName;
			RAID_REVIEW.trackingRaid.time = DateTime.Now;
			RAID_REVIEW.trackingRaid.timeInRaid = RAID_REVIEW.stopwatch.ElapsedMilliseconds;
			if ((Object)(object)RAID_REVIEW.myPlayer != (Object)null)
			{
				RAID_REVIEW.trackingRaid.type = (((int)RAID_REVIEW.myPlayer.Side == 4) ? "SCAV" : "PMC");
			}
			RAID_REVIEW.stopwatch.Reset();
			BotChecker.BotCheckLoop(endCheck: true);
			if (RAID_REVIEW.SOLARINT_SAIN__DETECTED)
			{
				SAIN_Integration.CheckForSainComponents(endCheck: true);
			}
			Telemetry.Send("PLAYER_CHECK", JsonConvert.SerializeObject((object)RAID_REVIEW.trackingPlayers.Values));
			Telemetry.Send("END", JsonConvert.SerializeObject((object)RAID_REVIEW.trackingRaid));
			if (RAID_REVIEW.RecordingNotification.Value && RAID_REVIEW.WebSocketConnected)
			{
				NotificationManager.DisplayMessageNotification("Raid Review Recording Completed", (ENotificationDurationType)1, (ENotificationIconType)0, (Color?)null);
			}
		}
		catch (Exception ex)
		{
			ModulePatch.Logger.LogError((object)(ex.Message ?? ""));
		}
		finally
		{
			RAID_REVIEW.trackingPlayers = new Dictionary<string, TrackingPlayer>();
			RAID_REVIEW._lastBotQuestState.Clear();
			RAID_REVIEW._lastBotObjectiveState.Clear();
			RAID_REVIEW.sessionId = null;
		}
	}
}
