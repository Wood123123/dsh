namespace StashTools.History;

public sealed class HistoryKill
{
	public string Key { get; set; } = "";

	public string Name { get; set; } = "";

	public string Side { get; set; } = "";

	public string Role { get; set; } = "";

	public int Level { get; set; }

	public double Seconds { get; set; }

	public string Weapon { get; set; } = "";

	public double Distance { get; set; }

	public string Profile { get; set; } = "";

	public bool? IsAi { get; set; }
}
