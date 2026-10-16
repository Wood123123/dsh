namespace StashTools;

internal readonly record struct MarketQuote(double Min, double Average, double Max)
{
	internal double Select(FleaPriceBasis basis)
	{
		return MarketPricing.A(basis, Min, Average, Max);
	}
}
