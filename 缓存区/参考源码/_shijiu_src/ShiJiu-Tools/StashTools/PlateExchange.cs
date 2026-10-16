using System;
using System.Collections.Generic;
using System.Diagnostics;
using Diz.Binding;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StashTools;

internal static class PlateExchange
{
	internal sealed record Plan(Item Incoming, Item Outgoing, ItemAddress From, Slot Target, InventoryController Controller);

	private static bool _pending;

	private static string _previewKey = "";

	private static long _previewUntil;

	private static Plan? _preview;

	private static readonly HashSet<string> Logged = new HashSet<string>();

	internal static void Trace(string key, string message)
	{
		if (Logged.Count < 64 && Logged.Add(key))
		{
			Plugin.Logger.LogInfo((object)("插板换装：" + message));
		}
	}

	internal static Plan? Build(SlotView view, DragItemContext drag)
	{
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Expected O, but got Unknown
		if (!EquippedModding.Active || _pending || Batch.Busy || ChineseSettings.IsOpen)
		{
			return null;
		}
		Slot slot = view.Slot;
		Item item = ((ItemContext)drag).Item;
		Item val = ((slot != null) ? slot.ContainedItem : null);
		InventoryController val2 = InventoryAccess.Controller(view)?.TryCast<InventoryController>();
		if (slot == null || val2 == null || val == null || val.Identity() == item.Identity() || item.TryCast<ArmorPlate>() == null || val.TryCast<ArmorPlate>() == null || (Selection.Contains(item) && Selection.Count > 1))
		{
			return null;
		}
		if (!EquippedModding.PlateMove(item, slot, val2) || !EquippedModding.UnlockedAncestry(val) || !EquippedModding.Owned(val, val2))
		{
			Trace($"policy:{item.Id}:{slot.Identity()}", $"互换权限不满足，来源={item.CurrentAddress}，目标槽={slot.ID}，目标锁={slot.Locked}，目标固定={ArmorValuation.FixedSlot((IContainer)(object)slot)}，自身物品={EquippedModding.Owned(item, val2)}");
			return null;
		}
		ItemAddress currentAddress = item.CurrentAddress;
		if (currentAddress == null || (currentAddress.TryCast<SlotItemAddress>() == null && currentAddress.TryCast<GridItemAddress>() == null))
		{
			return null;
		}
		if (!slot.CheckCompatibility(item))
		{
			Trace($"compat:{item.Id}:{slot.Identity()}", "插板不兼容目标槽 " + slot.ID);
			return null;
		}
		Slot val3 = currentAddress.TryCast<SlotItemAddress>()?.Slot;
		if (val3 != null && !val3.CheckCompatibility(val))
		{
			return null;
		}
		if (!MovementEnhancements.AddressAccepts(val, currentAddress, item, val))
		{
			Trace($"space:{item.Id}:{slot.Identity()}", "旧板无法放回新板的原位置");
			return null;
		}
		ItemView val4 = InventoryAccess.ItemView(view);
		if ((Object)(object)val4 == (Object)null || !val4.IsSearched || !val4.IsInteractable)
		{
			return null;
		}
		ProtectedSlotItemAddress val5 = new ProtectedSlotItemAddress(slot);
		Error val6 = default(Error);
		if (!ItemManipulator.CanModifyItem(item, (ItemAddress)(object)val5, (ItemController)(object)val2, ref val6))
		{
			Trace($"access:{item.Id}:{slot.Identity()}", "新板访问检查拒绝：" + (object)val6);
			return null;
		}
		Error val7 = default(Error);
		if (!ItemManipulator.CanModifyItem(val, currentAddress, (ItemController)(object)val2, ref val7))
		{
			Trace($"access:{val.Id}:{slot.Identity()}", "旧板访问检查拒绝：" + (object)val7);
			return null;
		}
		return new Plan(item, val, currentAddress, slot, val2);
	}

	internal static bool Preview(SlotView view, DragItemContext drag)
	{
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		if (!EquippedModding.Active || _pending || Batch.Busy || ChineseSettings.IsOpen || ((ItemContext)drag).Item.TryCast<ArmorPlate>() == null)
		{
			return false;
		}
		object[] obj = new object[4]
		{
			((ItemContext)drag).Item.Identity(),
			view.Slot?.Identity(),
			null,
			null
		};
		Slot slot = view.Slot;
		obj[2] = ((slot == null) ? ((IntPtr?)null) : slot.ContainedItem?.Identity());
		obj[3] = ((ItemContext)drag).Item.CurrentAddress;
		string text = string.Format("{0}:{1}:{2}:{3}", obj);
		long timestamp = Stopwatch.GetTimestamp();
		if (text != _previewKey || timestamp >= _previewUntil)
		{
			_preview = Build(view, drag);
			_previewKey = text;
			_previewUntil = timestamp + Stopwatch.Frequency * 6 / 100;
		}
		if (_preview == null)
		{
			return false;
		}
		view.DisableHighlightSilent();
		InventoryAccess.HighlightSlot(view);
		Image fullBorder = view.FullBorder;
		if ((Object)(object)fullBorder != (Object)null)
		{
			((Graphic)fullBorder).color = InventoryAccess.SlotSwapColor;
			((Component)fullBorder).gameObject.SetActive(true);
		}
		((ItemContext)drag).InventoryError = null;
		return true;
	}

	internal static bool Drop(ItemView source, PointerEventData eventData)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if ((int)eventData.button != 0 || !EquippedModding.Active)
		{
			return false;
		}
		DraggedItemView dragged = source.DraggedItemView;
		DraggedItemView obj = dragged;
		DragItemContext drag = ((obj != null) ? obj.ItemContext : null);
		SlotView view = InventoryAccess.Container(dragged)?.TryCast<SlotView>();
		if (drag == null || (Object)(object)view == (Object)null || !source.IsSearched || !source.IsInteractable)
		{
			return false;
		}
		Plan plan = Build(view, drag);
		if (plan == null)
		{
			return false;
		}
		GameObject ghost = ((Component)dragged).gameObject;
		_pending = true;
		DragVisualCleanup.Run(delegate(Exception ex)
		{
			Plugin.Logger.LogWarning((object)("插板换装拖动清理异常：" + ex));
		}, delegate
		{
			ghost.SetActive(false);
		}, delegate
		{
			((ItemContext)drag).DragCancelled();
		}, delegate
		{
			view.DisableHighlight();
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
		}, DragPreview.Clear, MovementEnhancements.End);
		StashBehaviour.Post(delegate
		{
			Submit(plan);
		});
		return true;
	}

	private static void Submit(Plan plan)
	{
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Expected O, but got Unknown
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0188: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!EquippedModding.PlateMove(plan.Incoming, plan.Target, plan.Controller) || !EquippedModding.Owned(plan.Outgoing, plan.Controller) || !EquippedModding.UnlockedAncestry(plan.Outgoing) || !SameAddress(plan.Incoming.CurrentAddress, plan.From) || plan.Target.ContainedItem?.Identity() != plan.Outgoing.Identity())
			{
				_pending = false;
				StashBehaviour.Notify("插板状态已变化，已取消互换。");
				return;
			}
			OperationResult<SwapResult> val = ItemManipulator.Swap(plan.Incoming, (ItemAddress)new ProtectedSlotItemAddress(plan.Target), plan.Outgoing, plan.From, (ItemController)(object)plan.Controller, false);
			if (val.Failed)
			{
				_pending = false;
				Plugin.Logger.LogWarning((object)("插板交换被游戏拒绝：" + (object)val.Error));
				StashBehaviour.Notify("插板不兼容或原位置无法容纳旧板，已保留原位。");
				return;
			}
			Moves.Commit(plan.Incoming, val.Value.Cast<IOperationResult>(), (ItemController)(object)plan.Controller, delegate(bool ok, string error)
			{
				_pending = false;
				if (!ok)
				{
					StashBehaviour.Notify(error);
				}
				else
				{
					Selection.Items.Remove(plan.Outgoing.Id);
					Plugin.Logger.LogInfo((object)("插板互换成功：" + plan.Incoming.Id + " ↔ " + plan.Outgoing.Id + "，目标槽=" + plan.Target.ID));
				}
			}, playSound: true);
		}
		catch (Exception ex)
		{
			_pending = false;
			StashBehaviour.Error("插板互换未完成", ex);
		}
	}

	private static bool SameAddress(ItemAddress? current, ItemAddress expected)
	{
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		SlotItemAddress val = current?.TryCast<SlotItemAddress>();
		SlotItemAddress val2 = expected.TryCast<SlotItemAddress>();
		if (val != null || val2 != null)
		{
			if (val != null && val2 != null)
			{
				return val.Slot.Identity() == val2.Slot.Identity();
			}
			return false;
		}
		GridItemAddress val3 = current?.TryCast<GridItemAddress>();
		GridItemAddress val4 = expected.TryCast<GridItemAddress>();
		if (val3 == null || val4 == null || val3.Grid.Identity() != val4.Grid.Identity())
		{
			return false;
		}
		LocationInGrid locationInGrid = val3.LocationInGrid;
		LocationInGrid locationInGrid2 = val4.LocationInGrid;
		if (locationInGrid.x == locationInGrid2.x && locationInGrid.y == locationInGrid2.y)
		{
			return locationInGrid.r == locationInGrid2.r;
		}
		return false;
	}
}
