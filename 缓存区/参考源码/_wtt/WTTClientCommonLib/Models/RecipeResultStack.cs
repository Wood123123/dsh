using EFT.InventoryLogic;

namespace WTTClientCommonLib.Models;

public class RecipeResultStack
{
	public Item Item;

	public int Count;

	public int? MinStackCount;

	public int? MaxStackCount;
}
