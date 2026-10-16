using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Logging;
using Newtonsoft.Json;
using UnityEngine;

namespace RAID_REVIEW;

internal class SAIN_Integration
{
	public static async Task CheckForSainComponents(bool endCheck = false)
	{
		if (!RAID_REVIEW.SOLARINT_SAIN__DETECTED)
		{
			return;
		}
		try
		{
			Type sainBotControllerType = Type.GetType("SAIN.Components.BotManagerComponent, SAIN");
			Type.GetType("SAIN.Components.BotComponent, SAIN");
			if (sainBotControllerType == null)
			{
				LoggerInstance.Log.LogError((object)"RAID_REVIEW :::: INFO :::: SAINBotController type not found.");
				return;
			}
			while (RAID_REVIEW.searchingForSainComponents)
			{
				if (!endCheck)
				{
					await Task.Delay(5000);
				}
				else
				{
					RAID_REVIEW.searchingForSainComponents = false;
				}
				if (RAID_REVIEW.sainBotController == null)
				{
					LoggerInstance.Log.LogInfo((object)"RAID_REVIEW :::: INFO :::: Looking For SAIN Bot Controller");
					if ((Object)(object)RAID_REVIEW.gameWorld != (Object)null)
					{
						RAID_REVIEW.sainBotController = ((object)RAID_REVIEW.gameWorld).GetType().GetMethod("GetComponent", new Type[1] { typeof(Type) })?.Invoke(RAID_REVIEW.gameWorld, new object[1] { sainBotControllerType });
						if (RAID_REVIEW.sainBotController == null)
						{
							RAID_REVIEW.sainBotController = sainBotControllerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
						}
						if (RAID_REVIEW.sainBotController != null)
						{
							LoggerInstance.Log.LogInfo((object)"RAID_REVIEW :::: INFO :::: SAIN Bot Controller Found");
						}
						else
						{
							LoggerInstance.Log.LogInfo((object)"RAID_REVIEW :::: INFO :::: SAIN Bot Controller Not Found");
						}
					}
					else
					{
						LoggerInstance.Log.LogInfo((object)"RAID_REVIEW :::: INFO :::: GameWorld Not Found");
					}
					continue;
				}
				object obj = sainBotControllerType.GetProperty("Bots")?.GetValue(RAID_REVIEW.sainBotController);
				foreach (object item in (IEnumerable)(obj?.GetType().GetProperty("Values").GetValue(obj)))
				{
					object obj2 = item.GetType().GetProperty("Player")?.GetValue(item);
					string key = obj2?.GetType().GetProperty("ProfileId")?.GetValue(obj2) as string;
					if (RAID_REVIEW.updatedBots.ContainsKey(key) || !RAID_REVIEW.trackingPlayers.ContainsKey(key))
					{
						continue;
					}
					TrackingPlayer trackingPlayer = RAID_REVIEW.trackingPlayers[key];
					object obj3 = item.GetType().GetProperty("Info")?.GetValue(item);
					object obj4 = (obj3?.GetType().GetProperty("Profile"))?.GetValue(obj3);
					trackingPlayer.mod_SAIN_brain = (obj3?.GetType().GetProperty("Personality")?.GetValue(obj3))?.ToString() ?? "UNKNOWN";
					object obj5 = obj3?.GetType().GetProperty("BotDifficulty")?.GetValue(obj3);
					if (obj5 == null)
					{
						obj5 = obj4?.GetType().GetProperty("BotDifficulty")?.GetValue(obj4);
					}
					if (obj5 == null)
					{
						obj5 = obj4?.GetType().GetField("BotDifficulty")?.GetValue(obj4);
					}
					trackingPlayer.mod_SAIN_difficulty = obj5?.ToString() ?? "";
					object obj6 = obj4?.GetType().GetProperty("IsPMC")?.GetValue(obj4);
					if (obj6 == null)
					{
						obj6 = obj4?.GetType().GetField("IsPMC")?.GetValue(obj4);
					}
					if (!(bool)obj6)
					{
						string botType = getBotType(obj3, obj4);
						bool num = trackingPlayer.type == "BOT" || trackingPlayer.type == "UNKNOWN" || trackingPlayer.type == "SCAV|SCAV";
						int num2;
						switch (botType)
						{
						default:
							num2 = ((botType == "TAGGED AND CURSED SCAV|SCAV") ? 1 : 0);
							break;
						case "SCAV|SCAV":
						case "SCAV GROUP|SCAV":
						case "CRAZY SCAV EVENT|SCAV":
							num2 = 1;
							break;
						}
						bool flag = (byte)num2 != 0;
						bool flag2 = trackingPlayer.type == "UNTAR HUNTER|UNTAR";
						if ((num || !flag) && !flag2)
						{
							trackingPlayer.type = botType;
						}
						if (botType.EndsWith("|ISB"))
						{
							trackingPlayer.mod_SAIN_name = getSainBotTypeName(obj4);
						}
					}
					RAID_REVIEW.trackingPlayers[trackingPlayer.profileId] = trackingPlayer;
					RAID_REVIEW.updatedBots[trackingPlayer.profileId] = trackingPlayer;
					LoggerInstance.Log.LogInfo((object)("RAID_REVIEW :::: INFO :::: Updating player " + trackingPlayer.name + " with brain " + trackingPlayer.mod_SAIN_brain + ", type " + trackingPlayer.type + ", difficulty " + trackingPlayer.mod_SAIN_difficulty + ", sainName " + trackingPlayer.mod_SAIN_name));
					Telemetry.Send("PLAYER_UPDATE", JsonConvert.SerializeObject((object)trackingPlayer));
				}
			}
			RAID_REVIEW.searchingForSainComponents = false;
			RAID_REVIEW.updatedBots.Clear();
			RAID_REVIEW.sainBotController = null;
		}
		catch (Exception arg)
		{
			LoggerInstance.Log.LogFatal((object)$"RAID_REVIEW :::: ERROR :::: {arg}");
		}
	}

	public static string getBotType(object info, object profile)
	{
		string text = "UNKNOWN";
		if (RAID_REVIEW.SOLARINT_SAIN__DETECTED)
		{
			if (info == null)
			{
				ManualLogSource log = LoggerInstance.Log;
				if (log != null)
				{
					log.LogError((object)"Info is null.");
				}
				return text;
			}
			if (profile == null)
			{
				ManualLogSource log2 = LoggerInstance.Log;
				if (log2 != null)
				{
					log2.LogError((object)"Profile is null.");
				}
				return text;
			}
			object obj = profile.GetType().GetProperty("WildSpawnType")?.GetValue(profile);
			if (obj == null)
			{
				obj = profile.GetType().GetField("WildSpawnType")?.GetValue(profile);
			}
			object obj2 = profile.GetType().GetProperty("IsBoss")?.GetValue(profile);
			if (obj2 == null)
			{
				obj2 = profile.GetType().GetField("IsBoss")?.GetValue(profile);
			}
			bool flag = (bool)obj2;
			object obj3 = profile.GetType().GetProperty("IsPlayerScav")?.GetValue(profile);
			if (obj3 == null)
			{
				obj3 = profile.GetType().GetField("IsPlayerScav")?.GetValue(profile);
			}
			bool flag2 = (bool)obj3;
			if (obj != null)
			{
				switch (obj.ToString())
				{
				case "assault":
					text = "SCAV|SCAV";
					break;
				case "assaultGroup":
					text = "SCAV GROUP|SCAV";
					break;
				case "crazyAssaultEvent":
					text = "CRAZY SCAV EVENT|SCAV";
					break;
				case "marksman":
					text = "SCAV SNIPER|SNIPER";
					break;
				case "cursedAssault":
					text = "TAGGED AND CURSED SCAV|SCAV";
					break;
				case "bossKnight":
					text = "KNIGHT|GOON";
					break;
				case "followerBigPipe":
					text = "BIGPIPE|GOON";
					break;
				case "followerBirdEye":
					text = "BIRDEYE|GOON";
					break;
				case "exUsec":
					text = "ROGUE|FOLLOWER";
					break;
				case "pmcBot":
					text = "RAIDER|FOLLOWER";
					break;
				case "arenaFighterEvent":
					text = "BLOODHOUND|BLOODHOUND";
					break;
				case "sectantPriest":
					text = "CULTIST PRIEST|CULT";
					break;
				case "sectantWarrior":
					text = "CULTIST|CULT";
					break;
				case "bossKilla":
					text = "KILLA|BOSS";
					break;
				case "bossBully":
					text = "RASHALA|BOSS";
					break;
				case "followerBully":
					text = "RASHALA GUARD|FOLLOWER";
					break;
				case "bossKojaniy":
					text = "SHTURMAN|BOSS";
					break;
				case "followerKojaniy":
					text = "SHTURMAN GUARD|FOLLOWER";
					break;
				case "bossTagilla":
					text = "TAGILLA|BOSS";
					break;
				case "followerTagilla":
					text = "TAGILLA GUARD|FOLLOWER";
					break;
				case "bossSanitar":
					text = "SANITAR|BOSS";
					break;
				case "followerSanitar":
					text = "SANITAR GUARD|FOLLOWER";
					break;
				case "bossGluhar":
					text = "GLUHAR|BOSS";
					break;
				case "followerGluharSnipe":
					text = "GLUHAR GUARD SNIPE|FOLLOWER";
					break;
				case "followerGluharScout":
					text = "GLUHAR GUARD SCOUT|FOLLOWER";
					break;
				case "followerGluharSecurity":
					text = "GLUHAR GUARD SECURITY|FOLLOWER";
					break;
				case "followerGluharAssault":
					text = "GLUHAR GUARD ASSAULT|FOLLOWER";
					break;
				case "bossZryachiy":
					text = "ZRYACHIY|BOSS";
					break;
				case "followerZryachiy":
					text = "ZRYACHIY GUARD|FOLLOWER";
					break;
				case "bossBoar":
					text = "KABAN|BOSS";
					break;
				case "followerBoar":
					text = "KABAN GUARD|FOLLOWER";
					break;
				case "bossBoarSniper":
					text = "KABAN SNIPER|FOLLOWER";
					break;
				case "bossKolontay":
					text = "KOLONTAY|BOSS";
					break;
				case "followerKolontayAssault":
					text = "KOLONTAY ASSAULT|FOLLOWER";
					break;
				case "followerKolontaySecurity":
					text = "KOLONTAY SECURITY|FOLLOWER";
					break;
				case "bossPartisan":
					text = "PARTIZAN|BOSS";
					break;
				case "shooterBTR":
					text = "BTR|OTHER";
					break;
				case "bossTagillaAgro":
					text = "SHADOW TAGILLA|BOSS";
					break;
				case "bossKillaAgro":
					text = "VENGEFUL KILLA|BOSS";
					break;
				case "tagillaHelperAgro":
					text = "SHADOW TAGILLA GUARD|FOLLOWER";
					break;
				case "infectedAssault":
					text = "INFECTED|INFECTED";
					break;
				case "infectedPmc":
					text = "INFECTED PMC|INFECTED";
					break;
				case "infectedCivil":
					text = "INFECTED CIVILIAN|INFECTED";
					break;
				case "infectedLaborant":
					text = "INFECTED LABORANT|INFECTED";
					break;
				case "infectedTagilla":
					text = "INFECTED TAGILLA|BOSS";
					break;
				case "spiritWinter":
					text = "SPIRIT WINTER|SPECIAL";
					break;
				case "spiritSpring":
					text = "SPIRIT SPRING|SPECIAL";
					break;
				case "mercenary":
					text = "MERCENARY|MERCENARY";
					break;
				case "ruafRifleman":
					text = "RUAF RIFLEMAN|RUAF";
					break;
				case "ruafRiflemanSenior":
					text = "RUAF SENIOR RIFLEMAN|RUAF";
					break;
				case "ruafAutorifleman":
					text = "RUAF AUTORIFLEMAN|RUAF";
					break;
				case "ruafGrenadier":
					text = "RUAF GRENADIER|RUAF";
					break;
				case "ruafMarksman":
					text = "RUAF MARKSMAN|RUAF";
					break;
				case "ruafMachinegunner":
					text = "RUAF MACHINEGUNNER|RUAF";
					break;
				case "remnantRifleman":
					text = "REMNANT RIFLEMAN|RUAF";
					break;
				case "remnantRiflemanSenior":
					text = "REMNANT SENIOR RIFLEMAN|RUAF";
					break;
				case "remnantAutorifleman":
					text = "REMNANT AUTORIFLEMAN|RUAF";
					break;
				case "remnantGrenadier":
					text = "REMNANT GRENADIER|RUAF";
					break;
				case "remnantMarksman":
					text = "REMNANT MARKSMAN|RUAF";
					break;
				case "remnantMachinegunner":
					text = "REMNANT MACHINEGUNNER|RUAF";
					break;
				case "followeruntar":
					text = "UNTAR GUARD|UNTAR";
					break;
				case "bossuntarlead":
					text = "UNTAR SQUAD LEADER|UNTAR";
					break;
				case "followeruntarmarksman":
					text = "UNTAR MARKSMAN|UNTAR";
					break;
				case "bossuntarofficer":
					text = "UNTAR OFFICER|UNTAR";
					break;
				case "blackDivLead":
					text = "BLACK DIV LEAD|BLACKDIV";
					break;
				case "blackDivAssault":
					text = "BLACK DIV ASSAULT|BLACKDIV";
					break;
				case "blackDivBreacher":
					text = "BLACK DIV BREACHER|BLACKDIV";
					break;
				case "blackDivSupport":
					text = "BLACK DIV SUPPORT|BLACKDIV";
					break;
				case "ISBSpecialForces":
					text = "ISB SPECIAL FORCES|ISB";
					break;
				case "ISBTeamLeader":
					text = "ISB TEAM LEADER|ISB";
					break;
				case "ISBSecondLeader":
					text = "ISB SECOND LEADER|ISB";
					break;
				case "ISBFirefly":
					text = "ISB FIREFLY|ISB";
					break;
				case "ISBFireflyFollowerLoghan":
					text = "ISB FIREFLY LOGHAN|ISB";
					break;
				case "ISBFireflyFollowerVipper":
					text = "ISB FIREFLY VIPPER|ISB";
					break;
				case "ISBFireflyShielder01":
					text = "ISB FIREFLY SHIELDER 1|ISB";
					break;
				case "ISBFireflyShielder02":
					text = "ISB FIREFLY SHIELDER 2|ISB";
					break;
				case "ISBBossCommander":
				case "ISBFollowerCommander":
					text = "WHITE TUSK|ISB";
					break;
				case "wedge":
					text = "WEDGE|BOSS";
					break;
				case "wedgeguard":
					text = "WEDGE GUARD|FOLLOWER";
					break;
				case "CombineSoldier":
					text = "COMBINE SOLDIER|COMBINE";
					break;
				case "CombineShotgunner":
					text = "COMBINE SHOTGUNNER|COMBINE";
					break;
				case "CombineElite":
					text = "COMBINE ELITE|COMBINE";
					break;
				default:
					text = obj.ToString().ToUpper() + "|FACTION_MOD";
					break;
				}
			}
			if (flag2)
			{
				text = "PLAYER_SCAV|SCAV";
			}
			if ((text == "UNKNOWN") & flag)
			{
				text = "CUSTOM|BOSS";
			}
		}
		return text;
	}

	public static string getSainBotTypeName(object profile)
	{
		try
		{
			if (profile == null)
			{
				return "";
			}
			object obj = profile.GetType().GetProperty("WildSpawnType")?.GetValue(profile) ?? profile.GetType().GetField("WildSpawnType")?.GetValue(profile);
			if (obj == null)
			{
				return "";
			}
			if (!(Type.GetType("SAIN.Preset.BotTypeDefinitions, SAIN")?.GetField("BotTypes", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is IDictionary dictionary) || !dictionary.Contains(obj))
			{
				return "";
			}
			object obj2 = dictionary[obj];
			return (obj2?.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.Public)?.GetValue(obj2) as string) ?? "";
		}
		catch (Exception arg)
		{
			ManualLogSource log = LoggerInstance.Log;
			if (log != null)
			{
				log.LogError((object)$"RAID_REVIEW :::: ERROR :::: getSainBotTypeName: {arg}");
			}
			return "";
		}
	}
}
