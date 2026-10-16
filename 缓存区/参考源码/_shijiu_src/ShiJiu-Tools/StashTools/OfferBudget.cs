using System;

namespace StashTools;

internal sealed class OfferBudget
{
	private int m_A;

	internal OfferBudget(int P_0, int P_1)
	{
		this.m_A = Math.Max(0, P_0 - P_1);
	}

	internal bool A(int P_0, int P_1)
	{
		if (this.m_A > 0)
		{
			return P_1 < P_0;
		}
		return false;
	}

	internal void A()
	{
		this.m_A = Math.Max(0, this.m_A - 1);
	}
}
