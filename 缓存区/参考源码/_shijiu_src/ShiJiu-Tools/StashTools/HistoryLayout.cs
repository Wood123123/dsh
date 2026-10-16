using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace StashTools;

internal static class HistoryLayout
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass2_0
	{
		public HistoryBox visible;

		public IReadOnlyList<HistoryBox> occupied;

		public float y;

		public float height;

		public float width;

		public Func<HistoryBox, bool> _003C_003E9__1;

		internal HistoryBox? A(float P_0, float P_1)
		{
			float num = Math.Max(P_0, visible.X + 8f);
			foreach (HistoryBox item in from historyBox in occupied
				where historyBox.Valid && historyBox.Y < y + height && historyBox.Bottom > y
				orderby historyBox.X
				select historyBox)
			{
				if (!(item.Right <= num))
				{
					if (num + width + 8f <= Math.Min(item.X, P_1))
					{
						return new HistoryBox(num, y, width, height);
					}
					num = Math.Max(num, item.Right + 10f);
				}
			}
			if (!(num + width <= P_1))
			{
				return null;
			}
			return new HistoryBox(num, y, width, height);
		}

		internal bool A(HistoryBox P_0)
		{
			if (P_0.Valid && P_0.Y < y + height)
			{
				return P_0.Bottom > y;
			}
			return false;
		}
	}

	internal static HistoryBox A(HistoryBox P_0, HistoryBox P_1)
	{
		float num = Math.Max(P_0.X, P_1.X);
		float num2 = Math.Max(P_0.Y, P_1.Y);
		return new HistoryBox(num, num2, Math.Max(0f, Math.Min(P_0.Right, P_1.Right) - num), Math.Max(0f, Math.Min(P_0.Bottom, P_1.Bottom) - num2));
	}

	internal static HistoryBox A(HistoryBox P_0, HistoryBox P_1, float P_2)
	{
		if (!P_0.Valid || !P_1.Valid)
		{
			return default(HistoryBox);
		}
		float num = Math.Max(P_0.X, P_1.X);
		float num2 = Math.Min(P_0.Right - 8f, P_1.Right);
		float num3 = Math.Max(P_0.Y, P_2);
		float num4 = P_0.Bottom - Math.Max(20f, P_0.Height * 0.05f);
		return new HistoryBox(num, num3, Math.Max(0f, num2 - num), Math.Max(0f, num4 - num3));
	}

	internal static HistoryBox? A(HistoryBox P_0, HistoryBox P_1, IReadOnlyList<HistoryBox> P_2)
	{
		_003C_003Ec__DisplayClass2_0 obj = new _003C_003Ec__DisplayClass2_0();
		obj.visible = P_0;
		obj.occupied = P_2;
		if (!obj.visible.Valid || !P_1.Valid)
		{
			return null;
		}
		obj.height = Math.Clamp(P_1.Height, 26f, 44f);
		obj.y = P_1.Y;
		if (obj.y < obj.visible.Y || obj.y + obj.height > obj.visible.Bottom)
		{
			return null;
		}
		obj.width = Math.Clamp(P_1.Width, 140f, 220f);
		return obj.A(P_1.Right + 10f, obj.visible.Right - 8f) ?? obj.A(obj.visible.X + 8f, P_1.X - 10f);
	}
}
