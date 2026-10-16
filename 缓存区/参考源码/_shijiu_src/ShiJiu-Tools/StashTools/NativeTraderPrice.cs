using EFT;
using EFT.InventoryLogic;
using EFT.Trading;

namespace StashTools;

internal static class NativeTraderPrice
{
	internal static ItemPrice? Read(Trader trader, Item item)
	{
		return trader.GetUserItemPrice(item);
	}

	internal static string? Currency(ItemPrice price)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		ref MongoID? currencyId = ref price.CurrencyId;
		if (!currencyId.HasValue)
		{
			return null;
		}
		return ((object)currencyId.GetValueOrDefault()/*cast due to constrained. prefix*/).ToString();
	}
}
