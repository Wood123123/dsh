using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace WTTClientCommonLib.Helpers;

public class PlayerWorldStats(ManualLogSource logger)
{
	public void GetPlayerWorldStats()
	{
		if ((Object)(object)WTTClientCommonLib.Player != (Object)null)
		{
			LogPlayerStats("Player", WTTClientCommonLib.Player);
		}
		else
		{
			logger.LogError((object)"Player is null. You aren't in raid or hideout.");
		}
	}

	private void LogPlayerStats(string playerType, Player player)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		LogHelper.LogDebug($"{playerType} Position X: {player.Transform.position.x} Y: {player.Transform.position.y} Z: {player.Transform.position.z}");
		object[] obj = new object[4] { playerType, null, null, null };
		Quaternion rotation = ((Component)player).gameObject.transform.rotation;
		obj[1] = ((Quaternion)(ref rotation)).eulerAngles.x;
		rotation = ((Component)player).gameObject.transform.rotation;
		obj[2] = ((Quaternion)(ref rotation)).eulerAngles.y;
		rotation = ((Component)player).gameObject.transform.rotation;
		obj[3] = ((Quaternion)(ref rotation)).eulerAngles.z;
		LogHelper.LogDebug(string.Format("{0} Rotation X: {1} Y: {2} Z: {3}", obj));
	}
}
