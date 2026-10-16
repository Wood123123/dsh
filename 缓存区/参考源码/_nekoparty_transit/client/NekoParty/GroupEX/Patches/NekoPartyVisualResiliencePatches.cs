using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Matchmaker;
using Fika.Core.Main.Utils;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyVisualResiliencePatches
{
	private static bool _transliterateNullLogged;

	internal static void Apply(Harmony harmony)
	{
		if (!NekoGroupExConfig.DisableVisualResiliencePatches)
		{
			TryAddFinalizer(harmony, AccessTools.Method(typeof(InventoryEquipmentDescriptor), "method_0", (Type[])null, (Type[])null));
			TryAddFinalizer(harmony, AccessTools.Method(typeof(GroupPlayer), "method_1", (Type[])null, (Type[])null));
			TryAddNotificationEnumGuard(harmony);
			TryAddTransliterateGuard(harmony);
			TryAddVisualDump(harmony);
			TryAddVisualUpdateDiag(harmony);
			TryAddMatchmakerPreviewGuard(harmony);
		}
	}

	private static void TryAddMatchmakerPreviewGuard(Harmony harmony)
	{
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Expected O, but got Unknown
		if (!NekoGroupExConfig.DisableMatchmakerPlayerModels)
		{
			return;
		}
		MethodInfo methodInfo = null;
		MethodInfo[] methods = typeof(MatchMakerPlayerPreview).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (MethodInfo methodInfo2 in methods)
		{
			if (methodInfo2.Name == "Show" && methodInfo2.GetParameters().Length == 4)
			{
				ParameterInfo[] parameters = methodInfo2.GetParameters();
				if (parameters[2].ParameterType.Name == "RaidPlayer" && parameters[3].ParameterType.Name.Contains("RaidGroupContextInteractions"))
				{
					methodInfo = methodInfo2;
					break;
				}
			}
		}
		if (methodInfo == null)
		{
			FikaGlobals.LogWarning("[NekoParty] MatchMakerPlayerPreview.Show 未找到，模型屏蔽实验跳过", "PreviewGuard");
			return;
		}
		harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(NekoPartyVisualResiliencePatches), "SkipMatchmakerPreviewShow", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		FikaGlobals.LogWarning("[NekoParty] 【实验】已屏蔽匹配/准备界面 MatchMakerPlayerPreview.Show（不加载 3D 模型），验证准备界面冻结是否与模型渲染有关", "PreviewGuard");
	}

	internal static bool SkipMatchmakerPreviewShow(ref Task __result)
	{
		if (!NekoGroupExConfig.DisableMatchmakerPlayerModels)
		{
			return true;
		}
		__result = Task.CompletedTask;
		return false;
	}

	private static void TryAddVisualUpdateDiag(Harmony harmony)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		MethodBase methodBase = AccessTools.Method(typeof(RaidPlayer), "UpdateVisualRepresentation", (Type[])null, (Type[])null);
		if (methodBase == null)
		{
			FikaGlobals.LogWarning("[NekoParty] RaidPlayer.UpdateVisualRepresentation 未找到", "VisualUpdDiag");
		}
		else
		{
			harmony.Patch(methodBase, new HarmonyMethod(typeof(NekoPartyVisualResiliencePatches), "VisualUpdatePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	internal static void VisualUpdatePrefix(RaidPlayer __instance, object playerVisualRepresentation)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		try
		{
			PlayerVisualRepresentation val = (PlayerVisualRepresentation)((playerVisualRepresentation is PlayerVisualRepresentation) ? playerVisualRepresentation : null);
			string text = ((val == null) ? (playerVisualRepresentation?.ToString() ?? "null") : JsonConvert.SerializeObject((object)new PlayerVisualRepresentationDescriptor(val)));
			if (text != null && text.Length > 4000)
			{
				text = text.Substring(0, 4000);
			}
			FikaGlobals.LogWarning($"[NekoP2P] VisualUpdate accountId={((GroupPlayer)(__instance?)).AccountId} id={((GroupPlayer)(__instance?)).Id} visual={text}\n{new StackTrace()}", "VisualUpdDiag");
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("[NekoP2P] VisualUpdate diag fail: " + ex.Message, "VisualUpdDiag");
		}
	}

	private static void TryAddVisualDump(Harmony harmony)
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		MethodBase methodBase = AccessTools.Method(typeof(PlayerModelView), "Show", new Type[6]
		{
			typeof(PlayerVisualRepresentation),
			typeof(InventoryController),
			typeof(Action),
			typeof(float),
			typeof(Vector3?),
			typeof(bool)
		}, (Type[])null);
		if (methodBase == null)
		{
			FikaGlobals.LogWarning("[NekoParty] PlayerModelView.Show(PlayerVisualRepresentation) 未找到，视觉诊断跳过", "VisualShowDiag");
		}
		else
		{
			harmony.Patch(methodBase, new HarmonyMethod(typeof(NekoPartyVisualResiliencePatches), "VisualShowPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	internal static void VisualShowPrefix(object playerVisual)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		if (playerVisual == null)
		{
			return;
		}
		try
		{
			PlayerVisualRepresentation val = (PlayerVisualRepresentation)((playerVisual is PlayerVisualRepresentation) ? playerVisual : null);
			string text = ((val == null) ? JsonConvert.SerializeObject(playerVisual) : JsonConvert.SerializeObject((object)new PlayerVisualRepresentationDescriptor(val)));
			if (text != null && text.Length > 6000)
			{
				text = text.Substring(0, 6000);
			}
			FikaGlobals.LogWarning("[NekoP2P] PlayerModelView.Show visual=" + text + "\n" + new StackTrace(), "VisualShowDiag");
		}
		catch (Exception ex)
		{
			FikaGlobals.LogWarning("[NekoP2P] VisualShow diag fail: " + ex.Message, "VisualShowDiag");
		}
	}

	private static void TryAddNotificationEnumGuard(Harmony harmony)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		MethodInfo methodInfo = AccessTools.Method(typeof(EnumConverter<ENotificationType>), "ReadJson", (Type[])null, (Type[])null);
		if (!(methodInfo == null))
		{
			harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(NekoPartyVisualResiliencePatches), "NotificationEnumReadPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	internal static bool NotificationEnumReadPrefix(JsonReader reader, JsonSerializer serializer, EnumConverter<ENotificationType> __instance, ref object __result)
	{
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		string text = serializer.Deserialize<string>(reader);
		if (!string.IsNullOrEmpty(text))
		{
			bool value = Traverse.Create((object)__instance).Field("_caseSensitive").GetValue<bool>();
			Dictionary<string, ENotificationType> value2 = Traverse.Create((object)__instance).Field("_table").GetValue<Dictionary<string, ENotificationType>>();
			if (value2 != null && value2.TryGetValue(value ? text : text.ToLower(), out var value3))
			{
				__result = value3;
				return false;
			}
			if (int.TryParse(text, out var result) && Enum.IsDefined(typeof(ENotificationType), result))
			{
				__result = (object)(ENotificationType)result;
				return false;
			}
		}
		__result = (object)(ENotificationType)0;
		return false;
	}

	private static void TryAddTransliterateGuard(Harmony harmony)
	{
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		MethodInfo methodInfo = AccessTools.Method(typeof(Transliteration), "Transliterate", new Type[2]
		{
			typeof(string),
			typeof(string)
		}, (Type[])null);
		if (!(methodInfo == null))
		{
			harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(NekoPartyVisualResiliencePatches), "TransliteratePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	internal static bool TransliteratePrefix(string text, ref string __result)
	{
		if (!string.IsNullOrEmpty(text))
		{
			return true;
		}
		if (NekoGroupExConfig.UseNativePartyInvite && !_transliterateNullLogged)
		{
			_transliterateNullLogged = true;
			FikaGlobals.LogWarning($"[NekoParty] Transliterate 收到 null/空昵称(原生在中文 locale 下会 NRE，此处兜底避免渲染中断)，调用栈：\n{new StackTrace()}", "TransliteratePrefix");
		}
		__result = text ?? string.Empty;
		return false;
	}

	private static void TryAddFinalizer(Harmony harmony, MethodBase target)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		if (!(target == null))
		{
			harmony.Patch(target, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(NekoPartyVisualResiliencePatches), "Finalizer", (Type[])null), (HarmonyMethod)null);
		}
	}

	internal static Exception Finalizer(Exception __exception, MethodBase __originalMethod)
	{
		if (__exception == null || !NekoGroupExConfig.UseNativePartyInvite)
		{
			return __exception;
		}
		FikaGlobals.LogWarning("[NekoParty] 已吞反序列化回调异常 @ " + __originalMethod?.DeclaringType?.Name + "." + __originalMethod?.Name + " 类型=" + __exception.GetType().Name + " 消息=" + __exception.Message + "（吞掉以保证整队数据可用，避免 method_44 放弃整队）", "Finalizer");
		return null;
	}
}
