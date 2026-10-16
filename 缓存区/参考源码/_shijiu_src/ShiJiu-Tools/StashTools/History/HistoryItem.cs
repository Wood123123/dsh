namespace StashTools.History;

public sealed class HistoryItem
{
	public string Id { get; set; } = "";

	public string Template { get; set; } = "";

	public string PriceKey { get; set; } = "";

	public string Name { get; set; } = "";

	public string Parent { get; set; } = "";

	public string Slot { get; set; } = "";

	public int X { get; set; }

	public int Y { get; set; }

	public int Width { get; set; } = 1;

	public int Height { get; set; } = 1;

	public int GridWidth { get; set; }

	public int GridHeight { get; set; }

	public bool Rotated { get; set; }

	public bool Storage { get; set; }

	public int Count { get; set; } = 1;

	public double Units { get; set; }

	public double BroughtUnits { get; set; }

	public double Price { get; set; }

	public bool Priced { get; set; }

	public bool Valued { get; set; } = true;

	public string Destination { get; set; } = "随身";
}
