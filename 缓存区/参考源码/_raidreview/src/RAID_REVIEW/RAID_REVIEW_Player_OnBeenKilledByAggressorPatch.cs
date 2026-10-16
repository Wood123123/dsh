using System;
using System.Reflection;
using EFT;
using EFT.Ballistics;
using Newtonsoft.Json;
using SPT.Reflection.Patching;
using UnityEngine;

namespace RAID_REVIEW;

public class RAID_REVIEW_Player_OnBeenKilledByAggressorPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(Player).GetMethod("OnBeenKilledByAggressor", BindingFlags.Instance | BindingFlags.Public);
	}

	[PatchPostfix]
	private unsafe static void PatchPostFix(ref Player __instance, IPlayer aggressor, DamageInfo damageInfo, EBodyPart bodyPart, EDamageType lethalDamageType)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		if (__instance.Location == "hideout")
		{
			return;
		}
		try
		{
			if (RAID_REVIEW.KillTracking.Value)
			{
				TrackingRaidKill trackingRaidKill = new TrackingRaidKill
				{
					sessionId = RAID_REVIEW.sessionId,
					time = RAID_REVIEW.stopwatch.ElapsedMilliseconds,
					profileId = aggressor.ProfileId,
					killedId = __instance.ProfileId,
					distance = Vector3.Distance(aggressor.Position, __instance.Position),
					weapon = ((damageInfo.Weapon == null) ? "?" : LocalizationExtensions.LocalizedShortName(damageInfo.Weapon)),
					weaponTemplateId = ((damageInfo.Weapon == null) ? "" : ((object)damageInfo.Weapon.TemplateId/*cast due to constrained. prefix*/).ToString()),
					bodyPart = ((object)(*(EBodyPart*)(&bodyPart))/*cast due to constrained. prefix*/).ToString(),
					type = ((object)(*(EDamageType*)(&lethalDamageType))/*cast due to constrained. prefix*/).ToString(),
					positionKiller = JsonConvert.SerializeObject((object)aggressor.Position),
					positionKilled = JsonConvert.SerializeObject((object)__instance.Position)
				};
				Telemetry.Send("KILL", JsonConvert.SerializeObject((object)trackingRaidKill));
			}
		}
		catch (Exception ex)
		{
			ModulePatch.Logger.LogError((object)(ex.Message ?? ""));
		}
	}
}
