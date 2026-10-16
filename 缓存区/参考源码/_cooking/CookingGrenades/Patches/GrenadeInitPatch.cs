using System;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

public class GrenadeInitPatch : ModulePatch
{
	private static readonly FieldInfo ImpactFuseTimeField = AccessTools.Field(typeof(Grenade), "_timeSpent");

	protected override MethodBase GetTargetMethod()
	{
		MethodInfo[] array = (from m in AccessTools.GetDeclaredMethods(typeof(Grenade))
			where m.Name == "Init"
			select m).ToArray();
		Plugin.log.LogInfo((object)$"[GrenadeInit] 找到 {array.Length} 个 Init 方法:");
		MethodInfo[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			ParameterInfo[] parameters = array2[num].GetParameters();
			Plugin.log.LogInfo((object)("  - Init(" + string.Join(", ", parameters.Select((ParameterInfo p) => p.ParameterType.Name + " " + p.Name)) + ")"));
		}
		array2 = array;
		foreach (MethodInfo methodInfo in array2)
		{
			ParameterInfo[] parameters2 = methodInfo.GetParameters();
			if (parameters2.Length >= 1 && parameters2.Any((ParameterInfo p) => p.ParameterType == typeof(float)))
			{
				Plugin.log.LogInfo((object)("[GrenadeInit] 选择: Init(" + string.Join(", ", parameters2.Select((ParameterInfo p) => p.ParameterType.Name)) + ")"));
				return methodInfo;
			}
		}
		if (array.Length != 0)
		{
			return array[0];
		}
		Plugin.log.LogError((object)"[GrenadeInit] 未找到 Init 方法");
		return null;
	}

	[PatchPrefix]
	public static void PatchPrefix(Grenade __instance, object[] __args, MethodBase __originalMethod)
	{
		ParameterInfo[] parameters = __originalMethod.GetParameters();
		ThrowWeap val = null;
		int num = -1;
		for (int i = 0; i < parameters.Length && i < __args.Length; i++)
		{
			if (parameters[i].ParameterType == typeof(ThrowWeap))
			{
				object obj = __args[i];
				ThrowWeap val2 = (ThrowWeap)((obj is ThrowWeap) ? obj : null);
				if (val2 != null)
				{
					val = val2;
				}
			}
			if (parameters[i].ParameterType == typeof(float) && num < 0)
			{
				num = i;
			}
		}
		if (val != null)
		{
			GrenadeWheel.PreferredTemplateId = ((Item)val).StringTemplateId;
		}
		GrenadeCookingTimer cookingTimer = GrenadeCookingManager.GetCookingTimer();
		if (cookingTimer.IsCooking && val != null)
		{
			float getExplDelay = val.GetExplDelay;
			float cookingTime = cookingTimer.GetCookingTime();
			float num2 = Math.Max(0.1f, getExplDelay - cookingTime);
			Plugin.log.LogInfo((object)$"[GrenadeInit] 温雷注入: 原始引信={getExplDelay:F2}s, 已烹饪={cookingTime:F2}s, 新引信={num2:F2}s");
			ThrowWeapGetExplDelayPatch.SetExplDelay(val, num2);
			CGEvents.Fire(cookingTime);
			GrenadeHandsController controller = cookingTimer.Controller;
			if ((Object)(object)controller != (Object)null)
			{
				cookingTimer.Reset(controller);
			}
		}
		else
		{
			float num3 = ((num >= 0) ? ((float)__args[num]) : 0f);
			Plugin.log.LogInfo((object)$"[GrenadeInit] 正常初始化 timeSpent={num3:F2}s");
		}
	}

	[PatchPostfix]
	public static void PatchPostfix(Grenade __instance, object[] __args, MethodBase __originalMethod)
	{
		if (ImpactFuseTimeField == null)
		{
			return;
		}
		ParameterInfo[] parameters = __originalMethod.GetParameters();
		for (int i = 0; i < parameters.Length && i < __args.Length; i++)
		{
			if (parameters[i].ParameterType == typeof(float) && __args[i] is float)
			{
				ImpactFuseTimeField.SetValue(__instance, (float)__args[i]);
				break;
			}
		}
	}
}
