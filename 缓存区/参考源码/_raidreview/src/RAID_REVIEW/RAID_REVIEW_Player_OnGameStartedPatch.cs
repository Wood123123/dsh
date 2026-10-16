using System;
using System.Reflection;
using EFT;
using EFT.Communications;
using Newtonsoft.Json;
using SPT.Reflection.Patching;
using UnityEngine;

namespace RAID_REVIEW;

public class RAID_REVIEW_Player_OnGameStartedPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(GameWorld).GetMethod("OnGameStarted", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}

	[PatchPostfix]
	private static void PatchPostFix(ref GameWorld __instance)
	{
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Invalid comparison between Unknown and I4
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (__instance.LocationId == "hideout")
			{
				return;
			}
			ModulePatch.Logger.LogInfo((object)"RAID_REVIEW :::: INFO :::: RAID Settings Loaded");
			if (RAID_REVIEW.sessionId == null)
			{
				GameWorld gameWorld = RAID_REVIEW.gameWorld;
				object obj;
				if (gameWorld == null)
				{
					obj = null;
				}
				else
				{
					ref MongoID? currentProfileId = ref gameWorld.CurrentProfileId;
					obj = (currentProfileId.HasValue ? ((object)currentProfileId.GetValueOrDefault()/*cast due to constrained. prefix*/).ToString() : null);
				}
				if (obj == null)
				{
					Player myPlayer = RAID_REVIEW.myPlayer;
					obj = ((myPlayer != null) ? myPlayer.ProfileId : null);
					if (obj == null)
					{
						Player mainPlayer = __instance.MainPlayer;
						obj = ((mainPlayer != null) ? mainPlayer.ProfileId : null);
					}
				}
				RAID_REVIEW.sessionId = (string)obj;
			}
			Integrations.ModCheck();
			RAID_REVIEW.stopwatch.Reset();
			RAID_REVIEW.stopwatch.Start();
			string type = "PMC";
			if ((Object)(object)RAID_REVIEW.myPlayer != (Object)null)
			{
				type = (((int)RAID_REVIEW.myPlayer.Side == 4) ? "SCAV" : "PMC");
			}
			RAID_REVIEW.trackingRaid = new TrackingRaid
			{
				sessionId = RAID_REVIEW.sessionId,
				profileId = RAID_REVIEW.sessionId,
				time = DateTime.Now,
				detectedMods = ((RAID_REVIEW.RAID_REVIEW__DETECTED_MODS.Count > 0) ? string.Join(",", RAID_REVIEW.RAID_REVIEW__DETECTED_MODS) : ""),
				location = RAID_REVIEW.gameWorld.LocationId,
				type = type,
				timeInRaid = (RAID_REVIEW.stopwatch.IsRunning ? RAID_REVIEW.stopwatch.ElapsedMilliseconds : 0)
			};
			Telemetry.Send("START", JsonConvert.SerializeObject((object)RAID_REVIEW.trackingRaid));
			if ((Object)(object)RAID_REVIEW.myPlayer != (Object)null)
			{
				TrackingPlayer trackingPlayer = new TrackingPlayer
				{
					sessionId = RAID_REVIEW.sessionId,
					profileId = RAID_REVIEW.myPlayer.ProfileId,
					name = RAID_REVIEW.myPlayer.Profile.Nickname,
					level = RAID_REVIEW.myPlayer.Profile.Info.Level,
					team = RAID_REVIEW.myPlayer.Side,
					group = 0,
					spawnTime = RAID_REVIEW.stopwatch.ElapsedMilliseconds,
					type = "HUMAN",
					mod_SAIN_brain = "PLAYER",
					mod_SAIN_difficulty = ""
				};
				RAID_REVIEW.trackingPlayers[trackingPlayer.profileId] = trackingPlayer;
				Telemetry.Send("PLAYER", JsonConvert.SerializeObject((object)trackingPlayer));
			}
			RAID_REVIEW.inRaid = true;
			RAID_REVIEW.ResetLooseLootFlag();
			RAID_REVIEW.ResetPhobosFieldFlag();
			RAID_REVIEW.ResetOrbitFieldFlag();
			RAID_REVIEW.ResetOrbitMainObjFlag();
			ModulePatch.Logger.LogInfo((object)"RAID_REVIEW :::: INFO :::: RAID Information Populated");
			if (RAID_REVIEW.RecordingNotification.Value && RAID_REVIEW.WebSocketConnected)
			{
				try
				{
					NotificationManager.DisplayMessageNotification("Raid Review Recording Started", (ENotificationDurationType)1, (ENotificationIconType)0, (Color?)null);
				}
				catch
				{
				}
			}
			if (RAID_REVIEW.SOLARINT_SAIN__DETECTED)
			{
				RAID_REVIEW.searchingForSainComponents = true;
				SAIN_Integration.CheckForSainComponents();
			}
			BotChecker.BotCheckLoop(endCheck: false);
		}
		catch (Exception arg)
		{
			ModulePatch.Logger.LogError((object)$"{arg}");
		}
	}
}
