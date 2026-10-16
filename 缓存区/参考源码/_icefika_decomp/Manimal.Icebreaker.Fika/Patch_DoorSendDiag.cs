using System;
using EFT;
using EFT.Interactive;
using Fika.Core.Main.Players;
using HarmonyLib;
using UnityEngine;

namespace Manimal.Icebreaker.Fika;

[HarmonyPatch(typeof(FikaPlayer), "ExecuteInteraction", new Type[]
{
	typeof(WorldInteractiveObject),
	typeof(InteractionResult)
})]
internal static class Patch_DoorSendDiag
{
	[HarmonyPostfix]
	private static void Postfix(FikaPlayer __instance, WorldInteractiveObject door)
	{
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (IceGate.On && !((Object)(object)door == (Object)null) && ((Player)__instance).IsYourPlayer)
			{
				FikaAddonPlugin.Log.LogDebug((object)$"[DoorDiag] SEND execute id='{door.Id}' pos={((Component)door).transform.position} forceLocal={door.ForceLocalInteraction}");
			}
		}
		catch
		{
		}
	}
}
