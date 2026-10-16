using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.HandBook;
using EFT.HealthSystem;
using EFT.Interactive;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using SPT.Reflection.Patching;
using UnityEngine;

namespace RAID_REVIEW;

[BepInPlugin("ekky.raidreview", "Raid Review", "1.6.1")]
[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInDependency(/*Could not decode attribute arguments.*/)]
[BepInDependency(/*Could not decode attribute arguments.*/)]
public class RAID_REVIEW : BaseUnityPlugin
{
	public static float PlayerTrackingInterval = 5f;

	public static string sessionId = null;

	public static bool inRaid = false;

	public static bool tracking = false;

	public static bool WebSocketConnected = false;

	public static string RAID_REVIEW_WS_Server = "ws://127.0.0.1:7828";

	public static string RAID_REVIEW_HTTP_Server = "http://127.0.0.1:7829";

	public static List<string> RAID_REVIEW__DETECTED_MODS = new List<string>();

	public static Dictionary<string, TrackingPlayer> trackingPlayers = new Dictionary<string, TrackingPlayer>();

	public static TrackingRaid trackingRaid = new TrackingRaid();

	public static Stopwatch stopwatch = new Stopwatch();

	public static GameWorld gameWorld;

	public static RaidSettings raid;

	public static Player myPlayer;

	public static ConfigEntry<KeyboardShortcut> LaunchWebpageKey;

	public static ConfigEntry<bool> PlayerTracking;

	public static ConfigEntry<bool> InsertMenuItem;

	public static ConfigEntry<bool> RecordingNotification;

	public static ConfigEntry<bool> KillTracking;

	public static ConfigEntry<bool> LootTracking;

	public static ConfigEntry<bool> BallisticsTracking;

	public static ConfigEntry<string> ServerAddress;

	public static ConfigEntry<string> ServerWsPort;

	public static ConfigEntry<string> ServerHttpPort;

	public static ConfigEntry<bool> EnableRecording;

	public static ConfigEntry<bool> ServerTLS;

	public static ConfigEntry<bool> EnableLegacyPhobos;

	public static ConfigEntry<bool> EnableLegacyQuestingBots;

	public static GameObject Hook;

	public static bool MODS_SEARCHED = false;

	public static bool searchingForSainComponents = false;

	public static Dictionary<string, TrackingPlayer> updatedBots = new Dictionary<string, TrackingPlayer>();

	public static Dictionary<string, string> _lastBotQuestState = new Dictionary<string, string>();

	public static Dictionary<string, string> _lastBotObjectiveState = new Dictionary<string, string>();

	private static HashSet<string> _seenLayerNames = new HashSet<string>();

	private static bool _sainReflectionInit = false;

	private static bool _sainAvailable = false;

	private static Type _sainBotComponentType;

	private static Type _sainBotControllerType;

	private static PropertyInfo _sainActiveLayerProp;

	private static PropertyInfo _sainDecisionProp;

	private static PropertyInfo _sainCombatDecProp;

	private static PropertyInfo _sainSelfDecProp;

	private static PropertyInfo _sainSquadDecProp;

	private static PropertyInfo _sainCurrentActionProp;

	private static PropertyInfo _sainActionNameProp;

	private static PropertyInfo _sainBotsProp;

	private static PropertyInfo _sainBotPlayerProp;

	private static PropertyInfo _sainBotProfileIdProp;

	private static PropertyInfo _sainMoverProp;

	private static PropertyInfo _sainMoverMovingProp;

	private static PropertyInfo _sainMoverSprintProp;

	private static PropertyInfo _sainStandByProp;

	private static PropertyInfo _sainLayersActiveProp;

	private static Dictionary<string, object> _sainBotCache = new Dictionary<string, object>();

	private static bool _looseLootCaptured = false;

	private static long _phobosFieldLastCapture = -999999L;

	private const long PhobosFieldIntervalMs = 30000L;

	private static long _orbitMainObjLastCapture = -999999L;

	private static int _orbitMainObjLastRevision = -1;

	private const long OrbitMainObjIntervalMs = 30000L;

	private static long _orbitFieldLastCapture = -999999L;

	private const long OrbitFieldIntervalMs = 30000L;

	public static bool SOLARINT_SAIN__DETECTED { get; set; }

	public static bool DANW_QUESTINGBOTS__DETECTED { get; set; }

	public static bool PHOBOS_LEGACY__DETECTED { get; set; }

	public static bool ORBIT__DETECTED { get; set; }

	public static object sainBotController { get; set; }

	public static bool IsHuntSpawn(Player player)
	{
		try
		{
			object obj;
			if (player == null)
			{
				obj = null;
			}
			else
			{
				IAIData aIData = player.AIData;
				if (aIData == null)
				{
					obj = null;
				}
				else
				{
					BotOwner botOwner = aIData.BotOwner;
					if (botOwner == null)
					{
						obj = null;
					}
					else
					{
						IGetProfileData spawnProfileData = botOwner.SpawnProfileData;
						obj = ((spawnProfileData == null) ? null : spawnProfileData.SpawnParams?.Id_spawn);
					}
				}
			}
			return ((string)obj)?.ToLower().Contains("hunt") ?? false;
		}
		catch
		{
			return false;
		}
	}

	public unsafe static string MapWildSpawnType(WildSpawnType role)
	{
		return ((object)(*(WildSpawnType*)(&role))/*cast due to constrained. prefix*/).ToString() switch
		{
			"assault" => "SCAV|SCAV", 
			"assaultGroup" => "SCAV GROUP|SCAV", 
			"marksman" => "SCAV SNIPER|SNIPER", 
			"cursedAssault" => "TAGGED AND CURSED SCAV|SCAV", 
			"bossKnight" => "KNIGHT|GOON", 
			"followerBigPipe" => "BIGPIPE|GOON", 
			"followerBirdEye" => "BIRDEYE|GOON", 
			"exUsec" => "ROGUE|FOLLOWER", 
			"pmcBot" => "RAIDER|FOLLOWER", 
			"arenaFighterEvent" => "BLOODHOUND|BLOODHOUND", 
			"sectantPriest" => "CULTIST PRIEST|CULT", 
			"sectantWarrior" => "CULTIST|CULT", 
			"bossKilla" => "KILLA|BOSS", 
			"bossBully" => "RASHALA|BOSS", 
			"followerBully" => "RASHALA GUARD|FOLLOWER", 
			"bossKojaniy" => "SHTURMAN|BOSS", 
			"followerKojaniy" => "SHTURMAN GUARD|FOLLOWER", 
			"bossTagilla" => "TAGILLA|BOSS", 
			"followerTagilla" => "TAGILLA GUARD|FOLLOWER", 
			"bossSanitar" => "SANITAR|BOSS", 
			"followerSanitar" => "SANITAR GUARD|FOLLOWER", 
			"bossGluhar" => "GLUHAR|BOSS", 
			"followerGluharSnipe" => "GLUHAR GUARD SNIPE|FOLLOWER", 
			"followerGluharScout" => "GLUHAR GUARD SCOUT|FOLLOWER", 
			"followerGluharSecurity" => "GLUHAR GUARD SECURITY|FOLLOWER", 
			"followerGluharAssault" => "GLUHAR GUARD ASSAULT|FOLLOWER", 
			"bossZryachiy" => "ZRYACHIY|BOSS", 
			"followerZryachiy" => "ZRYACHIY GUARD|FOLLOWER", 
			"bossBoar" => "KABAN|BOSS", 
			"followerBoar" => "KABAN GUARD|FOLLOWER", 
			"bossBoarSniper" => "KABAN SNIPER|FOLLOWER", 
			"bossKolontay" => "KOLONTAY|BOSS", 
			"followerKolontayAssault" => "KOLONTAY ASSAULT|FOLLOWER", 
			"followerKolontaySecurity" => "KOLONTAY SECURITY|FOLLOWER", 
			"bossPartisan" => "PARTIZAN|BOSS", 
			"shooterBTR" => "BTR|OTHER", 
			"bossTagillaAgro" => "SHADOW TAGILLA|BOSS", 
			"bossKillaAgro" => "VENGEFUL KILLA|BOSS", 
			"tagillaHelperAgro" => "SHADOW TAGILLA GUARD|FOLLOWER", 
			"infectedAssault" => "INFECTED|INFECTED", 
			"infectedPmc" => "INFECTED PMC|INFECTED", 
			"infectedCivil" => "INFECTED CIVILIAN|INFECTED", 
			"infectedLaborant" => "INFECTED LABORANT|INFECTED", 
			"infectedTagilla" => "INFECTED TAGILLA|BOSS", 
			"spiritWinter" => "SPIRIT WINTER|SPECIAL", 
			"spiritSpring" => "SPIRIT SPRING|SPECIAL", 
			"pmcBEAR" => "PMC|BEAR", 
			"pmcUSEC" => "PMC|USEC", 
			"sptBear" => "PMC|BEAR", 
			"sptUsec" => "PMC|USEC", 
			"mercenary" => "MERCENARY|MERCENARY", 
			"ruafRifleman" => "RUAF RIFLEMAN|RUAF", 
			"ruafRiflemanSenior" => "RUAF SENIOR RIFLEMAN|RUAF", 
			"ruafAutorifleman" => "RUAF AUTORIFLEMAN|RUAF", 
			"ruafGrenadier" => "RUAF GRENADIER|RUAF", 
			"ruafMarksman" => "RUAF MARKSMAN|RUAF", 
			"ruafMachinegunner" => "RUAF MACHINEGUNNER|RUAF", 
			"remnantRifleman" => "REMNANT RIFLEMAN|RUAF", 
			"remnantRiflemanSenior" => "REMNANT SENIOR RIFLEMAN|RUAF", 
			"remnantAutorifleman" => "REMNANT AUTORIFLEMAN|RUAF", 
			"remnantGrenadier" => "REMNANT GRENADIER|RUAF", 
			"remnantMarksman" => "REMNANT MARKSMAN|RUAF", 
			"remnantMachinegunner" => "REMNANT MACHINEGUNNER|RUAF", 
			"followeruntar" => "UNTAR GUARD|UNTAR", 
			"bossuntarlead" => "UNTAR SQUAD LEADER|UNTAR", 
			"followeruntarmarksman" => "UNTAR MARKSMAN|UNTAR", 
			"bossuntarofficer" => "UNTAR OFFICER|UNTAR", 
			"blackDivLead" => "BLACK DIV LEAD|BLACKDIV", 
			"blackDivAssault" => "BLACK DIV ASSAULT|BLACKDIV", 
			"blackDivBreacher" => "BLACK DIV BREACHER|BLACKDIV", 
			"blackDivSupport" => "BLACK DIV SUPPORT|BLACKDIV", 
			"ISBSpecialForces" => "ISB SPECIAL FORCES|ISB", 
			"ISBTeamLeader" => "ISB TEAM LEADER|ISB", 
			"ISBSecondLeader" => "ISB SECOND LEADER|ISB", 
			"ISBFirefly" => "ISB FIREFLY|ISB", 
			"ISBFireflyFollowerLoghan" => "ISB FIREFLY LOGHAN|ISB", 
			"ISBFireflyFollowerVipper" => "ISB FIREFLY VIPPER|ISB", 
			"ISBFireflyShielder01" => "ISB FIREFLY SHIELDER 1|ISB", 
			"ISBFireflyShielder02" => "ISB FIREFLY SHIELDER 2|ISB", 
			"ISBBossCommander" => "WHITE TUSK|ISB", 
			"ISBFollowerCommander" => "WHITE TUSK|ISB", 
			"wedge" => "WEDGE|BOSS", 
			"wedgeguard" => "WEDGE GUARD|FOLLOWER", 
			"CombineSoldier" => "COMBINE SOLDIER|COMBINE", 
			"CombineShotgunner" => "COMBINE SHOTGUNNER|COMBINE", 
			"CombineElite" => "COMBINE ELITE|COMBINE", 
			_ => ((object)(*(WildSpawnType*)(&role))/*cast due to constrained. prefix*/).ToString().ToUpper() + "|FACTION_MOD", 
		};
	}

	private static void InitSainReflection()
	{
		if (_sainReflectionInit)
		{
			return;
		}
		_sainReflectionInit = true;
		try
		{
			_sainBotComponentType = Type.GetType("SAIN.Components.BotComponent, SAIN");
			_sainBotControllerType = Type.GetType("SAIN.Components.BotManagerComponent, SAIN");
			if (_sainBotComponentType == null)
			{
				LoggerInstance.Log.LogWarning((object)"RAID_REVIEW :::: SAIN :::: BotComponent type not found");
				return;
			}
			_sainActiveLayerProp = _sainBotComponentType.GetProperty("ActiveLayer");
			_sainDecisionProp = _sainBotComponentType.GetProperty("Decision");
			_sainCurrentActionProp = _sainBotComponentType.GetProperty("CurrentAction");
			_sainBotPlayerProp = _sainBotComponentType.GetProperty("Player");
			if (_sainDecisionProp != null)
			{
				Type propertyType = _sainDecisionProp.PropertyType;
				_sainCombatDecProp = propertyType?.GetProperty("CurrentCombatDecision");
				_sainSelfDecProp = propertyType?.GetProperty("CurrentSelfDecision");
				_sainSquadDecProp = propertyType?.GetProperty("CurrentSquadDecision");
			}
			if (_sainCurrentActionProp != null)
			{
				_sainActionNameProp = _sainCurrentActionProp.PropertyType?.GetProperty("Name");
			}
			if (_sainBotControllerType != null)
			{
				_sainBotsProp = _sainBotControllerType.GetProperty("Bots");
			}
			if (_sainBotPlayerProp != null)
			{
				_sainBotProfileIdProp = _sainBotPlayerProp.PropertyType?.GetProperty("ProfileId");
			}
			_sainMoverProp = _sainBotComponentType.GetProperty("Mover");
			_sainStandByProp = _sainBotComponentType.GetProperty("BotInStandBy");
			_sainLayersActiveProp = _sainBotComponentType.GetProperty("SAINLayersActive");
			if (_sainMoverProp != null)
			{
				Type propertyType2 = _sainMoverProp.PropertyType;
				_sainMoverMovingProp = propertyType2?.GetProperty("Moving");
				_sainMoverSprintProp = propertyType2?.GetProperty("SprintController");
				if (_sainMoverSprintProp == null)
				{
					_sainMoverSprintProp = propertyType2?.GetProperty("Running");
				}
			}
			_sainAvailable = true;
			LoggerInstance.Log.LogInfo((object)("RAID_REVIEW :::: SAIN :::: Reflection init OK — " + $"Decision={_sainDecisionProp != null}, Combat={_sainCombatDecProp != null}, " + $"Self={_sainSelfDecProp != null}, Squad={_sainSquadDecProp != null}, " + $"Action={_sainCurrentActionProp != null}, ActionName={_sainActionNameProp != null}, " + $"ActiveLayer={_sainActiveLayerProp != null}, " + $"Controller={_sainBotControllerType != null}, Bots={_sainBotsProp != null}, " + $"Mover={_sainMoverProp != null}, Moving={_sainMoverMovingProp != null}, " + $"StandBy={_sainStandByProp != null}, LayersActive={_sainLayersActiveProp != null}"));
		}
		catch (Exception ex)
		{
			LoggerInstance.Log.LogWarning((object)("RAID_REVIEW :::: SAIN :::: Reflection init failed: " + ex.Message));
		}
	}

	private static void RefreshSainBotCache()
	{
		_sainBotCache.Clear();
		try
		{
			object obj = sainBotController;
			if (obj == null || _sainBotsProp == null)
			{
				return;
			}
			object value = _sainBotsProp.GetValue(obj);
			if (value == null)
			{
				return;
			}
			IEnumerable enumerable = (IEnumerable)(value.GetType().GetProperty("Values")?.GetValue(value));
			if (enumerable == null)
			{
				return;
			}
			foreach (object item in enumerable)
			{
				try
				{
					string text = null;
					if (_sainBotPlayerProp != null)
					{
						object value2 = _sainBotPlayerProp.GetValue(item);
						if (value2 != null && _sainBotProfileIdProp != null)
						{
							text = _sainBotProfileIdProp.GetValue(value2)?.ToString();
						}
					}
					if (!string.IsNullOrEmpty(text))
					{
						_sainBotCache[text] = item;
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static string ExtractDecisionFromBotComp(object botComp)
	{
		if (botComp == null)
		{
			return null;
		}
		if (_sainDecisionProp != null)
		{
			object value = _sainDecisionProp.GetValue(botComp);
			if (value != null)
			{
				if (_sainSelfDecProp != null)
				{
					string text = _sainSelfDecProp.GetValue(value)?.ToString();
					if (!string.IsNullOrEmpty(text) && text != "None" && text != "0")
					{
						return "SAIN:" + text;
					}
				}
				if (_sainCombatDecProp != null)
				{
					string text2 = _sainCombatDecProp.GetValue(value)?.ToString();
					if (!string.IsNullOrEmpty(text2) && text2 != "None" && text2 != "0")
					{
						return "SAIN:" + text2;
					}
				}
				if (_sainSquadDecProp != null)
				{
					string text3 = _sainSquadDecProp.GetValue(value)?.ToString();
					if (!string.IsNullOrEmpty(text3) && text3 != "None" && text3 != "0")
					{
						return "SAIN:" + text3;
					}
				}
			}
		}
		if (_sainCurrentActionProp != null && _sainActionNameProp != null)
		{
			object value2 = _sainCurrentActionProp.GetValue(botComp);
			if (value2 != null)
			{
				string text4 = _sainActionNameProp.GetValue(value2)?.ToString();
				if (!string.IsNullOrEmpty(text4))
				{
					return "SAIN:" + text4;
				}
			}
		}
		if (_sainActiveLayerProp != null)
		{
			string text5 = _sainActiveLayerProp.GetValue(botComp)?.ToString();
			if (!string.IsNullOrEmpty(text5) && text5 != "None" && text5 != "0")
			{
				return "SAIN:" + text5;
			}
		}
		try
		{
			if (_sainStandByProp != null)
			{
				object value3 = _sainStandByProp.GetValue(botComp);
				if (value3 is bool && (bool)value3)
				{
					return "standBy";
				}
			}
			if (_sainMoverProp != null && _sainMoverMovingProp != null)
			{
				object value4 = _sainMoverProp.GetValue(botComp);
				if (value4 != null)
				{
					object value5 = _sainMoverMovingProp.GetValue(value4);
					if (value5 is bool && (bool)value5)
					{
						return "simplePatrol";
					}
				}
			}
		}
		catch
		{
		}
		return "peaceful";
	}

	private static bool IsIdleOrPatrolDecision(string decision)
	{
		if (!string.IsNullOrEmpty(decision))
		{
			switch (decision)
			{
			default:
				return decision == "SAIN:Peace";
			case "peaceful":
			case "simplePatrol":
			case "standBy":
				break;
			}
		}
		return true;
	}

	private static string GetSainDecision(Player player)
	{
		if (!_sainAvailable)
		{
			return null;
		}
		try
		{
			if (_sainBotCache.TryGetValue(player.ProfileId, out var value))
			{
				string text = ExtractDecisionFromBotComp(value);
				if (text != null)
				{
					return text;
				}
			}
			if (_sainBotComponentType != null)
			{
				Component component = ((Component)player).gameObject.GetComponent(_sainBotComponentType);
				if ((Object)(object)component != (Object)null)
				{
					string text2 = ExtractDecisionFromBotComp(component);
					if (text2 != null)
					{
						return text2;
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static int GetHandbookPrice(Item item)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			return (int)Singleton<Handbook>.Instance.GetBasePrice(item.TemplateId);
		}
		catch
		{
			return 0;
		}
	}

	public static void ResetLooseLootFlag()
	{
		_looseLootCaptured = false;
	}

	public static void ResetPhobosFieldFlag()
	{
		_phobosFieldLastCapture = -999999L;
	}

	private void CaptureLegacyPhobosField()
	{
		if (!PHOBOS_LEGACY__DETECTED)
		{
			return;
		}
		long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
		if (elapsedMilliseconds - _phobosFieldLastCapture < 30000)
		{
			return;
		}
		try
		{
			TrackingPhobosField advectionFieldSnapshot = Phobos_Integration.GetAdvectionFieldSnapshot(sessionId, elapsedMilliseconds);
			if (advectionFieldSnapshot != null)
			{
				_phobosFieldLastCapture = elapsedMilliseconds;
				Telemetry.Send("PHOBOS_FIELD", JsonConvert.SerializeObject((object)advectionFieldSnapshot));
			}
		}
		catch (Exception ex)
		{
			_phobosFieldLastCapture = elapsedMilliseconds;
			((BaseUnityPlugin)this).Logger.LogWarning((object)("RAID_REVIEW :::: WARN :::: Phobos field capture failed: " + ex.Message));
		}
	}

	public static void ResetOrbitMainObjFlag()
	{
		_orbitMainObjLastCapture = -999999L;
		_orbitMainObjLastRevision = -1;
	}

	private void CaptureOrbitGhostFights()
	{
		if (!ORBIT__DETECTED)
		{
			return;
		}
		try
		{
			List<TrackingOrbitGhostFight> ghostFights = Orbit_Integration.GetGhostFights(sessionId, stopwatch.ElapsedMilliseconds);
			if (ghostFights == null)
			{
				return;
			}
			foreach (TrackingOrbitGhostFight item in ghostFights)
			{
				Telemetry.Send("ORBIT_GHOST_FIGHT", JsonConvert.SerializeObject((object)item));
			}
		}
		catch (Exception ex)
		{
			((BaseUnityPlugin)this).Logger.LogWarning((object)("RAID_REVIEW :::: WARN :::: ORBIT ghost-fight capture failed: " + ex.Message));
		}
	}

	private void CaptureOrbitMainObjectives()
	{
		if (!ORBIT__DETECTED)
		{
			return;
		}
		long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
		int mainObjectivesRevision = Orbit_Integration.GetMainObjectivesRevision();
		if (elapsedMilliseconds - _orbitMainObjLastCapture < 30000 && mainObjectivesRevision == _orbitMainObjLastRevision)
		{
			return;
		}
		try
		{
			TrackingOrbitMainObjectives mainObjectivesSnapshot = Orbit_Integration.GetMainObjectivesSnapshot(sessionId, elapsedMilliseconds);
			if (mainObjectivesSnapshot != null)
			{
				_orbitMainObjLastCapture = elapsedMilliseconds;
				_orbitMainObjLastRevision = mainObjectivesRevision;
				Telemetry.Send("ORBIT_MAIN_OBJECTIVES", JsonConvert.SerializeObject((object)mainObjectivesSnapshot));
			}
		}
		catch (Exception ex)
		{
			_orbitMainObjLastCapture = elapsedMilliseconds;
			_orbitMainObjLastRevision = mainObjectivesRevision;
			((BaseUnityPlugin)this).Logger.LogWarning((object)("RAID_REVIEW :::: WARN :::: ORBIT main-objectives capture failed: " + ex.Message));
		}
	}

	public static void ResetOrbitFieldFlag()
	{
		_orbitFieldLastCapture = -999999L;
	}

	private void CaptureOrbitField()
	{
		if (!ORBIT__DETECTED)
		{
			return;
		}
		long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
		if (elapsedMilliseconds - _orbitFieldLastCapture < 30000)
		{
			return;
		}
		try
		{
			TrackingOrbitField advectionFieldSnapshot = Orbit_Integration.GetAdvectionFieldSnapshot(sessionId, elapsedMilliseconds);
			if (advectionFieldSnapshot != null)
			{
				_orbitFieldLastCapture = elapsedMilliseconds;
				Telemetry.Send("ORBIT_FIELD", JsonConvert.SerializeObject((object)advectionFieldSnapshot));
			}
		}
		catch (Exception ex)
		{
			_orbitFieldLastCapture = elapsedMilliseconds;
			((BaseUnityPlugin)this).Logger.LogWarning((object)("RAID_REVIEW :::: WARN :::: ORBIT field capture failed: " + ex.Message));
		}
	}

	private void CaptureLooseLoot()
	{
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_0231: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		if (_looseLootCaptured || (Object)(object)gameWorld == (Object)null)
		{
			return;
		}
		_looseLootCaptured = true;
		try
		{
			List<TrackingLooseLootItem> list = new List<TrackingLooseLootItem>();
			HashSet<string> hashSet = new HashSet<string>();
			foreach (IKillable loot in gameWorld.LootList)
			{
				try
				{
					LootItem val = (LootItem)(object)((loot is LootItem) ? loot : null);
					if (val != null)
					{
						Item item = val.Item;
						if (item != null && hashSet.Add(item.Id))
						{
							Vector3 position = ((Component)val).transform.position;
							list.Add(new TrackingLooseLootItem
							{
								itemId = item.Id,
								templateId = ((object)item.TemplateId/*cast due to constrained. prefix*/).ToString(),
								itemName = LocalizationExtensions.LocalizedShortName(item),
								price = GetHandbookPrice(item),
								qty = item.StackObjectsCount,
								x = position.x,
								y = position.y,
								z = position.z,
								inContainer = false,
								containerName = ""
							});
						}
						continue;
					}
					LootableContainer val2 = (LootableContainer)(object)((loot is LootableContainer) ? loot : null);
					if (val2 == null)
					{
						continue;
					}
					ItemController itemOwner = val2.ItemOwner;
					Item val3 = ((itemOwner != null) ? itemOwner.RootItem : null);
					CompoundItem val4 = (CompoundItem)(object)((val3 is CompoundItem) ? val3 : null);
					if (val4 == null)
					{
						continue;
					}
					Vector3 position2 = ((Component)val2).transform.position;
					string containerName = "";
					try
					{
						containerName = LocalizationExtensions.LocalizedShortName(val3);
					}
					catch
					{
					}
					Grid[] grids = val4.Grids;
					for (int i = 0; i < grids.Length; i++)
					{
						foreach (Item item2 in grids[i].Items)
						{
							if (item2 != null && hashSet.Add(item2.Id))
							{
								list.Add(new TrackingLooseLootItem
								{
									itemId = item2.Id,
									templateId = ((object)item2.TemplateId/*cast due to constrained. prefix*/).ToString(),
									itemName = LocalizationExtensions.LocalizedShortName(item2),
									price = GetHandbookPrice(item2),
									qty = item2.StackObjectsCount,
									x = position2.x,
									y = position2.y,
									z = position2.z,
									inContainer = true,
									containerName = containerName
								});
							}
						}
					}
				}
				catch
				{
				}
			}
			if (list.Count > 0)
			{
				((BaseUnityPlugin)this).Logger.LogInfo((object)$"RAID_REVIEW :::: INFO :::: Captured {list.Count} loose loot items");
				TrackingLooseLoot trackingLooseLoot = new TrackingLooseLoot
				{
					sessionId = sessionId,
					time = stopwatch.ElapsedMilliseconds,
					items = list
				};
				Telemetry.Send("LOOSE_LOOT", JsonConvert.SerializeObject((object)trackingLooseLoot));
			}
		}
		catch (Exception ex)
		{
			((BaseUnityPlugin)this).Logger.LogWarning((object)("RAID_REVIEW :::: WARN :::: Loose loot capture failed: " + ex.Message));
		}
	}

	private void Awake()
	{
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0372: Unknown result type (might be due to invalid IL or missing references)
		//IL_037c: Expected O, but got Unknown
		((BaseUnityPlugin)this).Logger.LogInfo((object)"RAID_REVIEW :::: INFO :::: Mod Loaded");
		try
		{
			string location = Assembly.GetExecutingAssembly().Location;
			string directoryName = Path.GetDirectoryName(location);
			((BaseUnityPlugin)this).Logger.LogInfo((object)("RAID_REVIEW :::: INFO :::: Plugin directory: " + directoryName));
			if (directoryName != null)
			{
				string[] files = Directory.GetFiles(directoryName, "RAID_REVIEW*.dll");
				foreach (string text in files)
				{
					if (!string.Equals(text, location, StringComparison.OrdinalIgnoreCase))
					{
						((BaseUnityPlugin)this).Logger.LogInfo((object)("RAID_REVIEW :::: INFO :::: Removing legacy DLL: " + Path.GetFileName(text)));
						File.Delete(text);
					}
				}
			}
		}
		catch (Exception ex)
		{
			((BaseUnityPlugin)this).Logger.LogWarning((object)("RAID_REVIEW :::: WARN :::: Failed to clean up legacy DLLs: " + ex.Message));
		}
		LaunchWebpageKey = ((BaseUnityPlugin)this).Config.Bind<KeyboardShortcut>("Main", "Open Webpage Keybind", new KeyboardShortcut((KeyCode)286, Array.Empty<KeyCode>()), "Keybind to open the web client.");
		InsertMenuItem = ((BaseUnityPlugin)this).Config.Bind<bool>("Main", "Insert Menu Item", false, "Enables menu item to open the web client.");
		RecordingNotification = ((BaseUnityPlugin)this).Config.Bind<bool>("Main", "Recording Notification", true, "Enables notifications as recording starts and ends.");
		ServerAddress = ((BaseUnityPlugin)this).Config.Bind<string>("Server", "1. Server IP", "127.0.0.1", "IP address of the server.");
		ServerWsPort = ((BaseUnityPlugin)this).Config.Bind<string>("Server", "2. Server WS Port", "7828", "Listen port of the raid review websocket server.");
		ServerHttpPort = ((BaseUnityPlugin)this).Config.Bind<string>("Server", "3. Server HTTP Port", "7829", "Listen port of the raid review http server.");
		ServerTLS = ((BaseUnityPlugin)this).Config.Bind<bool>("Server", "4. TLS", false, "Enable if you are using an SSL Certificate infront of your http server.");
		EnableRecording = ((BaseUnityPlugin)this).Config.Bind<bool>("Server", "5. Enable Recording", true, "[WARNING] Only disable if you want to stop all data from being sent to raid review, requires restart.");
		PlayerTracking = ((BaseUnityPlugin)this).Config.Bind<bool>("Tracking Settings", "Player Tracking", true, "Enables location tracking of players and bots.");
		KillTracking = ((BaseUnityPlugin)this).Config.Bind<bool>("Tracking Settings", "Kill Tracking", true, "Enables location tracking of kills.");
		LootTracking = ((BaseUnityPlugin)this).Config.Bind<bool>("Tracking Settings", "Loot Tracking", true, "Enables location tracking of lootings.");
		BallisticsTracking = ((BaseUnityPlugin)this).Config.Bind<bool>("Tracking Settings", "Ballistics Tracking", true, "Enables location tracking of ballistics.");
		EnableLegacyPhobos = ((BaseUnityPlugin)this).Config.Bind<bool>("Legacy Integrations", "Enable legacy Phobos integration", true, "Captures bot-objective data from the legacy upstream Phobos mod (com.janky.phobos). NO SUPPORT — uses reflection (the mod doesn't expose a public API). Disable if you see errors.");
		EnableLegacyQuestingBots = ((BaseUnityPlugin)this).Config.Bind<bool>("Legacy Integrations", "Enable legacy QuestingBots integration", true, "Only used with QuestingBots older than 0.11.0 (newer versions use the official interop API, always enabled). NO SUPPORT — uses reflection. Disable if you see errors.");
		RAID_REVIEW_WS_Server = (ServerTLS.Value ? "wss://" : "ws://") + ServerAddress.Value + ((ServerWsPort.Value != "") ? (":" + ServerWsPort.Value) : "");
		RAID_REVIEW_HTTP_Server = (ServerTLS.Value ? "https://" : "http://") + ServerAddress.Value + ((ServerHttpPort.Value != "") ? (":" + ServerHttpPort.Value) : "");
		((BaseUnityPlugin)this).Logger.LogInfo((object)("RAID_REVIEW :::: INFO :::: Configured WS Server: " + RAID_REVIEW_WS_Server));
		((BaseUnityPlugin)this).Logger.LogInfo((object)("RAID_REVIEW :::: INFO :::: Configured HTTP Server: " + RAID_REVIEW_HTTP_Server));
		Hook = new GameObject();
		((BaseUnityPlugin)this).Logger.LogInfo((object)"RAID_REVIEW :::: INFO :::: Config Loaded");
		((ModulePatch)new RAID_REVIEW_Player_OnGameStartedPatch()).Enable();
		((ModulePatch)new RAID_REVIEW_Player_OnGameSessionEndPatch()).Enable();
		((ModulePatch)new RAID_REVIEW_menuTaskBar_setButtonsAvailablePatch()).Enable();
		((ModulePatch)new RAID_REVIEW_Player_OnBeenKilledByAggressorPatch()).Enable();
		((ModulePatch)new RAID_REVIEW_Player_OnItemAddedOrRemovedPatch()).Enable();
		((ModulePatch)new RAID_REVIEW_ClientGameWorld_ShotDelegatePatch()).Enable();
		((BaseUnityPlugin)this).Logger.LogInfo((object)"RAID_REVIEW :::: INFO :::: Patches Loaded");
		Telemetry.Connect(RAID_REVIEW_WS_Server);
		((BaseUnityPlugin)this).Logger.LogInfo((object)"RAID_REVIEW :::: INFO :::: Connected to backend");
		LoggerInstance.Log = ((BaseUnityPlugin)this).Logger;
		((MonoBehaviour)this).StartCoroutine(UpdateCoroutine());
	}

	private unsafe IEnumerator UpdateCoroutine()
	{
		Vector3 val2 = default(Vector3);
		while (true)
		{
			yield return (object)new WaitForSeconds(1f / PlayerTrackingInterval);
			try
			{
				KeyboardShortcut value = LaunchWebpageKey.Value;
				if (Input.GetKey(((KeyboardShortcut)(ref value)).MainKey))
				{
					Application.OpenURL(RAID_REVIEW_HTTP_Server);
				}
				if (!MapLoaded())
				{
					if (!inRaid)
					{
						continue;
					}
					try
					{
						tracking = false;
						inRaid = false;
						stopwatch.Stop();
						trackingRaid.exitName = trackingRaid.exitName ?? "UNKNOWN";
						trackingRaid.time = DateTime.Now;
						trackingRaid.timeInRaid = stopwatch.ElapsedMilliseconds;
						BotChecker.BotCheckLoop(endCheck: true);
						if (SOLARINT_SAIN__DETECTED)
						{
							SAIN_Integration.CheckForSainComponents(endCheck: true);
						}
						Telemetry.Send("PLAYER_CHECK", JsonConvert.SerializeObject((object)trackingPlayers.Values));
						Telemetry.Send("END", JsonConvert.SerializeObject((object)trackingRaid));
					}
					catch
					{
					}
					finally
					{
						trackingPlayers = new Dictionary<string, TrackingPlayer>();
						_lastBotQuestState.Clear();
						_lastBotObjectiveState.Clear();
						_seenLayerNames.Clear();
						sessionId = null;
						stopwatch.Reset();
						if (ORBIT__DETECTED)
						{
							Orbit_Integration.ResetSessionState();
						}
					}
					continue;
				}
				gameWorld = Singleton<GameWorld>.Instance;
				myPlayer = gameWorld?.MainPlayer;
				if ((Object)(object)gameWorld == (Object)null || gameWorld.LocationId == "hideout")
				{
					continue;
				}
				if (sessionId == null && (Object)(object)gameWorld != (Object)null)
				{
					ref MongoID? currentProfileId = ref gameWorld.CurrentProfileId;
					object obj2 = (currentProfileId.HasValue ? ((object)currentProfileId.GetValueOrDefault()/*cast due to constrained. prefix*/).ToString() : null);
					if (obj2 == null)
					{
						Player obj3 = myPlayer;
						obj2 = ((obj3 != null) ? obj3.ProfileId : null);
					}
					sessionId = (string)obj2;
				}
				if (!inRaid)
				{
					continue;
				}
				CaptureLooseLoot();
				IEnumerable<Player> allPlayersEverExisted = gameWorld.AllPlayersEverExisted;
				long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
				if (SOLARINT_SAIN__DETECTED)
				{
					InitSainReflection();
					RefreshSainBotCache();
				}
				if (DANW_QUESTINGBOTS__DETECTED)
				{
					QuestingBots_Integration.InitReflection();
				}
				if (PHOBOS_LEGACY__DETECTED)
				{
					Phobos_Integration.InitReflection();
					Phobos_Integration.RefreshAgentCache();
					CaptureLegacyPhobosField();
				}
				if (ORBIT__DETECTED)
				{
					Orbit_Integration.InitReflection();
					Orbit_Integration.RefreshAgentCache();
					CaptureOrbitField();
					CaptureOrbitMainObjectives();
					CaptureOrbitGhostFights();
				}
				foreach (Player item in allPlayersEverExisted)
				{
					if ((Object)(object)item == (Object)null)
					{
						continue;
					}
					TrackingPlayer value2 = new TrackingPlayer();
					if (!trackingPlayers.TryGetValue(item.ProfileId, out value2))
					{
						TrackingPlayer obj4 = new TrackingPlayer
						{
							sessionId = sessionId,
							profileId = item.ProfileId,
							name = item.Profile.Nickname,
							level = item.Profile.Info.Level,
							team = item.Side
						};
						int? obj5;
						if (item == null)
						{
							obj5 = null;
						}
						else
						{
							IAIData aIData = item.AIData;
							if (aIData == null)
							{
								obj5 = null;
							}
							else
							{
								BotOwner botOwner = aIData.BotOwner;
								if (botOwner == null)
								{
									obj5 = null;
								}
								else
								{
									BotsGroup botsGroup = botOwner.BotsGroup;
									obj5 = ((botsGroup != null) ? new int?(botsGroup.Id) : ((int?)null));
								}
							}
						}
						int? num = obj5;
						obj4.group = num.GetValueOrDefault();
						obj4.spawnTime = stopwatch.ElapsedMilliseconds;
						obj4.type = (item.IsAI ? "BOT" : "HUMAN");
						obj4.mod_SAIN_brain = "UNKNOWN";
						obj4.mod_SAIN_difficulty = "";
						obj4.mod_SAIN_name = "";
						value2 = obj4;
						if ((int)item.Side == 4)
						{
							value2.mod_SAIN_brain = "SCAV";
							value2.type = "SCAV|SCAV";
						}
						else if ((int)item.Side == 1 || (int)item.Side == 2)
						{
							value2.mod_SAIN_brain = "PMC";
						}
						if (item.IsAI)
						{
							try
							{
								WildSpawnType role = item.Profile.Info.Settings.Role;
								value2.type = MapWildSpawnType(role);
								if (((object)(*(WildSpawnType*)(&role))/*cast due to constrained. prefix*/).ToString() == "pmcBot" && IsHuntSpawn(item))
								{
									value2.type = "UNTAR HUNTER|UNTAR";
								}
								if (((object)(*(WildSpawnType*)(&role))/*cast due to constrained. prefix*/).ToString() == "bossPartisan")
								{
									value2.name = "Partizan";
								}
							}
							catch
							{
							}
						}
						trackingPlayers[value2.profileId] = value2;
						Telemetry.Send("PLAYER", JsonConvert.SerializeObject((object)value2));
						if (item.IsAI)
						{
							try
							{
								List<TrackingInventoryItem> list = new List<TrackingInventoryItem>();
								foreach (Item playerItem in item.Inventory.GetPlayerItems((EPlayerItems)2))
								{
									if (playerItem != null)
									{
										TrackingInventoryItem obj7 = new TrackingInventoryItem
										{
											templateId = ((object)playerItem.TemplateId/*cast due to constrained. prefix*/).ToString(),
											itemName = LocalizationExtensions.LocalizedShortName(playerItem),
											price = GetHandbookPrice(playerItem),
											qty = playerItem.StackObjectsCount
										};
										ItemAddress parent = playerItem.Parent;
										object obj8;
										if (parent == null)
										{
											obj8 = null;
										}
										else
										{
											IContainer container = parent.Container;
											obj8 = ((container != null) ? container.ID : null);
										}
										if (obj8 == null)
										{
											obj8 = "unknown";
										}
										obj7.slot = (string)obj8;
										list.Add(obj7);
									}
								}
								if (list.Count > 0)
								{
									TrackingPlayerInventory trackingPlayerInventory = new TrackingPlayerInventory
									{
										sessionId = sessionId,
										profileId = item.ProfileId,
										time = stopwatch.ElapsedMilliseconds,
										items = list
									};
									Telemetry.Send("PLAYER_INVENTORY", JsonConvert.SerializeObject((object)trackingPlayerInventory));
								}
							}
							catch
							{
							}
						}
					}
					if (!item.HealthController.IsAlive)
					{
						continue;
					}
					if (PlayerTracking.Value)
					{
						if ((Object)(object)item == (Object)null || (Object)(object)gameWorld == (Object)null)
						{
							((BaseUnityPlugin)this).Logger.LogWarning((object)"Player or gameWorld is null, skipping this iteration.");
							continue;
						}
						Vector3 position = item.Position;
						Vector3 lookDirection = item.LookDirection;
						Vector3 val = position - lookDirection;
						((Vector3)(ref val)).Normalize();
						((Vector3)(ref val2))._002Ector(0f, 0f, 1f);
						((Vector3)(ref val2)).Normalize();
						float dir = Mathf.Acos(Vector3.Dot(val, val2)) * 57.29578f;
						ValueStruct bodyPartHealth = ((IBaseHealthController)item.HealthController).GetBodyPartHealth((EBodyPart)7, true);
						float current3 = bodyPartHealth.Current;
						float maximum = bodyPartHealth.Maximum;
						string decision = "";
						if (item.IsAI)
						{
							try
							{
								string sainDecision = GetSainDecision(item);
								if (!string.IsNullOrEmpty(sainDecision))
								{
									decision = sainDecision;
								}
								else
								{
									IAIData aIData2 = item.AIData;
									BotOwner val3 = ((aIData2 != null) ? aIData2.BotOwner : null);
									if (((val3 != null) ? val3.Brain : null) != null)
									{
										BotLogicDecision? lastDecision = val3.Brain.LastDecision;
										if (lastDecision.HasValue)
										{
											decision = ((object)lastDecision.Value/*cast due to constrained. prefix*/).ToString();
										}
									}
								}
							}
							catch
							{
							}
						}
						if (item.IsAI && IsIdleOrPatrolDecision(decision))
						{
							try
							{
								IAIData aIData3 = item.AIData;
								BotOwner val4 = ((aIData3 != null) ? aIData3.BotOwner : null);
								string text = ((((val4 != null) ? val4.Brain : null) != null) ? val4.Brain.ActiveLayerName() : null);
								if (!string.IsNullOrEmpty(text))
								{
									if (_seenLayerNames.Add(text))
									{
										((BaseUnityPlugin)this).Logger.LogInfo((object)("RAID_REVIEW :::: BRAIN_LAYER :::: " + text));
									}
									if (text == "LootPatrol")
									{
										decision = text;
									}
									else if (text.Contains("Loot") && RAID_REVIEW__DETECTED_MODS.Contains("LOOTING_BOTS"))
									{
										decision = "LootingBots:" + text;
									}
									else if (text.Contains("Follower") || text.Contains("Regroup"))
									{
										decision = "BL:" + text;
									}
								}
							}
							catch
							{
							}
						}
						if (DANW_QUESTINGBOTS__DETECTED && item.IsAI)
						{
							try
							{
								TrackingBotQuest botQuestData = QuestingBots_Integration.GetBotQuestData(item, sessionId, elapsedMilliseconds);
								if (botQuestData != null && (botQuestData.status == "Active" || botQuestData.status == "Pending") && IsIdleOrPatrolDecision(decision))
								{
									decision = "QB:" + botQuestData.questName;
								}
							}
							catch
							{
							}
						}
						if (ORBIT__DETECTED && item.IsAI)
						{
							try
							{
								TrackingOrbitBotObjective botObjectiveData = Orbit_Integration.GetBotObjectiveData(item, sessionId, elapsedMilliseconds);
								if (botObjectiveData != null && IsIdleOrPatrolDecision(decision))
								{
									if (botObjectiveData.status == "Moving" || botObjectiveData.status == "Looting")
									{
										decision = "Orbit:" + botObjectiveData.category;
									}
									else if (botObjectiveData.status == "Finished")
									{
										decision = "Orbit:Guarding";
									}
								}
							}
							catch
							{
							}
						}
						else if (PHOBOS_LEGACY__DETECTED && item.IsAI)
						{
							try
							{
								TrackingBotObjective botObjectiveData2 = Phobos_Integration.GetBotObjectiveData(item, sessionId, elapsedMilliseconds);
								if (botObjectiveData2 != null && botObjectiveData2.status == "Moving" && IsIdleOrPatrolDecision(decision))
								{
									decision = "Phobos:" + botObjectiveData2.category;
								}
							}
							catch
							{
							}
						}
						bool ghost = false;
						if (ORBIT__DETECTED && item.IsAI)
						{
							try
							{
								ghost = Orbit_Integration.IsBotGhost(item);
							}
							catch
							{
							}
						}
						TrackingPlayerData trackingPlayerData = new TrackingPlayerData(sessionId, item.ProfileId, elapsedMilliseconds, position.x, position.y, position.z, dir, current3, maximum, decision, ghost);
						Telemetry.Send("POSITION", JsonConvert.SerializeObject((object)trackingPlayerData));
					}
					if (DANW_QUESTINGBOTS__DETECTED && item.IsAI)
					{
						try
						{
							TrackingBotQuest botQuestData2 = QuestingBots_Integration.GetBotQuestData(item, sessionId, elapsedMilliseconds);
							if (botQuestData2 != null)
							{
								string text2 = botQuestData2.questName + "|" + botQuestData2.status + "|" + botQuestData2.actionType;
								_lastBotQuestState.TryGetValue(item.ProfileId, out var value3);
								if (text2 != value3)
								{
									_lastBotQuestState[item.ProfileId] = text2;
									Telemetry.Send("BOT_QUEST", JsonConvert.SerializeObject((object)botQuestData2));
								}
							}
						}
						catch
						{
						}
					}
					if (ORBIT__DETECTED && item.IsAI)
					{
						try
						{
							TrackingOrbitBotObjective botObjectiveData3 = Orbit_Integration.GetBotObjectiveData(item, sessionId, elapsedMilliseconds);
							if (botObjectiveData3 != null)
							{
								string text3 = $"{botObjectiveData3.status}|{botObjectiveData3.category}|{botObjectiveData3.objectiveX:F1}|{botObjectiveData3.objectiveZ:F1}";
								_lastBotObjectiveState.TryGetValue(item.ProfileId, out var value4);
								if (text3 != value4)
								{
									_lastBotObjectiveState[item.ProfileId] = text3;
									Telemetry.Send("ORBIT_BOT_OBJECTIVE", JsonConvert.SerializeObject((object)botObjectiveData3));
								}
							}
						}
						catch
						{
						}
					}
					else
					{
						if (!PHOBOS_LEGACY__DETECTED || !item.IsAI)
						{
							continue;
						}
						try
						{
							TrackingBotObjective botObjectiveData4 = Phobos_Integration.GetBotObjectiveData(item, sessionId, elapsedMilliseconds);
							if (botObjectiveData4 != null)
							{
								string text4 = $"{botObjectiveData4.status}|{botObjectiveData4.category}|{botObjectiveData4.objectiveX:F1}|{botObjectiveData4.objectiveZ:F1}";
								_lastBotObjectiveState.TryGetValue(item.ProfileId, out var value5);
								if (text4 != value5)
								{
									_lastBotObjectiveState[item.ProfileId] = text4;
									Telemetry.Send("BOT_OBJECTIVE", JsonConvert.SerializeObject((object)botObjectiveData4));
								}
							}
						}
						catch
						{
						}
					}
				}
			}
			catch (Exception ex)
			{
				((BaseUnityPlugin)this).Logger.LogError((object)ex);
				((BaseUnityPlugin)this).Logger.LogError((object)(ex.Message ?? ""));
			}
		}
	}

	public static bool DetectMod(string modName)
	{
		if (Chainloader.PluginInfos.ContainsKey(modName))
		{
			return true;
		}
		return false;
	}

	public static bool DetectModContaining(string substring)
	{
		foreach (string key in Chainloader.PluginInfos.Keys)
		{
			if (key.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	public static bool MapLoaded()
	{
		return Singleton<GameWorld>.Instantiated;
	}
}
