using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace StashTools;

internal sealed class RaidLedger
{
	internal readonly struct Sample(string Id, string Template, double Units) : IEquatable<Sample>
	{
		[CompilerGenerated]
		private readonly string m_A = Id;

		[CompilerGenerated]
		private readonly string a = Template;

		[CompilerGenerated]
		private readonly double m_A = Units;

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

		[CompilerGenerated]
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("Sample");
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
			return true;
		}

		[CompilerGenerated]
		public static bool operator !=(Sample left, Sample right)
		{
			return !(left == right);
		}

		[CompilerGenerated]
		public static bool operator ==(Sample left, Sample right)
		{
			return left.Equals(right);
		}

		[CompilerGenerated]
		public override int GetHashCode()
		{
			return (EqualityComparer<string>.Default.GetHashCode(this.m_A) * -1521134295 + EqualityComparer<string>.Default.GetHashCode(a)) * -1521134295 + EqualityComparer<double>.Default.GetHashCode(this.m_A);
		}

		[CompilerGenerated]
		public override bool Equals(object obj)
		{
			if (obj is Sample)
			{
				return Equals((Sample)obj);
			}
			return false;
		}

		[CompilerGenerated]
		public bool Equals(Sample other)
		{
			if (EqualityComparer<string>.Default.Equals(this.m_A, other.m_A) && EqualityComparer<string>.Default.Equals(a, other.a))
			{
				return EqualityComparer<double>.Default.Equals(this.m_A, other.m_A);
			}
			return false;
		}

		[CompilerGenerated]
		public void Deconstruct(out string Id, out string Template, out double Units)
		{
			Id = this.Id;
			Template = this.Template;
			Units = this.Units;
		}
	}

	private sealed class Lot
	{
		internal bool A;

		internal double A;

		internal Lot(bool P_0, double P_1)
		{
			this.A = P_0;
			A = P_1;
		}
	}

	private sealed class Stack
	{
		internal string A;

		internal bool A;

		internal readonly List<Lot> A = new List<Lot>();

		internal double Units => A.Sum((Lot P_0) => P_0.A);

		internal Stack(string P_0)
		{
			this.A = P_0;
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

		public double Income
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

		public double Expense
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

		internal double Net => Income + Expense;

		public Totals(double Income, double Expense, int Pending, int Unpriced)
		{
			this.m_A = Income;
			this.a = Expense;
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
			P_0.Append("Income = ");
			P_0.Append(Income.ToString());
			P_0.Append(", Expense = ");
			P_0.Append(Expense.ToString());
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
		public void Deconstruct(out double Income, out double Expense, out int Pending, out int Unpriced)
		{
			Income = this.Income;
			Expense = this.Expense;
			Pending = this.Pending;
			Unpriced = this.Unpriced;
		}
	}

	private readonly Dictionary<string, Stack> m_A = new Dictionary<string, Stack>();

	private readonly Dictionary<string, double> m_A = new Dictionary<string, double>();

	internal void A(IEnumerable<Sample> P_0)
	{
		this.m_A.Clear();
		this.m_A.Clear();
		foreach (Sample item in P_0.DistinctBy((Sample sample) => sample.Id))
		{
			A(item, true).A = true;
			this.m_A[item.Template] = this.m_A.GetValueOrDefault(item.Template) + item.Units;
		}
	}

	private Stack A(Sample P_0, bool P_1 = false)
	{
		if (this.m_A.TryGetValue(P_0.Id, out Stack value))
		{
			return value;
		}
		value = new Stack(P_0.Template);
		value.A.Add(new Lot(P_1, Math.Max(0.0, P_0.Units)));
		this.m_A.Add(P_0.Id, value);
		return value;
	}

	private static List<Lot> A(Stack P_0, double P_1)
	{
		List<Lot> list = new List<Lot>();
		int num = P_0.A.Count - 1;
		while (num >= 0 && P_1 > 1E-08)
		{
			Lot lot = P_0.A[num];
			double num2 = Math.Min(P_1, lot.A);
			list.Insert(0, new Lot(lot.A, num2));
			lot.A -= num2;
			P_1 -= num2;
			if (lot.A <= 1E-08)
			{
				P_0.A.RemoveAt(num);
			}
			num--;
		}
		return list;
	}

	private static void a(Stack P_0, double P_1)
	{
		double num = Math.Max(0.0, P_1) - P_0.Units;
		if (num < -1E-08)
		{
			A(P_0, 0.0 - num);
		}
		else if (num > 1E-08)
		{
			P_0.A.Add(new Lot(false, num));
		}
	}

	internal void A(Sample P_0, Sample P_1, double P_2)
	{
		if (!(P_0.Id == P_1.Id) && !(P_0.Template != P_1.Template) && !(P_2 <= 0.0))
		{
			Stack stack = A(P_0);
			Stack stack2 = A(P_1);
			a(stack, P_0.Units);
			a(stack2, P_1.Units);
			stack2.A.AddRange(A(stack, Math.Min(P_2, stack.Units)));
		}
	}

	internal void a(IEnumerable<Sample> P_0)
	{
		foreach (Stack value in this.m_A.Values)
		{
			value.A = false;
		}
		foreach (Sample item in P_0.DistinctBy((Sample sample) => sample.Id))
		{
			Stack stack = A(item);
			a(stack, item.Units);
			stack.A = true;
		}
	}

	internal (bool Held, double Brought) A(string id)
	{
		if (!this.m_A.TryGetValue(id, out Stack value))
		{
			return (Held: false, Brought: 0.0);
		}
		return (Held: value.A, Brought: value.A.Where((Lot P_0) => P_0.A).Sum((Lot P_0) => P_0.A));
	}

	internal Totals A(Func<string, double?> P_0)
	{
		Dictionary<string, double> dictionary = new Dictionary<string, double>();
		Dictionary<string, double> dictionary2 = new Dictionary<string, double>();
		foreach (Stack item in this.m_A.Values.Where((Stack stack) => stack.A))
		{
			foreach (Lot item2 in item.A)
			{
				Dictionary<string, double> dictionary3 = (item2.A ? dictionary : dictionary2);
				dictionary3[item.A] = dictionary3.GetValueOrDefault(item.A) + item2.A;
			}
		}
		double num = 0.0;
		double num2 = 0.0;
		int num3 = 0;
		int num4 = 0;
		foreach (string item3 in this.m_A.Keys.Concat<string>(dictionary2.Keys).Distinct())
		{
			double valueOrDefault = dictionary2.GetValueOrDefault(item3);
			double num5 = Math.Max(0.0, this.m_A.GetValueOrDefault(item3) - dictionary.GetValueOrDefault(item3));
			if (!(valueOrDefault < 1E-08) || !(num5 < 1E-08))
			{
				double? num6 = P_0(item3);
				if (!num6.HasValue)
				{
					num3++;
					continue;
				}
				if (num6 <= 0.0 || !double.IsFinite(num6.Value))
				{
					num4++;
					continue;
				}
				num += valueOrDefault * num6.Value;
				num2 -= num5 * num6.Value;
			}
		}
		return new Totals(num, num2, num3, num4);
	}

	internal static string A(double P_0, RaidAmountUnit P_1)
	{
		double num = Math.Round(P_0, MidpointRounding.AwayFromZero);
		string obj = ((num < 0.0) ? "−" : "");
		num = Math.Abs(num);
		string text = obj;
		return text + P_1 switch
		{
			RaidAmountUnit.千位K => (num / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + "K", 
			RaidAmountUnit.百万M => (num / 1000000.0).ToString("0.######", CultureInfo.InvariantCulture) + "M", 
			_ => num.ToString("N0", CultureInfo.InvariantCulture), 
		};
	}
}
