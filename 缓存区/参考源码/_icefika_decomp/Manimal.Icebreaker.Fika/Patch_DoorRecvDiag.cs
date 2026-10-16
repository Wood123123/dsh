using System;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using Fika.Core.Main.Players;
using Fika.Core.Networking.Packets.Player.Common.SubPackets;
using HarmonyLib;
using UnityEngine;

namespace Manimal.Icebreaker.Fika;

[HarmonyPatch(typeof(WorldInteractionPacket), "Execute")]
internal static class Patch_DoorRecvDiag
{
	[HarmonyPrefix]
	private static void Prefix(WorldInteractionPacket __instance, FikaPlayer player)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!IceGate.On)
			{
				return;
			}
			GameWorld instance = Singleton<GameWorld>.Instance;
			WorldInteractiveObject val = ((instance != null) ? instance.FindDoor(__instance.InteractiveId) : null);
			string text = "";
			if ((Object)(object)val == (Object)null)
			{
				Door[] array = Object.FindObjectsOfType<Door>();
				foreach (Door val2 in array)
				{
					if ((Object)(object)val2 != (Object)null && ((WorldInteractiveObject)val2).Id == __instance.InteractiveId)
					{
						text = $" COMPONENT-EXISTS at {((Component)val2).transform.position} (registry stale!)";
						break;
					}
				}
				if (text == "")
				{
					text = " NO-COMPONENT (id mapping diverged!)";
				}
			}
			ManualLogSource log = FikaAddonPlugin.Log;
			string text2 = $"[DoorDiag] RECV id='{__instance.InteractiveId}' stage={__instance.InteractionStage} resolved={(Object)(object)val != (Object)null}";
			string obj = (((Object)(object)val != (Object)null) ? $" pos={((Component)val).transform.position} activeEnabled={((Behaviour)val).isActiveAndEnabled} forceLocal={val.ForceLocalInteraction}" : text);
			object obj2;
			if (!((Object)(object)player != (Object)null))
			{
				obj2 = "null";
			}
			else
			{
				Profile profile = ((Player)player).Profile;
				obj2 = ((profile != null) ? profile.Nickname : null);
			}
			log.LogDebug((object)(text2 + obj + " byPlayer=" + (string?)obj2));
		}
		catch (Exception ex)
		{
			FikaAddonPlugin.Log.LogDebug((object)("[DoorDiag] recv diag failed: " + ex.Message));
		}
	}
}
