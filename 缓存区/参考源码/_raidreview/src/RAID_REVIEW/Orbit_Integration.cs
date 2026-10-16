using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT;
using Orbit.Api;
using UnityEngine;

namespace RAID_REVIEW;

internal class Orbit_Integration
{
	private static bool _apiReadyLogged;

	private static bool _ghostApiMissing;

	private static bool _fieldApiMissing;

	public static void InitReflection()
	{
		if (!_apiReadyLogged && OrbitTelemetry.IsAvailable)
		{
			LoggerInstance.Log.LogInfo((object)"RAID_REVIEW :::: ORBIT :::: API ready");
			_apiReadyLogged = true;
		}
	}

	public static void ResetSessionState()
	{
		_apiReadyLogged = false;
	}

	public static void RefreshAgentCache()
	{
	}

	public static bool IsBotGhost(Player player)
	{
		if (_ghostApiMissing || (Object)(object)player == (Object)null || !player.IsAI)
		{
			return false;
		}
		try
		{
			return IsBotGhostInner(player.ProfileId);
		}
		catch (MissingMethodException)
		{
			_ghostApiMissing = true;
			LoggerInstance.Log.LogInfo((object)"RAID_REVIEW :::: ORBIT :::: ghost/limiter API not present (ORBIT older than 2.0) — ghost features disabled");
			return false;
		}
		catch (TypeLoadException)
		{
			_ghostApiMissing = true;
			return false;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool IsBotGhostInner(string profileId)
	{
		return OrbitTelemetry.IsBotDormant(profileId);
	}

	public static List<TrackingOrbitGhostFight> GetGhostFights(string sessionId, long time)
	{
		if (_ghostApiMissing)
		{
			return null;
		}
		try
		{
			return GetGhostFightsInner(sessionId, time);
		}
		catch (MissingMethodException)
		{
			_ghostApiMissing = true;
			return null;
		}
		catch (TypeLoadException)
		{
			_ghostApiMissing = true;
			return null;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static List<TrackingOrbitGhostFight> GetGhostFightsInner(string sessionId, long time)
	{
		List<OrbitGhostFight> list = OrbitTelemetry.DrainGhostFights();
		if (list == null)
		{
			return null;
		}
		List<TrackingOrbitGhostFight> list2 = new List<TrackingOrbitGhostFight>(list.Count);
		foreach (OrbitGhostFight item in list)
		{
			list2.Add(new TrackingOrbitGhostFight
			{
				sessionId = sessionId,
				time = time,
				aX = item.AX,
				aY = item.AY,
				aZ = item.AZ,
				bX = item.BX,
				bY = item.BY,
				bZ = item.BZ,
				durationMs = (long)(item.Duration * 1000f),
				casualties = item.Casualties
			});
		}
		return list2;
	}

	public static TrackingOrbitBotObjective GetBotObjectiveData(Player player, string sessionId, long time)
	{
		if ((Object)(object)player == (Object)null || !player.IsAI)
		{
			return null;
		}
		OrbitBotObjective botObjective = OrbitTelemetry.GetBotObjective(player.ProfileId);
		if (botObjective == null)
		{
			return null;
		}
		return new TrackingOrbitBotObjective
		{
			sessionId = sessionId,
			profileId = player.ProfileId,
			time = time,
			status = botObjective.Status,
			category = botObjective.Category,
			isLeader = botObjective.IsLeader,
			objectiveX = botObjective.ObjectiveX,
			objectiveY = botObjective.ObjectiveY,
			objectiveZ = botObjective.ObjectiveZ,
			extractReason = botObjective.ExtractReason
		};
	}

	public static TrackingOrbitField GetAdvectionFieldSnapshot(string sessionId, long time)
	{
		if (_fieldApiMissing)
		{
			return null;
		}
		try
		{
			return GetAdvectionFieldSnapshotInner(sessionId, time);
		}
		catch (MissingMemberException)
		{
			_fieldApiMissing = true;
			LoggerInstance.Log.LogInfo((object)"RAID_REVIEW :::: ORBIT :::: field snapshot API mismatch (ORBIT older than 2.0) — hotspot field overlay disabled");
			return null;
		}
		catch (TypeLoadException)
		{
			_fieldApiMissing = true;
			return null;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static TrackingOrbitField GetAdvectionFieldSnapshotInner(string sessionId, long time)
	{
		OrbitFieldSnapshot fieldSnapshot = OrbitTelemetry.GetFieldSnapshot();
		if (fieldSnapshot == null)
		{
			return null;
		}
		List<TrackingOrbitCell> list = new List<TrackingOrbitCell>(fieldSnapshot.Advection.Count);
		foreach (OrbitFieldCell item in fieldSnapshot.Advection)
		{
			list.Add(new TrackingOrbitCell
			{
				x = item.X,
				y = item.Y,
				fx = item.Fx,
				fz = item.Fz
			});
		}
		List<TrackingOrbitZone> list2 = new List<TrackingOrbitZone>(fieldSnapshot.Zones.Count);
		foreach (OrbitFieldZone zone in fieldSnapshot.Zones)
		{
			list2.Add(new TrackingOrbitZone
			{
				x = zone.X,
				y = zone.Y,
				radius = zone.Radius,
				force = zone.Force,
				decay = zone.Decay
			});
		}
		return new TrackingOrbitField
		{
			sessionId = sessionId,
			time = time,
			gridCols = fieldSnapshot.GridCols,
			gridRows = fieldSnapshot.GridRows,
			worldMinX = fieldSnapshot.WorldMinX,
			worldMinZ = fieldSnapshot.WorldMinZ,
			cellSize = fieldSnapshot.CellSize,
			advection = list,
			zones = list2
		};
	}

	public static TrackingOrbitMainObjectives GetMainObjectivesSnapshot(string sessionId, long time)
	{
		List<OrbitSquadMainObjectives> mainObjectivesSnapshot = OrbitTelemetry.GetMainObjectivesSnapshot();
		if (mainObjectivesSnapshot == null || mainObjectivesSnapshot.Count == 0)
		{
			return null;
		}
		List<TrackingOrbitSquadMainObjectives> list = new List<TrackingOrbitSquadMainObjectives>(mainObjectivesSnapshot.Count);
		foreach (OrbitSquadMainObjectives item in mainObjectivesSnapshot)
		{
			List<TrackingOrbitMainObjective> list2 = new List<TrackingOrbitMainObjective>(item.MainObjectives.Count);
			foreach (OrbitMainObjective mainObjective in item.MainObjectives)
			{
				list2.Add(new TrackingOrbitMainObjective
				{
					type = mainObjective.Type,
					cellX = mainObjective.CellX,
					cellY = mainObjective.CellY,
					x = mainObjective.X,
					y = mainObjective.Y,
					z = mainObjective.Z,
					completed = mainObjective.Completed,
					killsRoamStartedAt = mainObjective.KillsRoamStartedAt,
					killsRoamTargetDuration = mainObjective.KillsRoamTargetDuration,
					lootValueEnteredAt = mainObjective.LootValueEnteredAt,
					lootValueTotal = mainObjective.LootValueTotal,
					lootValueInterrupted = mainObjective.LootValueInterrupted,
					questTriggerId = mainObjective.QuestTriggerId,
					questTitle = mainObjective.QuestTitle
				});
			}
			list.Add(new TrackingOrbitSquadMainObjectives
			{
				squadId = item.SquadId,
				memberProfileIds = item.MemberProfileIds,
				mainObjectives = list2
			});
		}
		return new TrackingOrbitMainObjectives
		{
			sessionId = sessionId,
			time = time,
			squads = list
		};
	}

	public static int GetMainObjectivesRevision()
	{
		return OrbitTelemetry.MainObjectivesRevision;
	}
}
