using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using EFT.InventoryLogic;
using EFT.UI;
using UnityEngine;

namespace StashTools;

internal static class EquippedModding
{
	internal static bool Ready;

	private static ConfigEntry<bool> _plates;

	private static bool _logged;

	internal static bool Active
	{
		get
		{
			if (Ready)
			{
				return Plugin.Enabled.Value;
			}
			return false;
		}
	}

	internal static InventoryController? Controller
	{
		get
		{
			if (!((Object)(object)ItemUiContext.Instance == (Object)null))
			{
				return InventoryAccess.Controller(ItemUiContext.Instance).TryCast<InventoryController>();
			}
			return null;
		}
	}

	internal static void Configure(ConfigFile config)
	{
		_plates = config.Bind<bool>("10 穿戴改装", "穿着护甲更换可拆插板", true, ChineseSettings.ToggleDescription("在检视窗口拆装穿着护甲的可拆插板，也可直接拖到已有插板的槽位互换。双方必须兼容，旧板须能放回新板的原位置。固定内衬、物品锁、未搜索和所有权限制保持原规则；标准自定义插板按实际槽位适配。"));
	}

	internal static Item? Root(Item? item)
	{
		HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
		while (item != null && hashSet.Add(item.Identity()))
		{
			ItemAddress currentAddress = item.CurrentAddress;
			object obj;
			if (currentAddress == null)
			{
				obj = null;
			}
			else
			{
				IContainer container = currentAddress.Container;
				obj = ((container != null) ? container.ParentItem : null);
			}
			Item val = (Item)obj;
			if (val == null || val.TryCast<InventoryEquipment>() != null)
			{
				return item;
			}
			item = val;
		}
		return null;
	}

	internal static bool Owned(Item? item, InventoryController? controller)
	{
		if (((item != null) ? item.Owner : null) != null && controller != null)
		{
			return item.Owner.Identity() == controller.Identity();
		}
		return false;
	}

	private static bool Pinned(Item? item)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		if (item != null)
		{
			if ((int)item.PinLockState == 0)
			{
				return item.QuestItem;
			}
			return true;
		}
		return false;
	}

	internal static bool UnlockedAncestry(Item item)
	{
		HashSet<IntPtr> hashSet = new HashSet<IntPtr>();
		Item val = item;
		while (val != null && hashSet.Add(val.Identity()))
		{
			if (Pinned(val))
			{
				return false;
			}
			SlotItemAddress val2 = val.CurrentAddress?.TryCast<SlotItemAddress>();
			if (val2 != null && (val2.Slot.Locked || ArmorValuation.FixedSlot((IContainer)(object)val2.Slot)))
			{
				return false;
			}
			if (val.TryCast<InventoryEquipment>() != null)
			{
				return true;
			}
			ItemAddress currentAddress = val.CurrentAddress;
			object obj;
			if (currentAddress == null)
			{
				obj = null;
			}
			else
			{
				IContainer container = currentAddress.Container;
				obj = ((container != null) ? container.ParentItem : null);
			}
			val = (Item)obj;
		}
		return true;
	}

	internal static bool PlateSlot(Slot? slot, InventoryController? controller = null)
	{
		if (!Active || slot == null)
		{
			return false;
		}
		Item item = Root(slot.ParentItem);
		bool plateSlot = slot.TryCast<ArmorSlot>() != null || slot.ContainedItem?.TryCast<ArmorPlate>() != null;
		return ModdingPolicy.Plate(_plates.Value, Owned(item, controller ?? Controller), slot.Locked, ArmorValuation.FixedSlot((IContainer)(object)slot), plateSlot, Pinned(item) || Pinned(slot.ContainedItem) || !UnlockedAncestry(slot.ParentItem));
	}

	internal static bool PlateMove(Item item, Slot? to, InventoryController controller)
	{
		if (!Active || !_plates.Value || item.TryCast<ArmorPlate>() == null || !Owned(item, controller) || !UnlockedAncestry(item))
		{
			return false;
		}
		Slot val = item.CurrentAddress?.TryCast<SlotItemAddress>()?.Slot;
		if ((val != null || to != null) && (val == null || PlateSlot(val, controller)))
		{
			if (to != null)
			{
				return PlateSlot(to, controller);
			}
			return true;
		}
		return false;
	}

	internal static void Failed(Exception ex)
	{
		Ready = false;
		if (!_logged)
		{
			_logged = true;
			Plugin.Logger.LogError((object)("插板换装已停止，恢复原生规则，其他功能继续运行：" + ex));
		}
	}
}
