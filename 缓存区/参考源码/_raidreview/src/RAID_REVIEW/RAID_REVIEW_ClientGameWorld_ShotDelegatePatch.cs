using System;
using System.Reflection;
using EFT;
using EFT.Ballistics;
using Newtonsoft.Json;
using SPT.Reflection.Patching;
using UnityEngine;

namespace RAID_REVIEW;

public class RAID_REVIEW_ClientGameWorld_ShotDelegatePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(ClientGameWorld).GetMethod("ShotDelegate", BindingFlags.Instance | BindingFlags.Public);
	}

	[PatchPostfix]
	private static void PatchPostFix(ref ClientGameWorld __instance, Shot shotResult)
	{
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (((GameWorld)__instance).LocationId == "hideout" || !RAID_REVIEW.BallisticsTracking.Value)
			{
				return;
			}
			object obj;
			if (shotResult == null)
			{
				obj = null;
			}
			else
			{
				BallisticCollider hittedBallisticCollider = shotResult.HittedBallisticCollider;
				if (hittedBallisticCollider == null)
				{
					obj = null;
				}
				else
				{
					GameObject gameObject = ((Component)hittedBallisticCollider).gameObject;
					if (gameObject == null)
					{
						obj = null;
					}
					else
					{
						Player componentInParent = gameObject.GetComponentInParent<Player>();
						obj = ((componentInParent != null) ? componentInParent.ProfileId : null);
					}
				}
			}
			string text = (string)obj;
			TrackingBallistic trackingBallistic = new TrackingBallistic
			{
				sessionId = RAID_REVIEW.sessionId,
				profileId = shotResult.PlayerProfileID,
				weaponId = shotResult.Weapon.Id,
				weaponName = LocalizationExtensions.LocalizedShortName(shotResult.Weapon),
				ammoId = shotResult.Ammo.Id,
				time = RAID_REVIEW.stopwatch.ElapsedMilliseconds,
				hitPlayerId = (text ?? null),
				source = JsonConvert.SerializeObject((object)shotResult.MasterOrigin),
				target = JsonConvert.SerializeObject((object)shotResult.HitPoint)
			};
			Telemetry.Send("BALLISTIC", JsonConvert.SerializeObject((object)trackingBallistic));
		}
		catch (Exception arg)
		{
			ModulePatch.Logger.LogError((object)$"{arg}");
		}
	}
}
