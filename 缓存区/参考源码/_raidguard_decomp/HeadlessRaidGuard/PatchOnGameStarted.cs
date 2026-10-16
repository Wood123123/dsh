using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
using RAID_REVIEW;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeadlessRaidGuard;

[HarmonyPatch(typeof(GameWorld), "OnGameStarted")]
public static class PatchOnGameStarted
{
	private static ManualLogSource Log;

	private static bool _swallowedLogged;

	private static bool _skippedLogged;

	private const string OwnPrefix = "com.headless.raidguard";

	private static readonly HashSet<MethodBase> _wrapped = new HashSet<MethodBase>();

	private static bool? _headless;

	[HarmonyPriority(800)]
	[HarmonyPrefix]
	public static bool Prefix(GameWorld __instance)
	{
		try
		{
			WrapForeignPostfixes();
			if (!IsHeadlessProcess())
			{
				return true;
			}
			if (OnIcebreakerMap())
			{
				return true;
			}
			if ((Object)(object)__instance == (Object)null)
			{
				return true;
			}
			if ((Object)(object)__instance.MainPlayer != (Object)null)
			{
				return true;
			}
			if (!_skippedLogged)
			{
				_skippedLogged = true;
				(Log ?? (Log = Logger.CreateLogSource("HeadlessRaidGuard"))).LogWarning((object)("[HeadlessRaidGuard] headless host has no local player: skipping EFT GameWorld.OnGameStarted body (map: " + MapName() + ") so all postfixes (e.g. RaidReview START) run normally"));
			}
			return false;
		}
		catch
		{
			return true;
		}
	}

	private static void WrapForeignPostfixes()
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		try
		{
			if (!IsHeadlessProcess() || OnIcebreakerMap())
			{
				return;
			}
			MethodInfo methodInfo = AccessTools.Method(typeof(GameWorld), "OnGameStarted", (Type[])null, (Type[])null);
			if (methodInfo == null)
			{
				return;
			}
			Patches patchInfo = Harmony.GetPatchInfo((MethodBase)methodInfo);
			if (patchInfo?.Postfixes == null)
			{
				return;
			}
			Harmony val = new Harmony("com.headless.raidguard.postfixairbag");
			HarmonyMethod val2 = new HarmonyMethod(AccessTools.Method(typeof(PatchOnGameStarted), "PostfixAirbag", (Type[])null, (Type[])null));
			foreach (Patch postfix in patchInfo.Postfixes)
			{
				if (!(postfix.owner ?? "").StartsWith("com.headless.raidguard", StringComparison.OrdinalIgnoreCase) && !IsRaidReviewPatch(postfix) && _wrapped.Add(postfix.PatchMethod))
				{
					try
					{
						val.Patch((MethodBase)postfix.PatchMethod, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, val2, (HarmonyMethod)null);
						(Log ?? (Log = Logger.CreateLogSource("HeadlessRaidGuard"))).LogInfo((object)("[HeadlessRaidGuard] airbag on " + postfix.PatchMethod.DeclaringType?.FullName + "." + postfix.PatchMethod.Name + " (owner " + postfix.owner + ")"));
					}
					catch (Exception ex)
					{
						(Log ?? (Log = Logger.CreateLogSource("HeadlessRaidGuard"))).LogDebug((object)("[HeadlessRaidGuard] couldnt wrap " + postfix.PatchMethod.Name + " of " + postfix.owner + ": " + ex.Message));
					}
				}
			}
		}
		catch (Exception ex2)
		{
			(Log ?? (Log = Logger.CreateLogSource("HeadlessRaidGuard"))).LogWarning((object)("[HeadlessRaidGuard] postfix sweep failed: " + ex2.Message));
		}
	}

	private static bool IsRaidReviewPatch(Patch p)
	{
		try
		{
			if (!string.IsNullOrEmpty(p.owner) && p.owner.IndexOf("RAID_REVIEW", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			string text = p.PatchMethod?.DeclaringType?.Assembly?.GetName()?.Name;
			if (!string.IsNullOrEmpty(text) && text.IndexOf("RAID_REVIEW", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private static Exception PostfixAirbag(Exception __exception, MethodBase __originalMethod)
	{
		try
		{
			if (__exception == null)
			{
				return null;
			}
			if (!IsHeadlessProcess())
			{
				return __exception;
			}
			if (OnIcebreakerMap())
			{
				return __exception;
			}
			string text = __originalMethod?.DeclaringType?.Assembly.GetName().Name ?? "unknown";
			(Log ?? (Log = Logger.CreateLogSource("HeadlessRaidGuard"))).LogWarning((object)("[HeadlessRaidGuard] " + text + "'s hook " + __originalMethod?.DeclaringType?.Name + "." + __originalMethod?.Name + " threw on a headless host (likely no MainPlayer) — swallowed; only THAT mod's OnGameStarted hook is skipped, Raid Review and the other mods' hooks still run. inner: " + __exception.Message));
			return null;
		}
		catch
		{
			return __exception;
		}
	}

	[HarmonyFinalizer]
	public static Exception Finalizer(Exception __exception)
	{
		try
		{
			if (__exception != null)
			{
				if (!IsHeadlessProcess())
				{
					return __exception;
				}
				if (OnIcebreakerMap())
				{
					return __exception;
				}
				if (!_swallowedLogged)
				{
					_swallowedLogged = true;
					(Log ?? (Log = Logger.CreateLogSource("HeadlessRaidGuard"))).LogWarning((object)("[HeadlessRaidGuard] swallowed exception in GameWorld.OnGameStarted on headless host " + $"(map: {MapName()}) so the raid can start. Full exception (once):\n{__exception}"));
				}
				TryRelayRaidReviewStart();
				return null;
			}
			return null;
		}
		catch
		{
			return __exception;
		}
	}

	private static void TryRelayRaidReviewStart()
	{
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Invalid comparison between Unknown and I4
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (global::RAID_REVIEW.RAID_REVIEW.inRaid)
			{
				return;
			}
			GameWorld gameWorld = global::RAID_REVIEW.RAID_REVIEW.gameWorld;
			if ((Object)(object)gameWorld == (Object)null || gameWorld.LocationId == "hideout")
			{
				return;
			}
			if (global::RAID_REVIEW.RAID_REVIEW.sessionId == null)
			{
				string text = null;
				try
				{
					ref MongoID? currentProfileId = ref gameWorld.CurrentProfileId;
					text = (currentProfileId.HasValue ? ((object)currentProfileId.GetValueOrDefault()/*cast due to constrained. prefix*/).ToString() : null);
				}
				catch
				{
				}
				if (text == null)
				{
					try
					{
						Player myPlayer = global::RAID_REVIEW.RAID_REVIEW.myPlayer;
						text = ((myPlayer != null) ? myPlayer.ProfileId : null);
					}
					catch
					{
					}
				}
				if (text == null)
				{
					try
					{
						Player mainPlayer = gameWorld.MainPlayer;
						text = ((mainPlayer != null) ? mainPlayer.ProfileId : null);
					}
					catch
					{
					}
				}
				global::RAID_REVIEW.RAID_REVIEW.sessionId = text;
			}
			global::RAID_REVIEW.RAID_REVIEW.stopwatch.Reset();
			global::RAID_REVIEW.RAID_REVIEW.stopwatch.Start();
			string type = "PMC";
			try
			{
				if ((Object)(object)global::RAID_REVIEW.RAID_REVIEW.myPlayer != (Object)null && (int)global::RAID_REVIEW.RAID_REVIEW.myPlayer.Side == 4)
				{
					type = "SCAV";
				}
			}
			catch
			{
			}
			Telemetry.Send("START", JsonConvert.SerializeObject((object)(global::RAID_REVIEW.RAID_REVIEW.trackingRaid = new TrackingRaid
			{
				sessionId = global::RAID_REVIEW.RAID_REVIEW.sessionId,
				profileId = global::RAID_REVIEW.RAID_REVIEW.sessionId,
				time = DateTime.Now,
				detectedMods = ((global::RAID_REVIEW.RAID_REVIEW.RAID_REVIEW__DETECTED_MODS.Count > 0) ? string.Join(",", global::RAID_REVIEW.RAID_REVIEW.RAID_REVIEW__DETECTED_MODS) : ""),
				location = gameWorld.LocationId,
				type = type,
				timeInRaid = 0L
			})));
			if (global::RAID_REVIEW.RAID_REVIEW.sessionId != null)
			{
				TrackingPlayer trackingPlayer = new TrackingPlayer
				{
					sessionId = global::RAID_REVIEW.RAID_REVIEW.sessionId,
					profileId = global::RAID_REVIEW.RAID_REVIEW.sessionId,
					name = "Headless Host",
					level = 0,
					team = (EPlayerSide)1,
					group = 0,
					spawnTime = 0L,
					type = "HUMAN",
					mod_SAIN_brain = "PLAYER",
					mod_SAIN_difficulty = ""
				};
				global::RAID_REVIEW.RAID_REVIEW.trackingPlayers[trackingPlayer.profileId] = trackingPlayer;
			}
			global::RAID_REVIEW.RAID_REVIEW.inRaid = true;
			(Log ?? (Log = Logger.CreateLogSource("HeadlessRaidGuard"))).LogInfo((object)("[HeadlessRaidGuard] relayed Raid Review START (map: " + gameWorld.LocationId + ", session: " + global::RAID_REVIEW.RAID_REVIEW.sessionId + ")"));
		}
		catch (Exception ex)
		{
			(Log ?? (Log = Logger.CreateLogSource("HeadlessRaidGuard"))).LogWarning((object)("[HeadlessRaidGuard] Raid Review START relay failed: " + ex.Message));
		}
	}

	private static string MapName()
	{
		try
		{
			GameWorld instance = Singleton<GameWorld>.Instance;
			return ((Object)(object)instance != (Object)null) ? (instance.LocationId ?? "<null>") : "<no world>";
		}
		catch
		{
			return "<unknown>";
		}
	}

	private static bool OnIcebreakerMap()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				Scene sceneAt = SceneManager.GetSceneAt(i);
				if (((Scene)(ref sceneAt)).isLoaded && ((Scene)(ref sceneAt)).name != null && ((Scene)(ref sceneAt)).name.StartsWith("Icebreaker"))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static bool IsHeadlessProcess()
	{
		if (_headless.HasValue)
		{
			return _headless.Value;
		}
		bool value = false;
		try
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				string text = assemblies[i]?.GetName()?.Name;
				if (text != null && text.IndexOf("Fika.Headless", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					value = true;
					break;
				}
			}
		}
		catch
		{
		}
		_headless = value;
		return _headless.Value;
	}
}
