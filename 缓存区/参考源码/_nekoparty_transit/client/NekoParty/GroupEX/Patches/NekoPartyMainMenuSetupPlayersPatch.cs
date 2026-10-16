using System.Linq;
using System.Reflection;
using Fika.Core.Networking.Models.Presence;
using Fika.Core.UI.Custom;
using HarmonyLib;
using NekoParty.GroupEX.UI;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch]
public static class NekoPartyMainMenuSetupPlayersPatch
{
	private static MethodBase TargetMethod()
	{
		return typeof(MainMenuUIScript).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).First((MethodInfo method) => method.Name == "SetupPlayers");
	}

	private static void Postfix(MainMenuUIScript __instance, ref FikaPlayerPresence[] responses)
	{
		NekoPartyMainMenuController obj = ((Component)__instance).GetComponent<NekoPartyMainMenuController>() ?? ((Component)__instance).gameObject.AddComponent<NekoPartyMainMenuController>();
		obj.Initialize(__instance);
		obj.RefreshPlayerRows(responses);
	}
}
