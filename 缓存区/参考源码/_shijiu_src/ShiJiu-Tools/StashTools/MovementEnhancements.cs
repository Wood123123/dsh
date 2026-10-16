using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Diz.Binding;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StashTools;

internal static class MovementEnhancements
{
	internal sealed record Step(Item Item, ItemAddress To, string From);

	internal sealed record Plan(Step[] Steps)
	{
		internal Item First => Steps[0].Item;

		internal ItemAddress To => Steps[0].To;
	}

	internal static bool Ready;

	private static ConfigEntry<bool> _equip = null;

	private static ConfigEntry<bool> _swap = null;

	private static ItemView? _source;

	private static bool _previewFailed;

	private static string _previewKey = "";

	private static float _previewAt;

	private static Plan? _preview;

	internal static bool EquipEnabled
	{
		get
		{
			if (Ready)
			{
				return _equip.Value;
			}
			return false;
		}
	}

	internal static bool Active
	{
		get
		{
			if (Ready && StashBehaviour.Active && !TraderSelection.Active && !ChineseSettings.IsOpen)
			{
				return !Batch.Busy;
			}
			return false;
		}
	}

	internal static void Configure(ConfigFile config)
	{
		_equip = config.Bind<bool>("09 移动增强", "Ctrl 快速移动优先穿戴", true, ChineseSettings.ToggleDescription("对应装备位空着时优先穿戴，主副武器按顺序判断；已有装备不替换，继续原版快速收纳。"));
		_swap = config.Bind<bool>("09 移动增强", "拖动交换物品位置", true, ChineseSettings.ToggleDescription("按拖动预览的完整占地交换，覆盖到的小物品放回腾出的原位置，保持方向。局外自有库存支持一件换多件；装填、装配、维修、堆叠、装入容器及套包保持原操作。"));
	}

	internal static void Begin(ItemView view)
	{
		_source = view;
		_preview = null;
		_previewKey = "";
		_previewAt = 0f;
		_previewFailed = false;
	}

	internal static void End()
	{
		_source = null;
		_preview = null;
		_previewKey = "";
	}

	internal static void PreviewFailed(Exception ex)
	{
		if (!_previewFailed)
		{
			_previewFailed = true;
			_preview = null;
			_previewKey = "";
			Plugin.Logger.LogWarning((object)("移动增强预览已停止，松手时仍会独立检查目标：" + ex));
		}
	}

	private static GridPacking.Box Box(Item item, GridItemAddress address)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		LocationInGrid locationInGrid = address.LocationInGrid;
		IntVec2 val = item.CalculateRotatedSize(locationInGrid.r);
		return new GridPacking.Box(locationInGrid.x, locationInGrid.y, val.X, val.Y);
	}

	private static bool Inside(Item child, string id)
	{
		HashSet<string> hashSet = new HashSet<string>();
		Item val = child;
		while (val != null && hashSet.Add(val.Id))
		{
			if (val.Id == id)
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
		return false;
	}

	private static bool Related(Item a, Item b)
	{
		if (!Inside(a, b.Id))
		{
			return Inside(b, a.Id);
		}
		return true;
	}

	private static string AddressKey(Item item)
	{
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		ItemAddress currentAddress = item.CurrentAddress;
		GridItemAddress val = currentAddress?.TryCast<GridItemAddress>();
		SlotItemAddress val2 = currentAddress?.TryCast<SlotItemAddress>();
		if (val == null)
		{
			if (val2 == null)
			{
				return "";
			}
			return $"slot:{val2.Slot.Identity()}";
		}
		return $"{val.Grid.Identity()}:{val.LocationInGrid.x}:{val.LocationInGrid.y}:{val.LocationInGrid.r}";
	}

	private static Plan MakePlan(Item a, Item b, ItemAddress to, ItemAddress to2)
	{
		return new Plan(new Step[2]
		{
			new Step(a, to, AddressKey(a)),
			new Step(b, to2, AddressKey(b))
		});
	}

	private static bool Fits(Item item, GridItemAddress to, params Item[] released)
	{
		Grid grid = to.Grid;
		int gridWidth = grid.GridWidth;
		int gridHeight = grid.GridHeight;
		bool[] array = ReadLayout(grid);
		if (array != null)
		{
			return MovementPolicy.A(gridWidth, gridHeight, array, Box(item, to), Released(grid, released));
		}
		return false;
	}

	private static GridPacking.Box[] Released(Grid grid, Item[] released)
	{
		return (from i in released
			select (Item: i, Address: i.CurrentAddress?.TryCast<GridItemAddress>()) into p
			where p.Address?.Grid.Identity() == grid.Identity()
			select Box(p.Item, p.Address)).ToArray();
	}

	private static bool[]? ReadLayout(Grid grid)
	{
		int gridWidth = grid.GridWidth;
		int gridHeight = grid.GridHeight;
		if (gridWidth <= 0 || gridHeight <= 0 || (long)gridWidth * (long)gridHeight > 16384)
		{
			return null;
		}
		IReadOnlyList<bool> layout = grid.Layout;
		if (layout == null || layout.Count < (long)gridWidth * (long)gridHeight)
		{
			return null;
		}
		bool[] array = new bool[gridWidth * gridHeight];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = layout[i];
		}
		return array;
	}

	private static ItemAddress? Destination(Item item, ItemAddress from, ItemRotation rotation)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		GridItemAddress val = from.TryCast<GridItemAddress>();
		if (val != null)
		{
			LocationInGrid locationInGrid = val.LocationInGrid;
			return (ItemAddress?)new ProtectedGridItemAddress(val.Grid, new LocationInGrid(locationInGrid.x, locationInGrid.y, rotation));
		}
		SlotItemAddress val2 = from.TryCast<SlotItemAddress>();
		if (val2 != null)
		{
			return (ItemAddress?)new ProtectedSlotItemAddress(val2.Slot);
		}
		return null;
	}

	internal static bool AddressAccepts(Item item, ItemAddress address, params Item[] released)
	{
		GridItemAddress val = address.TryCast<GridItemAddress>();
		if (val != null)
		{
			if (val.Grid.CheckCompatibility(item))
			{
				return Fits(item, val, released);
			}
			return false;
		}
		SlotItemAddress val2 = address.TryCast<SlotItemAddress>();
		if (val2 != null && !val2.Slot.Locked)
		{
			return val2.Slot.CheckCompatibility(item);
		}
		return false;
	}

	private static bool Single(Item item)
	{
		if (Selection.Contains(item))
		{
			return Selection.Count < 2;
		}
		return true;
	}

	private static bool CanExchange(GridView view, DragItemContext drag, Item other)
	{
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		Item item = ((ItemContext)drag).Item;
		ItemView val = view.FindItemView(other, (Predicate<ItemView>)((ItemView _) => true));
		if ((Object)(object)val == (Object)null || !val.IsSearched || !val.IsInteractable || ((BaseBindable<Error>)(object)val.RemoveError)?.Value != null)
		{
			return false;
		}
		int num;
		if (!val.CanInteract(drag) && (item.TryCast<Ammo>() == null || (other.TryCast<Magazine>() == null && other.TryCast<Weapon>() == null)) && item.TryCast<RepairKit>() == null)
		{
			if (item.TryCast<Mod>() != null)
			{
				CompoundItem? obj = other.TryCast<CompoundItem>();
				num = ((obj != null && obj.Slots?.Length > 0) ? 1 : 0);
			}
			else
			{
				num = 0;
			}
		}
		else
		{
			num = 1;
		}
		CompoundItem? obj2 = other.TryCast<CompoundItem>();
		return MovementPolicy.A((byte)num != 0, obj2 != null && obj2.Grids?.Length > 0, item.TemplateId == other.TemplateId && (item.StackMaxSize > 1 || other.StackMaxSize > 1), Related(item, other), !Moves.Eligible(other), !Single(item));
	}

	private static bool GroupOwnerAllowed(ItemController controller, IEnumerable<Item> items, IEnumerable<ItemAddress> addresses)
	{
		if (controller.TryCast<BackEndInventoryController>() == null)
		{
			return false;
		}
		if (items.All((Item i) => i.Owner?.Identity() == controller.Identity() && Moves.Eligible(i)))
		{
			return addresses.All(delegate(ItemAddress a)
			{
				GridItemAddress? obj = a.TryCast<GridItemAddress>();
				return ((obj == null) ? ((IntPtr?)null) : ((Item)obj.Grid.ParentItem).Owner?.Identity()) == controller.Identity();
			});
		}
		return false;
	}

	internal static Plan? Build(GridView view, DragItemContext drag, ItemContext? target)
	{
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Expected O, but got Unknown
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Expected O, but got Unknown
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0402: Unknown result type (might be due to invalid IL or missing references)
		//IL_040c: Expected O, but got Unknown
		//IL_0407: Unknown result type (might be due to invalid IL or missing references)
		//IL_040e: Expected O, but got Unknown
		if (!Active || !_swap.Value || !Single(((ItemContext)drag).Item) || view._nonInteractable || (Object)(object)view.TryCast<TradingTableGridView>() != (Object)null || !Moves.Eligible(((ItemContext)drag).Item) || (Object)(object)_source == (Object)null || !_source.IsSearched || !_source.IsInteractable || ((BaseBindable<Error>)(object)_source.RemoveError)?.Value != null)
		{
			return null;
		}
		Item item = ((ItemContext)drag).Item;
		Grid grid = view.Grid;
		ItemAddress currentAddress = item.CurrentAddress;
		if (grid == null || currentAddress == null || Inside((Item)(object)grid.ParentItem, item.Id) || !grid.CheckCompatibility(item))
		{
			return null;
		}
		LocationInGrid val = view.CalculateItemLocation(drag);
		if (val == (LocationInGrid)null)
		{
			return null;
		}
		ProtectedGridItemAddress val2 = new ProtectedGridItemAddress(grid, new LocationInGrid(val.x, val.y, drag.ItemRotation));
		GridPacking.Box box = Box(item, (GridItemAddress)(object)val2);
		List<Item> list = new List<Item>();
		foreach (Item item2 in grid.Items)
		{
			if (!(item2.Id == item.Id))
			{
				GridItemAddress val3 = item2.CurrentAddress?.TryCast<GridItemAddress>();
				if (val3 == null || val3.Grid.Identity() != grid.Identity())
				{
					return null;
				}
				if (GridPacking.A(box, Box(item2, val3)))
				{
					list.Add(item2);
				}
			}
		}
		if (list.Count == 0 || list.Count > 32)
		{
			return null;
		}
		if (target?.Item != null && target.Item.Id != item.Id && !CanExchange(view, drag, target.Item))
		{
			return null;
		}
		if (list.Any((Item other) => !CanExchange(view, drag, other)))
		{
			return null;
		}
		Item[] released = ((IEnumerable<Item>)(object)new Item[1] { item }).Concat(list).ToArray();
		if (!AddressAccepts(item, (ItemAddress)(object)val2, released))
		{
			return null;
		}
		GridItemAddress sourceGrid = currentAddress.TryCast<GridItemAddress>();
		if (sourceGrid == null)
		{
			if (list.Count != 1)
			{
				return null;
			}
			Item val4 = list[0];
			GridItemAddress val5 = val4.CurrentAddress.Cast<GridItemAddress>();
			ItemAddress val6 = Destination(val4, currentAddress, val5.LocationInGrid.r);
			if (val6 == null || !AddressAccepts(val4, val6, released))
			{
				return null;
			}
			return MakePlan(item, val4, (ItemAddress)(object)val2, val6);
		}
		if (list.Any((Item other) => !sourceGrid.Grid.CheckCompatibility(other) || Inside((Item)(object)sourceGrid.Grid.ParentItem, other.Id)))
		{
			return null;
		}
		bool[] array = ReadLayout(sourceGrid.Grid);
		if (array == null)
		{
			return null;
		}
		GridPacking.Box[] array2 = list.Select((Item other) => Box(other, other.CurrentAddress.Cast<GridItemAddress>())).ToArray();
		GridPacking.Box[] array3 = ExchangePacking.A(sourceGrid.Grid.GridWidth, sourceGrid.Grid.GridHeight, array, Box(item, sourceGrid), box, sourceGrid.Grid.Identity() == grid.Identity(), array2);
		if (array3 == null)
		{
			return null;
		}
		List<Step> list2 = new List<Step>
		{
			new Step(item, (ItemAddress)(object)val2, AddressKey(item))
		};
		for (int num = 0; num < list.Count; num++)
		{
			Item val7 = list[num];
			LocationInGrid locationInGrid = val7.CurrentAddress.Cast<GridItemAddress>().LocationInGrid;
			GridPacking.Box box2 = array3[num];
			ProtectedGridItemAddress to = new ProtectedGridItemAddress(sourceGrid.Grid, new LocationInGrid(box2.X, box2.Y, locationInGrid.r));
			list2.Add(new Step(val7, (ItemAddress)(object)to, AddressKey(val7)));
		}
		if (list2.Count > 2 && !GroupOwnerAllowed(InventoryAccess.Controller(view), list2.Select((Step s) => s.Item), list2.Select((Step s) => s.To)))
		{
			return null;
		}
		return new Plan(list2.ToArray());
	}

	private static Plan? BuildSlot(SlotView view, DragItemContext drag)
	{
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Expected O, but got Unknown
		//IL_01a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b9: Unknown result type (might be due to invalid IL or missing references)
		Slot slot = view.Slot;
		Item val = ((slot != null) ? slot.ContainedItem : null);
		Item item = ((ItemContext)drag).Item;
		if (!Active || !_swap.Value || slot == null || slot.ParentItem.TryCast<InventoryEquipment>() == null || slot.Locked || val == null || (Object)(object)_source == (Object)null || !_source.IsSearched || !_source.IsInteractable || ((BaseBindable<Error>)(object)_source.RemoveError)?.Value != null || !Moves.Eligible(item) || !Moves.Eligible(val) || !Single(item))
		{
			return null;
		}
		ItemView val2 = InventoryAccess.ItemView(view);
		if ((Object)(object)val2 == (Object)null || !val2.IsSearched || !val2.IsInteractable || ((BaseBindable<Error>)(object)val2.RemoveError)?.Value != null)
		{
			return null;
		}
		bool num = val2.CanInteract(drag);
		CompoundItem? obj = val.TryCast<CompoundItem>();
		if (!MovementPolicy.A(num, obj != null && obj.Grids?.Length > 0, item.TemplateId == val.TemplateId && (item.StackMaxSize > 1 || val.StackMaxSize > 1), Related(item, val), false, false))
		{
			return null;
		}
		ItemAddress currentAddress = item.CurrentAddress;
		if (currentAddress == null)
		{
			return null;
		}
		ProtectedSlotItemAddress val3 = new ProtectedSlotItemAddress(slot);
		ItemRotation valueOrDefault = (val.CurrentAddress?.TryCast<GridItemAddress>()?.LocationInGrid.r).GetValueOrDefault();
		ItemAddress val4 = Destination(val, currentAddress, valueOrDefault);
		if (val4 == null || !AddressAccepts(item, (ItemAddress)(object)val3, item, val) || !AddressAccepts(val, val4, item, val))
		{
			return null;
		}
		return MakePlan(item, val, (ItemAddress)(object)val3, val4);
	}

	internal static void Preview(DraggedItemView dragged)
	{
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		if (_previewFailed || !Active || !_swap.Value || dragged.ItemContext == null || !Single(((ItemContext)dragged.ItemContext).Item))
		{
			return;
		}
		GridView val = InventoryAccess.Container(dragged)?.TryCast<GridView>();
		if ((Object)(object)val == (Object)null || Time.unscaledTime < _previewAt)
		{
			return;
		}
		_previewAt = Time.unscaledTime + 0.06f;
		DragItemContext itemContext = dragged.ItemContext;
		ItemContext val2 = InventoryAccess.Target(dragged);
		LocationInGrid val3 = val.CalculateItemLocation(itemContext);
		object[] obj = new object[7]
		{
			val.Identity(),
			((ItemContext)itemContext).Item.Id,
			itemContext.ItemRotation,
			val3?.x,
			val3?.y,
			null,
			null
		};
		object obj2;
		if (val2 == null)
		{
			obj2 = null;
		}
		else
		{
			Item item = val2.Item;
			obj2 = ((item != null) ? item.Id : null);
		}
		obj[5] = obj2;
		obj[6] = _swap.Value;
		string text = string.Format("{0}:{1}:{2}:{3}:{4}:{5}:{6}", obj);
		if (_previewKey != text)
		{
			_preview = Build(val, itemContext, val2);
			_previewKey = text;
		}
		if (!(_preview == null))
		{
			GridItemAddress val4 = _preview.To.TryCast<GridItemAddress>();
			if (val4 != null)
			{
				Highlight(val, ((ItemContext)itemContext).Item, val4, InventoryAccess.GridSwapColor);
			}
		}
	}

	private static void Highlight(GridView view, Item item, GridItemAddress address, Color color)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		Image highlightPanel = view._highlightPanel;
		if (!((Object)(object)highlightPanel == (Object)null))
		{
			IntVec2 val = item.CalculateRotatedSize(address.LocationInGrid.r);
			((Graphic)highlightPanel).rectTransform.anchoredPosition = new Vector2((float)address.LocationInGrid.x * 63f, (float)(-address.LocationInGrid.y) * 63f);
			((Graphic)highlightPanel).rectTransform.sizeDelta = new Vector2((float)val.X * 63f, (float)val.Y * 63f);
			((Graphic)highlightPanel).color = color;
			((Component)highlightPanel).gameObject.SetActive(true);
		}
	}

	internal static bool TryDrop(ItemView source, PointerEventData eventData)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		if (!Active || (int)eventData.button != 0 || _source?.Identity() != source.Identity())
		{
			return false;
		}
		DraggedItemView dragged = source.DraggedItemView;
		DraggedItemView obj = dragged;
		DragItemContext drag = ((obj != null) ? obj.ItemContext : null);
		GridView view = InventoryAccess.Container(dragged)?.TryCast<GridView>();
		SlotView slotView = InventoryAccess.Container(dragged)?.TryCast<SlotView>();
		if (((Object)(object)view == (Object)null && (Object)(object)slotView == (Object)null) || drag == null || !Single(((ItemContext)drag).Item))
		{
			return false;
		}
		ItemContext target = InventoryAccess.Target(dragged);
		Plan plan = (((Object)(object)view != (Object)null) ? Build(view, drag, target) : BuildSlot(slotView, drag));
		if (plan == null)
		{
			return false;
		}
		ItemController controller = (((Object)(object)view != (Object)null) ? InventoryAccess.Controller(view) : InventoryAccess.Controller(slotView));
		GameObject ghost = ((Component)dragged).gameObject;
		if (!Batch.Start("交换位置", new(string, Action<Action<bool, string>>)[1] { ("提交移动增强", delegate(Action<bool, string> done)
		{
			Execute(plan, controller, done);
		}) }))
		{
			return false;
		}
		DragVisualCleanup.Run(delegate(Exception ex)
		{
			Plugin.Logger.LogWarning((object)("交换拖动清理异常，继续其余清理：" + ex));
		}, delegate
		{
			ghost.SetActive(false);
		}, delegate
		{
			((ItemContext)drag).DragCancelled();
		}, delegate
		{
			GridView obj2 = view;
			if (obj2 != null)
			{
				obj2.DisableHighlight();
			}
		}, delegate
		{
			SlotView obj2 = slotView;
			if (obj2 != null)
			{
				obj2.DisableHighlight();
			}
		}, delegate
		{
			dragged.Kill();
		}, delegate
		{
			Object.Destroy((Object)(object)ghost);
		}, delegate
		{
			source.DraggedItemView = null;
		}, delegate
		{
			((BaseBindable<bool>)(object)source.IsBeingDragged).Value = false;
		}, DragPreview.Clear, End, delegate
		{
			Plugin.Logger.LogInfo((object)$"移动增强已接管：交换位置，{plan.First.Id} → {plan.To}，覆盖物品={plan.Steps.Length - 1}");
		});
		return true;
	}

	private static void Execute(Plan plan, ItemController controller, Action<bool, string> done)
	{
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		if (plan.Steps.Any((Step s) => !Moves.Eligible(s.Item)))
		{
			done(arg1: false, "物品状态变化，已取消操作。");
			return;
		}
		if (plan.Steps.Any((Step s) => AddressKey(s.Item) != s.From))
		{
			done(arg1: false, "物品位置已变化，已取消操作。");
			return;
		}
		Item[] released = plan.Steps.Select((Step s) => s.Item).ToArray();
		if (plan.Steps.Any((Step s) => !AddressAccepts(s.Item, s.To, released)) || DestinationsOverlap(plan))
		{
			done(arg1: false, "目标空间或限制已变化，已取消操作。");
			return;
		}
		if (plan.Steps.Length > 2)
		{
			ExecuteGroup(plan, controller, done);
			return;
		}
		Step second = plan.Steps[1];
		OperationResult<SwapResult> val = ItemManipulator.Swap(plan.First, plan.To, second.Item, second.To, controller, false);
		if (val.Failed)
		{
			Plugin.Logger.LogWarning((object)("交换提交被游戏拒绝：" + (object)val.Error));
			done(arg1: false, "双方位置不满足交换条件，已保留原位。");
			return;
		}
		Moves.Commit(plan.First, val.Value.Cast<IOperationResult>(), controller, delegate(bool ok, string error)
		{
			if (ok)
			{
				Selection.Items.Remove(second.Item.Id);
			}
			done(ok, error);
		}, playSound: true);
	}

	private static bool DestinationsOverlap(Plan plan)
	{
		for (int i = 0; i < plan.Steps.Length; i++)
		{
			for (int j = 0; j < i; j++)
			{
				Step step = plan.Steps[i];
				Step step2 = plan.Steps[j];
				GridItemAddress val = step.To.TryCast<GridItemAddress>();
				GridItemAddress val2 = step2.To.TryCast<GridItemAddress>();
				if (val != null && val2 != null && val.Grid.Identity() == val2.Grid.Identity() && GridPacking.A(Box(step.Item, val), Box(step2.Item, val2)))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void ExecuteGroup(Plan plan, ItemController controller, Action<bool, string> done)
	{
		//IL_00fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02eb: Expected O, but got Unknown
		//IL_02f2: Expected O, but got Unknown
		//IL_029c: Unknown result type (might be due to invalid IL or missing references)
		BackEndInventoryController val = controller.TryCast<BackEndInventoryController>();
		if (val == null || !GroupOwnerAllowed(controller, plan.Steps.Select((Step s) => s.Item), plan.Steps.Select((Step s) => s.To)))
		{
			done(arg1: false, "多件交换仅支持局外自己的库存。");
			return;
		}
		CompoundItem parentItem = plan.First.CurrentAddress.Cast<GridItemAddress>().Grid.ParentItem;
		if (!((InventoryController)val).IsAllowedToSort(parentItem))
		{
			done(arg1: false, "当前界面不能提交多件位置交换。");
			return;
		}
		Step[] steps = plan.Steps;
		foreach (Step step in steps)
		{
			Option val2 = controller.CheckItemAction(step.Item, step.To);
			if (((Option)(ref val2)).Failed || ItemManipulator.MovePathCheck(step.Item, step.To, controller).Failed || ItemManipulator.DestinationCheck(step.Item.CurrentAddress, step.To, controller).Failed)
			{
				done(arg1: false, "物品存在移动限制，全部保留原位。");
				return;
			}
		}
		List<ContainerRemoveResult> list = new List<ContainerRemoveResult>();
		List<GridAddResult> list2 = new List<GridAddResult>();
		using InventoryPreparation inventoryPreparation = new InventoryPreparation();
		steps = plan.Steps;
		foreach (Step step2 in steps)
		{
			OperationResult<ContainerRemoveResult> val3 = step2.Item.CurrentAddress.Cast<GridItemAddress>().Grid.Remove(step2.Item, false);
			if (val3.Failed)
			{
				done(arg1: false, "物品无法取出，已取消整组交换。");
				return;
			}
			ContainerRemoveResult value = val3.Value;
			inventoryPreparation.Record(delegate
			{
				((ItemInContainerOperationResult)value).RollBack();
			});
			list.Add(value);
		}
		steps = plan.Steps;
		foreach (Step step3 in steps)
		{
			GridItemAddress val4 = step3.To.Cast<GridItemAddress>();
			OperationResult<GridAddResult> val5 = val4.Grid.Add(step3.Item, val4.LocationInGrid, false);
			if (val5.Failed)
			{
				done(arg1: false, "目标无法放入物品，已取消整组交换。");
				return;
			}
			GridAddResult value2 = val5.Value;
			inventoryPreparation.Record(delegate
			{
				((ItemInContainerOperationResult)value2).RollBack();
			});
			list2.Add(value2);
		}
		ApplySortItemsPositionResult val6 = new ApplySortItemsPositionResult(parentItem, (IEnumerable<ContainerRemoveResult>)list, (IEnumerable<GridAddResult>)list2, (InventoryController)(object)val);
		IOperationResult value3 = ((object)val6).Cast<IOperationResult>();
		((object)val6).Cast<IRollable>();
		inventoryPreparation.Handoff();
		Moves.Commit(plan.First, value3, controller, delegate(bool ok, string error)
		{
			if (ok)
			{
				Step[] steps2 = plan.Steps;
				foreach (Step step4 in steps2)
				{
					Selection.Items.Remove(step4.Item.Id);
				}
			}
			done(ok, error);
		}, playSound: true);
	}

	internal static bool TryEquip(Item item, ItemController controller, Action<bool, string> done)
	{
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		if (!EquipEnabled || TraderSelection.Active || !Moves.Eligible(item))
		{
			return false;
		}
		InventoryController? obj = controller.TryCast<InventoryController>();
		InventoryEquipment val = ((obj == null) ? null : obj.Inventory?.Equipment);
		if (val == null)
		{
			return false;
		}
		SlotItemAddress? obj2 = item.CurrentAddress?.TryCast<SlotItemAddress>();
		object obj3;
		if (obj2 == null)
		{
			obj3 = null;
		}
		else
		{
			Item parentItem = obj2.Slot.ParentItem;
			obj3 = ((parentItem != null) ? parentItem.Id : null);
		}
		if ((string?)obj3 == ((Item)val).Id)
		{
			return false;
		}
		foreach (Slot item2 in ((CompoundItem)val).Slots.OrderBy((Slot s) => (!(s.ID == "FirstPrimaryWeapon")) ? ((s.ID == "SecondPrimaryWeapon") ? 1 : 2) : 0))
		{
			if (item2.ContainedItem == null && !item2.Locked && item2.CheckCompatibility(item) && !ItemManipulator.Move(item, item2.CreateItemAddress(), controller, true).Failed)
			{
				OperationResult<MoveResult> val2 = ItemManipulator.Move(item, item2.CreateItemAddress(), controller, false);
				if (!val2.Failed)
				{
					Moves.Commit(item, val2.Value.Cast<IOperationResult>(), controller, done, playSound: true);
					return true;
				}
			}
		}
		return false;
	}
}
