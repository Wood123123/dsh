namespace CookingGrenades;

internal static class FikaPatches_ObservedGrenadeSpawn
{
	public static void Prefix(object __instance, ref float timeSinceSafetyLevelRemoved)
	{
		if (__instance != null)
		{
			float num = ObservedControllerCookStore.Take(__instance);
			if (num > 0f)
			{
				timeSinceSafetyLevelRemoved = num;
				Plugin.log.LogInfo((object)$"[CG-Fika][SpawnGrenade] 注入 cook={num:F2}s (controller={__instance.GetType().Name})");
			}
		}
	}
}
