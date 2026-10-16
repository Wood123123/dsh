namespace StashTools.History;

public sealed class HistoryQuery
{
	public string Profile { get; set; } = "";

	public string Search { get; set; } = "";

	public int Page { get; set; } = 1;

	public bool FavoritesOnly { get; set; }

	public string Id { get; set; } = "";

	public bool Favorite { get; set; }
}
