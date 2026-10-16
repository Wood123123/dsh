using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace StashTools;

internal static class PreviewIconGeometry
{
	internal readonly struct Shape(float Width, float Height, float OffsetX, float OffsetY) : IEquatable<Shape>
	{
		[CompilerGenerated]
		private readonly float m_A = Width;

		[CompilerGenerated]
		private readonly float a = Height;

		[CompilerGenerated]
		private readonly float B = OffsetX;

		[CompilerGenerated]
		private readonly float b = OffsetY;

		public float Width
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

		public float Height
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

		public float OffsetX
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

		public float OffsetY
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
			stringBuilder.Append("Shape");
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
			P_0.Append("Width = ");
			P_0.Append(Width.ToString());
			P_0.Append(", Height = ");
			P_0.Append(Height.ToString());
			P_0.Append(", OffsetX = ");
			P_0.Append(OffsetX.ToString());
			P_0.Append(", OffsetY = ");
			P_0.Append(OffsetY.ToString());
			return true;
		}

		[CompilerGenerated]
		public static bool operator !=(Shape left, Shape right)
		{
			return !(left == right);
		}

		[CompilerGenerated]
		public static bool operator ==(Shape left, Shape right)
		{
			return left.Equals(right);
		}

		[CompilerGenerated]
		public override int GetHashCode()
		{
			return ((EqualityComparer<float>.Default.GetHashCode(this.m_A) * -1521134295 + EqualityComparer<float>.Default.GetHashCode(a)) * -1521134295 + EqualityComparer<float>.Default.GetHashCode(B)) * -1521134295 + EqualityComparer<float>.Default.GetHashCode(b);
		}

		[CompilerGenerated]
		public override bool Equals(object obj)
		{
			if (obj is Shape)
			{
				return Equals((Shape)obj);
			}
			return false;
		}

		[CompilerGenerated]
		public bool Equals(Shape other)
		{
			if (EqualityComparer<float>.Default.Equals(this.m_A, other.m_A) && EqualityComparer<float>.Default.Equals(a, other.a) && EqualityComparer<float>.Default.Equals(B, other.B))
			{
				return EqualityComparer<float>.Default.Equals(b, other.b);
			}
			return false;
		}

		[CompilerGenerated]
		public void Deconstruct(out float Width, out float Height, out float OffsetX, out float OffsetY)
		{
			Width = this.Width;
			Height = this.Height;
			OffsetX = this.OffsetX;
			OffsetY = this.OffsetY;
		}
	}

	internal static bool A(string P_0, string P_1, bool P_2)
	{
		if (!P_2)
		{
			return P_0 != P_1;
		}
		return false;
	}

	internal static Shape A(float P_0, float P_1, float P_2, float P_3, float P_4, float P_5, float P_6)
	{
		if (!float.IsFinite(P_0) || !float.IsFinite(P_1) || !float.IsFinite(P_2) || !float.IsFinite(P_3) || !float.IsFinite(P_4) || !float.IsFinite(P_5) || !float.IsFinite(P_6) || P_0 <= 0f || P_1 <= 0f || P_2 <= 0f || P_3 <= 0f || P_6 <= 0f)
		{
			throw new ArgumentException("预览图标尺寸无效");
		}
		return new Shape(P_2 / P_6, P_3 / P_6, (P_4 + P_2 / 2f - P_0 / 2f) / P_6, (P_1 / 2f - P_5 - P_3 / 2f) / P_6);
	}
}
