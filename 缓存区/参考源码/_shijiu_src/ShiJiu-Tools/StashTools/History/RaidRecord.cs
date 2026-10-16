using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace StashTools.History;

public sealed class RaidRecord
{
	public int Schema { get; set; } = 1;

	public string Id { get; set; } = "";

	public string Profile { get; set; } = "";

	public string Name { get; set; } = "";

	public string Side { get; set; } = "";

	public DateTime StartedUtc { get; set; }

	public DateTime EndedUtc { get; set; }

	public double PausedSeconds { get; set; }

	[JsonIgnore]
	public TimeSpan ActiveDuration => TimeSpan.FromSeconds(Math.Max(0.0, (EndedUtc - StartedUtc).TotalSeconds - PausedSeconds));

	public string Status { get; set; } = "";

	public bool Final { get; set; }

	public bool Reconciled { get; set; }

	public double Income { get; set; }

	public double Expense { get; set; }

	public double Loadout { get; set; }

	public int MissingPrices { get; set; }

	public int Revision { get; set; }

	public List<string> Route { get; set; } = new List<string>();

	public List<HistoryItem> Items { get; set; } = new List<HistoryItem>();

	public List<HistoryKill> Kills { get; set; } = new List<HistoryKill>();

	public List<string> Events { get; set; } = new List<string>();

	public List<string> Tasks { get; set; } = new List<string>();

	public List<string> Achievements { get; set; } = new List<string>();

	public CoopRaidContext? Team { get; set; }
}
