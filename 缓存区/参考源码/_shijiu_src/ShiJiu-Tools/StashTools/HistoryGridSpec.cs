namespace StashTools;

internal readonly record struct HistoryGridSpec(string Slot, int Width, int Height, float X = float.NaN, float Y = float.NaN)
{
	internal bool Positioned
	{
		get
		{
			if (float.IsFinite(X))
			{
				return float.IsFinite(Y);
			}
			return false;
		}
	}
}
