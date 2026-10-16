using System;
using System.Collections.Generic;
using System.Linq;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;

namespace StashTools;

internal sealed class GroupMovePlan
{
	internal sealed record Step(Item Item, GridItemAddress Address, bool Unchanged);

	internal sealed record Snapshot(IntPtr Grid, int Width, int Height, Dictionary<string, GridPacking.Box> Occupied);

	internal readonly List<Step> Steps = new List<Step>();

	internal int Skipped;

	internal string? Error;

	internal bool CanMove => Steps.Any((Step s) => !s.Unchanged);

	internal static Snapshot Capture(Grid grid, Item[] items)
	{
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0192: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		int gridWidth = grid.GridWidth;
		int gridHeight = grid.GridHeight;
		if (gridWidth <= 0 || gridHeight <= 0 || (long)gridWidth * (long)gridHeight > 16384)
		{
			throw new InvalidOperationException("目标格子尺寸无效");
		}
		IReadOnlyList<bool> layout = grid.Layout;
		if (layout == null || layout.Count < gridWidth * gridHeight)
		{
			throw new InvalidOperationException("格子占用数据尚未就绪");
		}
		Dictionary<string, GridPacking.Box> dictionary = new Dictionary<string, GridPacking.Box>();
		for (int i = 0; i < gridWidth * gridHeight; i++)
		{
			if (layout[i])
			{
				dictionary["cell:" + i] = new GridPacking.Box(i % gridWidth, i / gridWidth, 1, 1);
			}
		}
		foreach (Item val in items)
		{
			GridItemAddress val2 = val.CurrentAddress?.TryCast<GridItemAddress>();
			if (val2?.Grid.Identity() != grid.Identity())
			{
				continue;
			}
			LocationInGrid locationInGrid = val2.LocationInGrid;
			IntVec2 val3 = val.CalculateRotatedSize(locationInGrid.r);
			for (int k = Math.Max(0, locationInGrid.y); k < Math.Min(gridHeight, locationInGrid.y + val3.Y); k++)
			{
				for (int l = Math.Max(0, locationInGrid.x); l < Math.Min(gridWidth, locationInGrid.x + val3.X); l++)
				{
					dictionary.Remove("cell:" + (k * gridWidth + l));
				}
			}
			dictionary[val.Id] = new GridPacking.Box(locationInGrid.x, locationInGrid.y, val3.X, val3.Y);
		}
		return new Snapshot(grid.Identity(), gridWidth, gridHeight, dictionary);
	}

	internal static GroupMovePlan Build(Item[] items, DragItemContext lead, GridView view, Snapshot snapshot)
	{
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0300: Expected O, but got Unknown
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_030c: Expected O, but got Unknown
		GroupMovePlan groupMovePlan = new GroupMovePlan();
		Grid grid = view.Grid;
		GridItemAddress origin = ((ItemContext)lead).Item.CurrentAddress?.TryCast<GridItemAddress>();
		LocationInGrid target = view.CalculateItemLocation(lead);
		if (grid == null || origin == null || target == (LocationInGrid)null || view._nonInteractable)
		{
			groupMovePlan.Error = "当前目标不能放置物品。";
			return groupMovePlan;
		}
		if (lead.ItemRotation != origin.LocationInGrid.r)
		{
			groupMovePlan.Error = "整组移动保留原方向，请先取消旋转。";
			return groupMovePlan;
		}
		int dx = target.x - origin.LocationInGrid.x;
		int dy = target.y - origin.LocationInGrid.y;
		(Item, GridItemAddress)[] source = (from i in items.DistinctBy((Item i) => i.Id)
			select (item: i, @from: i.CurrentAddress?.TryCast<GridItemAddress>())).ToArray();
		source = ((!(origin.Grid.Identity() == grid.Identity()) || !source.All(((Item item, GridItemAddress from) p) => p.from?.Grid.Identity() == grid.Identity())) ? source.OrderByDescending(((Item item, GridItemAddress from) p) => p.item.Id == ((ItemContext)lead).Item.Id).ToArray() : (from p in source
			orderby (dy == 0) ? (p.@from.LocationInGrid.x * Math.Sign(dx)) : (p.@from.LocationInGrid.y * Math.Sign(dy)) descending, p.@from.LocationInGrid.x * Math.Sign(dx) descending
			select p).ToArray());
		Dictionary<string, (Item, GridItemAddress)> dictionary = source.ToDictionary(((Item item, GridItemAddress from) p) => p.item.Id);
		HashSet<string> allowed = (from p in source
			where Moves.Eligible(p.item) && ((Item)grid.ParentItem).Id != p.item.Id && grid.CheckCompatibility(p.item)
			select p.item.Id).ToHashSet();
		IEnumerable<GridPacking.Piece> enumerable = source.Select(delegate((Item item, GridItemAddress from) p)
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Unknown result type (might be due to invalid IL or missing references)
			//IL_0081: Unknown result type (might be due to invalid IL or missing references)
			//IL_0087: Unknown result type (might be due to invalid IL or missing references)
			Item item2 = p.item;
			GridItemAddress item3 = p.from;
			IntVec2 val2 = item2.CalculateRotatedSize((ItemRotation)((item3 != null) ? ((int)item3.LocationInGrid.r) : 0));
			bool flag = p.from?.Grid.Identity() == origin.Grid.Identity();
			GridPacking.Box value;
			return new GridPacking.Piece(p.item.Id, val2.X, val2.Y, snapshot.Occupied.TryGetValue(p.item.Id, out value) ? new GridPacking.Box?(value) : ((GridPacking.Box?)null), flag ? (target.x + p.from.LocationInGrid.x - origin.LocationInGrid.x) : target.x, flag ? (target.y + p.from.LocationInGrid.y - origin.LocationInGrid.y) : target.y);
		});
		GridPacking.Result result = GridPacking.A(snapshot.Width, snapshot.Height, snapshot.Occupied, enumerable, (GridPacking.Piece piece, GridPacking.Box _) => allowed.Contains(piece.Id));
		foreach (GridPacking.Placement item4 in result.Placed)
		{
			(Item, GridItemAddress) tuple = dictionary[item4.Piece.Id];
			GridItemAddress item = tuple.Item2;
			ItemRotation val = (ItemRotation)((item != null) ? ((int)item.LocationInGrid.r) : 0);
			groupMovePlan.Steps.Add(new Step(tuple.Item1, (GridItemAddress)new ProtectedGridItemAddress(grid, new LocationInGrid(item4.Target.X, item4.Target.Y, val)), item4.Unchanged));
		}
		groupMovePlan.Skipped = result.Skipped.Count;
		return groupMovePlan;
	}
}
