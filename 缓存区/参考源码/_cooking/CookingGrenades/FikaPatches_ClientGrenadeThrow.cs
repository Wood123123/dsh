using UnityEngine;

namespace CookingGrenades;

internal static class FikaPatches_ClientGrenadeThrow
{
	public static void Prefix(float timeSinceSafetyLevelRemoved)
	{
		float num = (GrenadeCookingManager.GetCookingTimer().IsCooking ? Mathf.Max(0f, GrenadeCookingManager.GetCookingTimer().GetCookingTime()) : 0f);
		float num2 = Mathf.Max(num, timeSinceSafetyLevelRemoved);
		if (num2 > 0f)
		{
			Plugin.NextCookTime = num2;
			Plugin.log.LogInfo((object)$"[CG-Fika][Throw] 本次投掷 cook={num2:F2}s (mod={num:F2}, fika={timeSinceSafetyLevelRemoved:F2})");
		}
	}
}
