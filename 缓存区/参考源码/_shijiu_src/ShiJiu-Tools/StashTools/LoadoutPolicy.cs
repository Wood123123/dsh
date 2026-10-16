using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace StashTools;

internal static class LoadoutPolicy
{
	internal readonly struct Entry(string Id, string Template, double Units, bool Equipment) : IEquatable<Entry>
	{
		[CompilerGenerated]
		private readonly string m_A = Id;

		[CompilerGenerated]
		private readonly string a = Template;

		[CompilerGenerated]
		private readonly double m_A = Units;

		[CompilerGenerated]
		private readonly bool m_A = Equipment;

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

		public string Template
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

		public double Units
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

		public bool Equipment
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

		[CompilerGenerated]
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Entry");
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
			P_0.Append("Id = ");
			P_0.Append((object?)Id);
			P_0.Append(", Template = ");
			P_0.Append((object?)Template);
			P_0.Append(", Units = ");
			P_0.Append(Units.ToString());
			P_0.Append(", Equipment = ");
			P_0.Append(Equipment.ToString());
			return true;
		}

		[CompilerGenerated]
		public static bool operator !=(Entry left, Entry right)
		{
			return !(left == right);
		}

		[CompilerGenerated]
		public static bool operator ==(Entry left, Entry right)
		{
			return left.Equals(right);
		}

		[CompilerGenerated]
		public override int GetHashCode()
		{
			return ((EqualityComparer<string>.Default.GetHashCode(this.m_A) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(a)) * -1521134295 + EqualityComparer<double>.Default.GetHashCode(this.m_A)) * -1521134295 + EqualityComparer<bool>.Default.GetHashCode(this.m_A);
		}

		[CompilerGenerated]
		public override bool Equals(object obj)
		{
			if (obj is Entry)
			{
				return Equals((Entry)obj);
			}
			return false;
		}

		[CompilerGenerated]
		public bool Equals(Entry other)
		{
			if (EqualityComparer<string>.Default.Equals(this.m_A, other.m_A) && EqualityComparer<string>.Default.Equals(a, other.a) && EqualityComparer<double>.Default.Equals(this.m_A, other.m_A))
			{
				return EqualityComparer<bool>.Default.Equals(this.m_A, other.m_A);
			}
			return false;
		}

		[CompilerGenerated]
		public void Deconstruct(out string Id, out string Template, out double Units, out bool Equipment)
		{
			Id = this.Id;
			Template = this.Template;
			Units = this.Units;
			Equipment = this.Equipment;
		}
	}

	internal sealed class Totals : IEquatable<Totals>
	{
		[CompilerGenerated]
		private readonly double m_A;

		[CompilerGenerated]
		private readonly double a;

		[CompilerGenerated]
		private readonly int m_A;

		[CompilerGenerated]
		private readonly int a;

		[CompilerGenerated]
		private Type EqualityContract
		{
			[CompilerGenerated]
			get
			{
				return typeof(Totals);
			}
		}

		public double Equipment
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

		public double Supplies
		{
			[CompilerGenerated]
			get
			{
				return this.a;
			}
			[CompilerGenerated]
			init
			{
				this.a = value;
			}
		}

		public int Pending
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

		public int Unpriced
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

		internal double Total => Equipment + Supplies;

		public Totals(double Equipment, double Supplies, int Pending, int Unpriced)
		{
			this.m_A = Equipment;
			this.a = Supplies;
			this.m_A = Pending;
			a = Unpriced;
			base._002Ector();
		}

		[CompilerGenerated]
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Totals");
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
			P_0.Append("Equipment = ");
			P_0.Append(Equipment.ToString());
			P_0.Append(", Supplies = ");
			P_0.Append(Supplies.ToString());
			P_0.Append(", Pending = ");
			P_0.Append(Pending.ToString());
			P_0.Append(", Unpriced = ");
			P_0.Append(Unpriced.ToString());
			return true;
		}

		[CompilerGenerated]
		public static bool operator !=(Totals? left, Totals? right)
		{
			return !(left == right);
		}

		[CompilerGenerated]
		public static bool operator ==(Totals? left, Totals? right)
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
			return (((EqualityComparer<Type>.Default.GetHashCode(EqualityContract) * -1521134295 + EqualityComparer<double>.Default.GetHashCode(this.m_A)) * -1521134295 + EqualityComparer<double>.Default.GetHashCode(this.a)) * -1521134295 + EqualityComparer<int>.Default.GetHashCode(this.m_A)) * -1521134295 + EqualityComparer<int>.Default.GetHashCode(a);
		}

		[CompilerGenerated]
		public override bool Equals(object? obj)
		{
			return Equals(obj as Totals);
		}

		[CompilerGenerated]
		public bool Equals(Totals? other)
		{
			if ((object)this != other)
			{
				if ((object)other != null && EqualityContract == other.EqualityContract && EqualityComparer<double>.Default.Equals(this.m_A, other.m_A) && EqualityComparer<double>.Default.Equals(this.a, other.a) && EqualityComparer<int>.Default.Equals(this.m_A, other.m_A))
				{
					return EqualityComparer<int>.Default.Equals(a, other.a);
				}
				return false;
			}
			return true;
		}

		[CompilerGenerated]
		private Totals(Totals P_0)
		{
			this.m_A = P_0.m_A;
			this.a = P_0.a;
			this.m_A = P_0.m_A;
			a = P_0.a;
		}

		[CompilerGenerated]
		public void Deconstruct(out double Equipment, out double Supplies, out int Pending, out int Unpriced)
		{
			Equipment = this.Equipment;
			Supplies = this.Supplies;
			Pending = this.Pending;
			Unpriced = this.Unpriced;
		}
	}

	internal static bool A(string P_0)
	{
		switch (P_0)
		{
		case "Headwear":
		case "Scabbard":
		case "Backpack":
		case "Earpiece":
		case "ArmorVest":
		case "FaceCover":
		case "Eyewear":
		case "Holster":
		case "TacticalVest":
		case "FirstPrimaryWeapon":
		case "SecondPrimaryWeapon":
			return true;
		default:
			return false;
		}
	}

	internal static bool A(string P_0, bool P_1)
	{
		if (P_0 != "Dogtag" && P_0 != "Pockets" && P_0 != "SecuredContainer")
		{
			return (P_0 != "Scabbard") | P_1;
		}
		return false;
	}

	internal static bool A(bool P_0, bool P_1, bool P_2, bool P_3)
	{
		if (P_0 && !P_1 && !P_2)
		{
			return !P_3;
		}
		return false;
	}

	internal static Totals A(IEnumerable<Entry> P_0, Func<string, double?> P_1)
	{
		double num = 0.0;
		double num2 = 0.0;
		HashSet<string> hashSet = new HashSet<string>();
		HashSet<string> hashSet2 = new HashSet<string>();
		foreach (Entry item in P_0.DistinctBy((Entry entry) => entry.Id))
		{
			if (!(item.Units <= 0.0))
			{
				double? num3 = P_1(item.Template);
				if (!num3.HasValue)
				{
					hashSet.Add(item.Template);
				}
				else if (num3 <= 0.0 || !double.IsFinite(num3.Value))
				{
					hashSet2.Add(item.Template);
				}
				else if (item.Equipment)
				{
					num += num3.Value * item.Units;
				}
				else
				{
					num2 += num3.Value * item.Units;
				}
			}
		}
		return new Totals(num, num2, hashSet.Count, hashSet2.Count);
	}
}
