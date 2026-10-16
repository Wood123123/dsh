using System;
using System.Linq;
using System.Reflection;
using CookingGrenades.Config;
using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using TMPro;

namespace CookingGrenades.Patches;

public class MenuScreenPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		Type type = PatchConstants.EftTypes.FirstOrDefault((Type t) => !t.IsGenericTypeDefinition && t.GetMethods(BindingFlags.Instance | BindingFlags.Public).Any((MethodInfo m) => m.Name == "ShowScreen" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(EMenuType) && m.GetParameters()[1].ParameterType == typeof(bool)));
		if (type == null)
		{
			Plugin.log.LogError((object)"Failed to find MainMenuController type with ShowScreen(EMenuType, bool)");
			return null;
		}
		return AccessTools.Method(type, "ShowScreen", new Type[2]
		{
			typeof(EMenuType),
			typeof(bool)
		}, (Type[])null);
	}

	[PatchPostfix]
	public static void PatchPostfix(object __instance)
	{
		if (!ConfigManager.UserWarningConfirmed.Value)
		{
			string text;
			string text2;
			if (IsChineseLanguage())
			{
				text = "温雷警告";
				text2 = "[警告]\n温雷（拔销后在手中延时持握）在现实中极度危险！\n引信延时并非绝对精确，手雷可能早于预设时间起爆。\n拔出拉环、保险销后，切勿松开保险杆——否则手雷可能在手中提前爆炸，造成重伤甚至死亡。\n你是否已阅读并理解此警告，并愿意自行承担相关责任？";
			}
			else
			{
				text = "Cooking Grenades Warning";
				text2 = "[WARNING]\nCooking a grenade is extremely dangerous in real life. Time-delay setting may vary and fuzes may function before prescribed times. DO NOT \"COOK OFF\" the safety lever after pull ring with safety pin extraction. This action can lead to premature detonation of the grenade leading to severe injury, death.\nDo you acknowledge this warning and accept responsibility?";
			}
			ItemUiContext.Instance.ShowMessageWindow(text2, (Action)delegate
			{
				ConfigManager.UserWarningConfirmed.Value = true;
			}, (Action)delegate
			{
				ConfigManager.UserWarningConfirmed.Value = false;
			}, text, 0f, false, (TextAlignmentOptions)514);
		}
	}

	private static bool IsChineseLanguage()
	{
		try
		{
			LocalizationManager instance = LocalizationManager.Instance;
			string text = ((instance != null) ? instance.Culture : null);
			return !string.IsNullOrEmpty(text) && text.StartsWith("ch", StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}
}
