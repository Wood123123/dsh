using EFT.InventoryLogic;

namespace StashTools;

internal static class RaidPrices
{
	private static readonly PriceBook Book = new PriceBook();

	internal static void Reset()
	{
		Book.Reset();
	}

	internal static void Resume()
	{
		Book.Resume();
	}

	internal static void Freeze()
	{
		Book.Freeze();
	}

	internal static void RememberSession()
	{
		Book.RememberSession();
	}

	internal static void Register(Item item)
	{
		Book.Register(item);
	}

	internal static double? Get(string template)
	{
		return Book.Get(template);
	}

	internal static void Tick()
	{
		Book.Tick();
	}
}
