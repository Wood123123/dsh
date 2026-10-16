using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace StashTools;

internal static class ExchangePacking
{
	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass0_0
	{
		public IReadOnlyList<GridPacking.Box> blockers;

		public GridPacking.Box source;

		public int width;

		public int height;

		public GridPacking.Box destination;

		public IReadOnlyList<bool> occupied;

		public GridPacking.Box[] released;

		public bool sameGrid;

		public int[] order;

		public GridPacking.Box[][] candidates;

		public int budget;

		public GridPacking.Box[] placed;

		internal int A(int P_0)
		{
			return blockers[P_0].W * blockers[P_0].H;
		}

		internal int a(int P_0)
		{
			return Math.Max(blockers[P_0].W, blockers[P_0].H);
		}

		internal int B(int P_0)
		{
			return blockers[P_0].Y;
		}

		internal int b(int P_0)
		{
			return blockers[P_0].X;
		}

		internal GridPacking.Box[] A(GridPacking.Box P_0)
		{
			return a(P_0).ToArray();
		}

		internal IEnumerable<GridPacking.Box> a(GridPacking.Box P_0)
		{
			int left = Math.Max(0, source.X - P_0.W + 1);
			int num = Math.Max(0, source.Y - P_0.H + 1);
			int right = Math.Min(width - P_0.W, source.X + source.W - 1);
			int num2 = Math.Min(height - P_0.H, source.Y + source.H - 1);
			if (P_0.W <= 0 || P_0.H <= 0 || right < left || num2 < num)
			{
				return Array.Empty<GridPacking.Box>();
			}
			int preferredX = source.X + P_0.X - destination.X;
			int preferredY = source.Y + P_0.Y - destination.Y;
			return from box in Enumerable.Range(num, num2 - num + 1).SelectMany((int y) => from x in Enumerable.Range(left, right - left + 1)
					select new GridPacking.Box(x, y, P_0.W, P_0.H))
				where MovementPolicy.A(width, height, occupied, box, released) && (!sameGrid || !GridPacking.A(box, destination))
				orderby Math.Abs(box.X - preferredX) + Math.Abs(box.Y - preferredY), box.Y, box.X
				select box;
		}

		internal bool B(GridPacking.Box P_0)
		{
			if (MovementPolicy.A(width, height, occupied, P_0, released))
			{
				if (sameGrid)
				{
					return !GridPacking.A(P_0, destination);
				}
				return true;
			}
			return false;
		}

		internal bool C(int P_0)
		{
			if (P_0 == order.Length)
			{
				return true;
			}
			int num = order[P_0];
			GridPacking.Box[] array = candidates[num];
			foreach (GridPacking.Box p in array)
			{
				if (--budget < 0)
				{
					return false;
				}
				if (!order.Take(P_0).Any((int num2) => GridPacking.A(placed[num2], p)))
				{
					placed[num] = p;
					if (C(P_0 + 1))
					{
						return true;
					}
				}
			}
			return false;
		}
	}

	internal static GridPacking.Box[]? A(int P_0, int P_1, IReadOnlyList<bool> P_2, GridPacking.Box P_3, GridPacking.Box P_4, bool P_5, IReadOnlyList<GridPacking.Box> P_6)
	{
		_003C_003Ec__DisplayClass0_0 CS_0024_003C_003E8__locals34 = new _003C_003Ec__DisplayClass0_0();
		CS_0024_003C_003E8__locals34.blockers = P_6;
		CS_0024_003C_003E8__locals34.source = P_3;
		CS_0024_003C_003E8__locals34.width = P_0;
		CS_0024_003C_003E8__locals34.height = P_1;
		CS_0024_003C_003E8__locals34.destination = P_4;
		CS_0024_003C_003E8__locals34.occupied = P_2;
		CS_0024_003C_003E8__locals34.sameGrid = P_5;
		if (CS_0024_003C_003E8__locals34.blockers.Count == 0 || CS_0024_003C_003E8__locals34.blockers.Count > 32 || CS_0024_003C_003E8__locals34.source.W <= 0 || CS_0024_003C_003E8__locals34.source.H <= 0 || CS_0024_003C_003E8__locals34.source.W * CS_0024_003C_003E8__locals34.source.H > 256)
		{
			return null;
		}
		CS_0024_003C_003E8__locals34.released = (CS_0024_003C_003E8__locals34.sameGrid ? CS_0024_003C_003E8__locals34.blockers.Concat(new GridPacking.Box[1] { CS_0024_003C_003E8__locals34.source }).ToArray() : new GridPacking.Box[1] { CS_0024_003C_003E8__locals34.source });
		CS_0024_003C_003E8__locals34.placed = new GridPacking.Box[CS_0024_003C_003E8__locals34.blockers.Count];
		CS_0024_003C_003E8__locals34.order = (from index in Enumerable.Range(0, CS_0024_003C_003E8__locals34.blockers.Count)
			orderby CS_0024_003C_003E8__locals34.blockers[index].W * CS_0024_003C_003E8__locals34.blockers[index].H descending, Math.Max(CS_0024_003C_003E8__locals34.blockers[index].W, CS_0024_003C_003E8__locals34.blockers[index].H) descending, CS_0024_003C_003E8__locals34.blockers[index].Y, CS_0024_003C_003E8__locals34.blockers[index].X
			select index).ToArray();
		CS_0024_003C_003E8__locals34.candidates = CS_0024_003C_003E8__locals34.blockers.Select((GridPacking.Box box) => CS_0024_003C_003E8__locals34.a(box).ToArray()).ToArray();
		CS_0024_003C_003E8__locals34.budget = 8192;
		if (!CS_0024_003C_003E8__locals34.C(0))
		{
			return null;
		}
		return CS_0024_003C_003E8__locals34.placed;
	}
}
