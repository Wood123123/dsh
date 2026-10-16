using System.Threading.Tasks;
using EFT;
using Newtonsoft.Json;
using UnityEngine;

namespace RAID_REVIEW;

internal class BotChecker
{
	public static async void BotCheckLoop(bool endCheck)
	{
		while (RAID_REVIEW.inRaid)
		{
			if (!endCheck)
			{
				await Task.Delay(30000);
			}
			long elapsedMilliseconds = RAID_REVIEW.stopwatch.ElapsedMilliseconds;
			foreach (Player item in RAID_REVIEW.gameWorld.AllPlayersEverExisted)
			{
				DeadOrUnspawnCheck(elapsedMilliseconds, item.ProfileId);
			}
		}
	}

	public static void DeadOrUnspawnCheck(long captureTime, string profileId)
	{
		TrackingPlayerDeadOrUnspawned trackingPlayerDeadOrUnspawned = new TrackingPlayerDeadOrUnspawned
		{
			sessionId = RAID_REVIEW.sessionId,
			profileId = profileId,
			time = captureTime
		};
		Player everExistedPlayerByID = RAID_REVIEW.gameWorld.GetEverExistedPlayerByID(profileId);
		if ((Object)(object)everExistedPlayerByID == (Object)null)
		{
			trackingPlayerDeadOrUnspawned.status = PlayerStatus.Unknown;
			Telemetry.Send("PLAYER_STATUS", JsonConvert.SerializeObject((object)trackingPlayerDeadOrUnspawned));
		}
		else if ((Object)(object)everExistedPlayerByID != (Object)null)
		{
			if (!everExistedPlayerByID.HealthController.IsAlive && everExistedPlayerByID.KillerId == null)
			{
				trackingPlayerDeadOrUnspawned.status = PlayerStatus.Unspawned;
				Telemetry.Send("PLAYER_STATUS", JsonConvert.SerializeObject((object)trackingPlayerDeadOrUnspawned));
			}
			else if (!everExistedPlayerByID.HealthController.IsAlive)
			{
				trackingPlayerDeadOrUnspawned.status = PlayerStatus.Dead;
				Telemetry.Send("PLAYER_STATUS", JsonConvert.SerializeObject((object)trackingPlayerDeadOrUnspawned));
			}
			else if (everExistedPlayerByID.HealthController.IsAlive)
			{
				trackingPlayerDeadOrUnspawned.status = PlayerStatus.Alive;
				Telemetry.Send("PLAYER_STATUS", JsonConvert.SerializeObject((object)trackingPlayerDeadOrUnspawned));
			}
		}
	}
}
