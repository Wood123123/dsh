using System;
using System.Collections.Generic;
using System.Linq;

namespace StashTools;

internal static class HistoryGridLayout
{
	internal static HistoryGridPlan Build(IEnumerable<HistoryGridSpec> source, float available, float maximumCell = 52f)
	{
		HistoryGridSpec[] grids = source.Where((HistoryGridSpec g) => g.Width > 0 && g.Height > 0 && g.Width <= 1024 && g.Height <= 1024).DistinctBy((HistoryGridSpec g) => g.Slot).Take(128)
			.ToArray();
		if (grids.Length == 0 || !float.IsFinite(available) || available <= 0f || !float.IsFinite(maximumCell) || maximumCell <= 0f)
		{
			return new HistoryGridPlan(0f, 0f, 0f, Array.Empty<HistoryGridPlacement>(), Native: false);
		}
		bool flag = grids.All((HistoryGridSpec g) => g.Positioned && Math.Abs(g.X) <= 4096f && Math.Abs(g.Y) <= 4096f);
		HistoryBox[] array = new HistoryBox[grids.Length];
		if (flag)
		{
			float num = grids.Min((HistoryGridSpec g) => g.X);
			float num2 = grids.Min((HistoryGridSpec g) => g.Y);
			for (int num3 = 0; num3 < grids.Length; num3++)
			{
				array[num3] = new HistoryBox(grids[num3].X - num, grids[num3].Y - num2, grids[num3].Width, grids[num3].Height);
			}
			for (int num4 = 0; (num4 < array.Length) & flag; num4++)
			{
				for (int num5 = 0; num5 < num4; num5++)
				{
					if (array[num4].Overlaps(array[num5]))
					{
						flag = false;
						break;
					}
				}
			}
		}
		float cell;
		if (flag)
		{
			cell = Math.Min(maximumCell, available / array.Max((HistoryBox b) => b.Right));
		}
		else
		{
			cell = Math.Min(maximumCell, available / (float)grids.Max((HistoryGridSpec g) => g.Width));
			float num6 = available / cell;
			float num7 = 7f / maximumCell;
			float num8 = 0f;
			float num9 = 0f;
			float num10 = 0f;
			for (int num11 = 0; num11 < grids.Length; num11++)
			{
				HistoryGridSpec historyGridSpec = grids[num11];
				if (num8 > 0f && num8 + (float)historyGridSpec.Width > num6 + 0.001f)
				{
					num8 = 0f;
					num9 += num10 + num7;
					num10 = 0f;
				}
				array[num11] = new HistoryBox(num8, num9, historyGridSpec.Width, historyGridSpec.Height);
				num8 += (float)historyGridSpec.Width + num7;
				num10 = Math.Max(num10, historyGridSpec.Height);
			}
		}
		HistoryGridPlacement[] array2 = array.Select((HistoryBox b, int n) => new HistoryGridPlacement(grids[n].Slot, new HistoryBox(b.X * cell, b.Y * cell, b.Width * cell, b.Height * cell))).ToArray();
		return new HistoryGridPlan(cell, array2.Max((HistoryGridPlacement p) => p.Box.Right), array2.Max((HistoryGridPlacement p) => p.Box.Bottom), array2, flag);
	}
}
