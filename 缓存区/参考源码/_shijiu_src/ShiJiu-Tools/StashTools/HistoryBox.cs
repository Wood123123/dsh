namespace StashTools;

internal readonly record struct HistoryBox(float X, float Y, float Width, float Height)
{
	public float Right => X + Width;

	public float Bottom => Y + Height;

	public bool Valid
	{
		get
		{
			if (float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Width) && float.IsFinite(Height) && Width > 0f)
			{
				return Height > 0f;
			}
			return false;
		}
	}

	public bool Overlaps(HistoryBox other)
	{
		if (X < other.Right && Right > other.X && Y < other.Bottom)
		{
			return Bottom > other.Y;
		}
		return false;
	}
}
