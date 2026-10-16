using EFT;
using EFT.InputSystem;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(GamePlayerOwner), "TranslateCommand")]
internal static class PauseCommandPatch
{
	private static bool Prefix(GamePlayerOwner __instance, ECommand __0, ref ETranslateResult __result)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I4
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Invalid comparison between Unknown and I4
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Invalid comparison between Unknown and I4
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Invalid comparison between Unknown and I4
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Invalid comparison between Unknown and I4
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Invalid comparison between Unknown and I4
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Invalid comparison between Unknown and I4
		bool flag = !SinglePlayerPause.Owns(__instance) && !RaidPause.CoopPaused;
		bool flag2;
		if (!flag)
		{
			if ((int)__0 <= 26)
			{
				if ((int)__0 == 2 || (int)__0 == 24 || (int)__0 == 26)
				{
					goto IL_003f;
				}
			}
			else if ((int)__0 == 58 || __0 - 69 <= 2 || __0 - 81 <= 2)
			{
				goto IL_003f;
			}
			flag2 = false;
			goto IL_0045;
		}
		goto IL_0047;
		IL_0045:
		flag = flag2;
		goto IL_0047;
		IL_003f:
		flag2 = true;
		goto IL_0045;
		IL_0047:
		if (flag)
		{
			return true;
		}
		__result = (ETranslateResult)2;
		return false;
	}
}
