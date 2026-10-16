using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace StashTools;

internal static class GridPacking
{
	internal readonly struct Box(int X, int Y, int W, int H) : IEquatable<Box>
	{
		[CompilerGenerated]
		private readonly int m_A = X;

		[CompilerGenerated]
		private readonly int a = Y;

		[CompilerGenerated]
		private readonly int B = W;

		[CompilerGenerated]
		private readonly int b = H;

		public int X
		{
			[CompilerGenerated]
			get
			{
				return this.m_A;
			}
			[CompilerGenerated]
			init
			{
				this.m_A = value;
			}
		}

		public int Y
		{
			[CompilerGenerated]
			get
			{
				return a;
			}
			[CompilerGenerated]
			init
			{
				a = value;
			}
		}

		public int W
		{
			[CompilerGenerated]
			get
			{
				return B;
			}
			[CompilerGenerated]
			init
			{
				B = value;
			}
		}

		public int H
		{
			[CompilerGenerated]
			get
			{
				return b;
			}
			[CompilerGenerated]
			init
			{
				b = value;
			}
		}

		[CompilerGenerated]
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Box");
			stringBuilder.Append(" { ");
			if (A(stringBuilder))
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append('}');
			return stringBuilder.ToString();
		}

		[CompilerGenerated]
		private bool A(StringBuilder P_0)
		{
			P_0.Append("X = ");
			P_0.Append(X.ToString());
			P_0.Append(", Y = ");
			P_0.Append(Y.ToString());
			P_0.Append(", W = ");
			P_0.Append(W.ToString());
			P_0.Append(", H = ");
			P_0.Append(H.ToString());
			return true;
		}

		[CompilerGenerated]
		public static bool operator !=(Box left, Box right)
		{
			return !(left == right);
		}

		[CompilerGenerated]
		public static bool operator ==(Box left, Box right)
		{
			return left.Equals(right);
		}

		[CompilerGenerated]
		public override int GetHashCode()
		{
			return ((EqualityComparer<int>.Default.GetHashCode(this.m_A) * -1521134295 + EqualityComparer<int>.Default.GetHashCode(a)) * -1521134295 + EqualityComparer<int>.Default.GetHashCode(B)) * -1521134295 + EqualityComparer<int>.Default.GetHashCode(b);
		}

		[CompilerGenerated]
		public override bool Equals(object obj)
		{
			if (obj is Box)
			{
				return Equals((Box)obj);
			}
			return false;
		}

		[CompilerGenerated]
		public bool Equals(Box other)
		{
			if (EqualityComparer<int>.Default.Equals(this.m_A, other.m_A) && EqualityComparer<int>.Default.Equals(a, other.a) && EqualityComparer<int>.Default.Equals(B, other.B))
			{
				return EqualityComparer<int>.Default.Equals(b, other.b);
			}
			return false;
		}

		[CompilerGenerated]
		public void Deconstruct(out int X, out int Y, out int W, out int H)
		{
			X = this.X;
			Y = this.Y;
			W = this.W;
			H = this.H;
		}
	}

	internal sealed class Piece : IEquatable<Piece>
	{
		[CompilerGenerated]
		private readonly string m_A;

		[CompilerGenerated]
		private readonly int m_A;

		[CompilerGenerated]
		private readonly int a;

		[CompilerGenerated]
		private readonly Box? m_A;

		[CompilerGenerated]
		private readonly int B;

		[CompilerGenerated]
		private readonly int b;

		[CompilerGenerated]
		private Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(Piece);
			}
		}

		public string Id
		{
			[CompilerGenerated]
			get
			{
				return this.m_A;
			}
			[CompilerGenerated]
			init
			{
				this.m_A = value;
			}
		}

		public int W
		{
			[CompilerGenerated]
			get
			{
				return this.m_A;
			}
			[CompilerGenerated]
			init
			{
				this.m_A = value;
			}
		}

		public int H
		{
			[CompilerGenerated]
			get
			{
				return a;
			}
			[CompilerGenerated]
			init
			{
				a = value;
			}
		}

		public Box? Source
		{
			[CompilerGenerated]
			get
			{
				return this.m_A;
			}
			[CompilerGenerated]
			init
			{
				this.m_A = value;
			}
		}

		public int X
		{
			[CompilerGenerated]
			get
			{
				return B;
			}
			[CompilerGenerated]
			init
			{
				B = value;
			}
		}

		public int Y
		{
			[CompilerGenerated]
			get
			{
				return b;
			}
			[CompilerGenerated]
			init
			{
				b = value;
			}
		}

		public Piece(string Id, int W, int H, Box? Source, int X, int Y)
		{
			this.m_A = Id;
			this.m_A = W;
			a = H;
			this.m_A = Source;
			B = X;
			b = Y;
			base._002Ector();
		}

		[CompilerGenerated]
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Piece");
			stringBuilder.Append(" { ");
			if (A(stringBuilder))
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append('}');
			return stringBuilder.ToString();
		}

		[CompilerGenerated]
		private bool A(StringBuilder P_0)
		{
			RuntimeHelpers.EnsureSufficientExecutionStack();
			P_0.Append("Id = ");
			P_0.Append((object?)Id);
			P_0.Append(", W = ");
			P_0.Append(W.ToString());
			P_0.Append(", H = ");
			P_0.Append(H.ToString());
			P_0.Append(", Source = ");
			P_0.Append(Source.ToString());
			P_0.Append(", X = ");
			P_0.Append(X.ToString());
			P_0.Append(", Y = ");
			P_0.Append(Y.ToString());
			return true;
		}

		[CompilerGenerated]
		public static bool operator !=(Piece? left, Piece? right)
		{
			return !(left == right);
		}

		[CompilerGenerated]
		public static bool operator ==(Piece? left, Piece? right)
		{
			if ((object)left != right)
			{
				return left?.Equals(right) ?? false;
			}
			return true;
		}

		[CompilerGenerated]
		public override int GetHashCode()
		{
			return (((((EqualityComparer<Type>.Default.GetHashCode(EqualityContract) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.m_A)) * -1521134295 + EqualityComparer<int>.Default.GetHashCode(this.m_A)) * -1521134295 + EqualityComparer<int>.Default.GetHashCode(a)) * -1521134295 + EqualityComparer<Box?>.Default.GetHashCode(this.m_A)) * -1521134295 + EqualityComparer<int>.Default.GetHashCode(B)) * -1521134295 + EqualityComparer<int>.Default.GetHashCode(b);
		}

		[CompilerGenerated]
		public override bool Equals(object? obj)
		{
			return Equals(obj as Piece);
		}

		[CompilerGenerated]
		public bool Equals(Piece? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<string>.Default.Equals(this.m_A, other.m_A) && EqualityComparer<int>.Default.Equals(this.m_A, other.m_A) && EqualityComparer<int>.Default.Equals(a, other.a) && EqualityComparer<Box?>.Default.Equals(this.m_A, other.m_A) && EqualityComparer<int>.Default.Equals(B, other.B))
				{
					return EqualityComparer<int>.Default.Equals(b, other.b);
				}
				return false;
			}
			return true;
		}

		[CompilerGenerated]
		private Piece(Piece P_0)
		{
			this.m_A = P_0.m_A;
			this.m_A = P_0.m_A;
			a = P_0.a;
			this.m_A = P_0.m_A;
			B = P_0.B;
			b = P_0.b;
		}

		[CompilerGenerated]
		public void Deconstruct(out string Id, out int W, out int H, out Box? Source, out int X, out int Y)
		{
			Id = this.Id;
			W = this.W;
			H = this.H;
			Source = this.Source;
			X = this.X;
			Y = this.Y;
		}
	}

	internal sealed class Placement : IEquatable<Placement>
	{
		[CompilerGenerated]
		private readonly Piece m_A;

		[CompilerGenerated]
		private readonly Box m_A;

		[CompilerGenerated]
		private readonly bool m_A;

		[CompilerGenerated]
		private Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(Placement);
			}
		}

		public Piece Piece
		{
			[CompilerGenerated]
			get
			{
				return this.m_A;
			}
			[CompilerGenerated]
			init
			{
				this.m_A = value;
			}
		}

		public Box Target
		{
			[CompilerGenerated]
			get
			{
				return this.m_A;
			}
			[CompilerGenerated]
			init
			{
				this.m_A = value;
			}
		}

		public bool Unchanged
		{
			[CompilerGenerated]
			get
			{
				return this.m_A;
			}
			[CompilerGenerated]
			init
			{
				this.m_A = value;
			}
		}

		public Placement(Piece Piece, Box Target, bool Unchanged)
		{
			this.m_A = Piece;
			this.m_A = Target;
			this.m_A = Unchanged;
			base._002Ector();
		}

		[CompilerGenerated]
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Placement");
			stringBuilder.Append(" { ");
			if (A(stringBuilder))
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append('}');
			return stringBuilder.ToString();
		}

		[CompilerGenerated]
		private bool A(StringBuilder P_0)
		{
			RuntimeHelpers.EnsureSufficientExecutionStack();
			P_0.Append("Piece = ");
			P_0.Append(Piece);
			P_0.Append(", Target = ");
			P_0.Append(Target.ToString());
			P_0.Append(", Unchanged = ");
			P_0.Append(Unchanged.ToString());
			return true;
		}

		[CompilerGenerated]
		public static bool operator !=(Placement? left, Placement? right)
		{
			return !(left == right);
		}

		[CompilerGenerated]
		public static bool operator ==(Placement? left, Placement? right)
		{
			if ((object)left != right)
			{
				return left?.Equals(right) ?? false;
			}
			return true;
		}

		[CompilerGenerated]
		public override int GetHashCode()
		{
			return ((EqualityComparer<Type>.Default.GetHashCode(EqualityContract) * -1521134295 + EqualityComparer<GridPacking.Piece>.Default.GetHashCode(this.m_A)) * -1521134295 + EqualityComparer<Box>.Default.GetHashCode(this.m_A)) * -1521134295 + EqualityComparer<bool>.Default.GetHashCode(this.m_A);
		}

		[CompilerGenerated]
		public override bool Equals(object? obj)
		{
			return Equals(obj as Placement);
		}

		[CompilerGenerated]
		public bool Equals(Placement? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<GridPacking.Piece>.Default.Equals(this.m_A, other.m_A) && EqualityComparer<Box>.Default.Equals(this.m_A, other.m_A))
				{
					return EqualityComparer<bool>.Default.Equals(this.m_A, other.m_A);
				}
				return false;
			}
			return true;
		}

		[CompilerGenerated]
		private Placement(Placement P_0)
		{
			this.m_A = P_0.m_A;
			this.m_A = P_0.m_A;
			this.m_A = P_0.m_A;
		}

		[CompilerGenerated]
		public void Deconstruct(out Piece Piece, out Box Target, out bool Unchanged)
		{
			Piece = this.Piece;
			Target = this.Target;
			Unchanged = this.Unchanged;
		}
	}

	internal sealed class Result : IEquatable<Result>
	{
		[CompilerGenerated]
		private readonly List<Placement> m_A;

		[CompilerGenerated]
		private readonly List<Piece> m_A;

		[CompilerGenerated]
		private Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(Result);
			}
		}

		public List<Placement> Placed
		{
			[CompilerGenerated]
			get
			{
				return this.m_A;
			}
			[CompilerGenerated]
			init
			{
				this.m_A = value;
			}
		}

		public List<Piece> Skipped
		{
			[CompilerGenerated]
			get
			{
				return this.m_A;
			}
			[CompilerGenerated]
			init
			{
				this.m_A = value;
			}
		}

		public Result(List<Placement> Placed, List<Piece> Skipped)
		{
			this.m_A = Placed;
			this.m_A = Skipped;
			base._002Ector();
		}

		[CompilerGenerated]
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Result");
			stringBuilder.Append(" { ");
			if (A(stringBuilder))
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append('}');
			return stringBuilder.ToString();
		}

		[CompilerGenerated]
		private bool A(StringBuilder P_0)
		{
			RuntimeHelpers.EnsureSufficientExecutionStack();
			P_0.Append("Placed = ");
			P_0.Append(Placed);
			P_0.Append(", Skipped = ");
			P_0.Append(Skipped);
			return true;
		}

		[CompilerGenerated]
		public static bool operator !=(Result? left, Result? right)
		{
			return !(left == right);
		}

		[CompilerGenerated]
		public static bool operator ==(Result? left, Result? right)
		{
			if ((object)left != right)
			{
				return left?.Equals(right) ?? false;
			}
			return true;
		}

		[CompilerGenerated]
		public override int GetHashCode()
		{
			return (EqualityComparer<Type>.Default.GetHashCode(EqualityContract) * -1521134295 + EqualityComparer<List<Placement>>.Default.GetHashCode(this.m_A)) * -1521134295 + EqualityComparer<List<Piece>>.Default.GetHashCode(this.m_A);
		}

		[CompilerGenerated]
		public override bool Equals(object? obj)
		{
			return Equals(obj as Result);
		}

		[CompilerGenerated]
		public bool Equals(Result? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<List<Placement>>.Default.Equals(this.m_A, other.m_A))
				{
					return EqualityComparer<List<Piece>>.Default.Equals(this.m_A, other.m_A);
				}
				return false;
			}
			return true;
		}

		[CompilerGenerated]
		private Result(Result P_0)
		{
			this.m_A = P_0.m_A;
			this.m_A = P_0.m_A;
		}

		[CompilerGenerated]
		public void Deconstruct(out List<Placement> Placed, out List<Piece> Skipped)
		{
			Placed = this.Placed;
			Skipped = this.Skipped;
		}
	}

	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct _003C_003Ec__DisplayClass4_0
	{
		public int[] cells;

		public int width;

		public int height;
	}

	internal static Result A(int P_0, int P_1, IReadOnlyDictionary<string, Box> P_2, IEnumerable<Piece> P_3, Func<Piece, Box, bool> P_4)
	{
		_003C_003Ec__DisplayClass4_0 obj = default(_003C_003Ec__DisplayClass4_0);
		obj.width = P_0;
		obj.height = P_1;
		Dictionary<string, Box> dictionary = new Dictionary<string, Box>(P_2);
		obj.cells = new int[checked(obj.width * obj.height)];
		foreach (Box value2 in dictionary.Values)
		{
			A(value2, 1, ref obj);
		}
		List<Placement> list = new List<Placement>();
		List<Piece> list2 = new List<Piece>();
		foreach (Piece item in P_3)
		{
			bool flag = dictionary.Remove(item.Id, out var value);
			if (flag)
			{
				A(value, -1, ref obj);
			}
			Box? box = null;
			foreach (Box item2 in A(obj.width, obj.height, item))
			{
				if (item2.X >= 0 && item2.Y >= 0 && item2.X + item2.W <= obj.width && item2.Y + item2.H <= obj.height && A(item2, ref obj))
				{
					bool flag2 = flag && item2 == value;
					if ((!flag2 || (item2.X == item.X && item2.Y == item.Y)) && P_4(item, item2))
					{
						box = item2;
						list.Add(new Placement(item, item2, flag2));
						dictionary[item.Id] = item2;
						A(item2, 1, ref obj);
						break;
					}
				}
			}
			if (!box.HasValue)
			{
				if (flag)
				{
					dictionary[item.Id] = value;
					A(value, 1, ref obj);
				}
				list2.Add(item);
			}
		}
		return new Result(list, list2);
	}

	private static IEnumerable<Box> A(int P_0, int P_1, Piece P_2)
	{
		yield return new Box(P_2.X, P_2.Y, P_2.W, P_2.H);
		if (P_0 <= 0 || P_1 <= 0)
		{
			yield break;
		}
		int num = Math.Clamp(P_2.Y, 0, P_1 - 1) * P_0 + Math.Clamp(P_2.X, 0, P_0 - 1);
		for (int i = 0; i < P_0 * P_1; i++)
		{
			int num2 = (num + i) % (P_0 * P_1);
			int num3 = num2 % P_0;
			int num4 = num2 / P_0;
			if (num3 != P_2.X || num4 != P_2.Y)
			{
				yield return new Box(num3, num4, P_2.W, P_2.H);
			}
		}
	}

	internal static bool A(Box P_0, Box P_1)
	{
		if (P_0.X < P_1.X + P_1.W && P_0.X + P_0.W > P_1.X && P_0.Y < P_1.Y + P_1.H)
		{
			return P_0.Y + P_0.H > P_1.Y;
		}
		return false;
	}

	[CompilerGenerated]
	internal static void A(Box P_0, int P_1, ref _003C_003Ec__DisplayClass4_0 P_2)
	{
		for (int i = Math.Max(0, P_0.Y); i < Math.Min(P_2.height, P_0.Y + P_0.H); i++)
		{
			for (int j = Math.Max(0, P_0.X); j < Math.Min(P_2.width, P_0.X + P_0.W); j++)
			{
				P_2.cells[i * P_2.width + j] += P_1;
			}
		}
	}

	[CompilerGenerated]
	internal static bool A(Box P_0, ref _003C_003Ec__DisplayClass4_0 P_1)
	{
		for (int i = P_0.Y; i < P_0.Y + P_0.H; i++)
		{
			for (int j = P_0.X; j < P_0.X + P_0.W; j++)
			{
				if (P_1.cells[i * P_1.width + j] != 0)
				{
					return false;
				}
			}
		}
		return true;
	}
}
