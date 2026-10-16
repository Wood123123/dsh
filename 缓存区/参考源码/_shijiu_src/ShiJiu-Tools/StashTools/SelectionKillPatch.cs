using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

[HarmonyPatch(typeof(ItemView), "Kill")]
internal static class SelectionKillPatch
{
	private static void Prefix(ItemView __instance)
	{
		string[] array = new string[2] { "StashToolsSelectedMark", "StashToolsSelectedBackground" };
		foreach (string text in array)
		{
			Transform val = ((Component)__instance).transform.Find(text);
			if ((Object)(object)val != (Object)null)
			{
				((Component)val).gameObject.SetActive(false);
			}
		}
	}
}
