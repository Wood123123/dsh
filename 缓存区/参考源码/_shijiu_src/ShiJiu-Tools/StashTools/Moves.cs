using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;

namespace StashTools;

internal static class Moves
{
	internal static bool Eligible(Item i)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		if (Selection.Accessible(i) && (int)i.PinLockState == 0)
		{
			return !i.QuestItem;
		}
		return false;
	}

	internal static void QuickMove(Item[] items)
	{
		ItemUiContext ui = ItemUiContext.Instance;
		ItemController controller = InventoryAccess.Controller(ui);
		Batch.Start("快速移动", from i in items.Where(Eligible)
			select ((string, Action<Action<bool, string>>))("快速移动 " + i.ShortName, delegate(Action<bool, string> done)
			{
				//IL_003b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0041: Expected O, but got Unknown
				//IL_005b: Unknown result type (might be due to invalid IL or missing references)
				//IL_0060: Unknown result type (might be due to invalid IL or missing references)
				//IL_0089: Unknown result type (might be due to invalid IL or missing references)
				if (!Eligible(i))
				{
					done(arg1: false, "物品状态发生变化，已停止移动。");
				}
				else if (!MovementEnhancements.TryEquip(i, controller, done))
				{
					DefaultItemContext val = new DefaultItemContext(i, (EItemViewType)3);
					try
					{
						OperationResult val2 = ui.QuickFindAppropriatePlace((ItemContext)(object)val, controller, false, false, false);
						if (((OperationResult)(ref val2)).Failed)
						{
							Batch.Skip("空间不足或无合适位置");
							done(arg1: true, "");
						}
						else
						{
							Commit(i, val2.Value, controller, done, playSound: true);
						}
					}
					finally
					{
						((IDisposable)val)?.Dispose();
					}
				}
			}));
	}

	internal static void PackInto(Item[] items, Grid[] grids)
	{
		ItemController controller = InventoryAccess.Controller(ItemUiContext.Instance);
		Batch.Start("移动到容器", from i in items.Where(Eligible)
			select ((string, Action<Action<bool, string>>))("移动 " + i.ShortName, delegate(Action<bool, string> done)
			{
				//IL_004c: Unknown result type (might be due to invalid IL or missing references)
				//IL_0053: Expected O, but got Unknown
				//IL_0067: Unknown result type (might be due to invalid IL or missing references)
				//IL_006c: Unknown result type (might be due to invalid IL or missing references)
				Grid[] array = grids;
				foreach (Grid val in array)
				{
					if (!(((Item)val.ParentItem).Id == i.Id))
					{
						LocationInGrid val2 = val.FindFreeSpace(i);
						if (!(val2 == (LocationInGrid)null))
						{
							ProtectedGridItemAddress val3 = new ProtectedGridItemAddress(val, val2);
							if (!ItemManipulator.Move(i, (ItemAddress)(object)val3, controller, true).Failed)
							{
								Submit(i, (GridItemAddress)(object)val3, controller, done);
								return;
							}
						}
					}
				}
				Batch.Skip("目标空间不足或不接收");
				done(arg1: true, "");
			}));
	}

	internal static void Drop(Item[] items, DragItemContext lead, GridView view)
	{
		GroupMovePlan groupMovePlan = DragPreview.Ensure(view, lead, force: true);
		if (groupMovePlan.Error != null)
		{
			StashBehaviour.Notify(groupMovePlan.Error);
			return;
		}
		if (!DragPreview.SameAsShown(groupMovePlan))
		{
			StashBehaviour.Notify("库存或目标位置已变化，请重新拖动以确认预览。");
			return;
		}
		if (!groupMovePlan.CanMove)
		{
			if (groupMovePlan.Skipped > 0)
			{
				StashBehaviour.Notify($"没有可放置位置，{groupMovePlan.Skipped} 件保留原处。");
			}
			return;
		}
		IEnumerable<(string, Action<Action<bool, string>>)> jobs = from p in groupMovePlan.Steps
			where !p.Unchanged
			select ((string, Action<Action<bool, string>>))("移动 " + p.Item.ShortName, delegate(Action<bool, string> done)
			{
				Submit(p.Item, p.Address, InventoryAccess.Controller(view), done, skipUnavailable: true);
			});
		if (Batch.Start("整组移动", jobs) && groupMovePlan.Skipped > 0)
		{
			Batch.Skip("目标空间不足或不接收", groupMovePlan.Skipped);
		}
	}

	private static void Submit(Item i, GridItemAddress address, ItemController controller, Action<bool, string> done, bool skipUnavailable = false)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		if (!Eligible(i))
		{
			done(arg1: false, "物品状态已改变，停止移动。");
			return;
		}
		OperationResult<MoveResult> val = ItemManipulator.Move(i, (ItemAddress)(object)address, controller, false);
		if (val.Failed)
		{
			if (skipUnavailable)
			{
				Batch.Skip("目标位置已不可用");
				done(arg1: true, "");
			}
			else
			{
				Fail(done, "目标位置无法放入此物品。", val.Error);
			}
		}
		else
		{
			IOperationResult value = val.Value.Cast<IOperationResult>();
			Commit(i, value, controller, done);
		}
	}

	internal static void Commit(Item i, IOperationResult value, ItemController controller, Action<bool, string> done, bool playSound = false)
	{
		IRollable rollback = value.Cast<IRollable>();
		if (!PreparedTransaction.A(delegate
		{
			rollback.RollBack();
		}, () => controller.CanExecute(value), delegate
		{
			Plugin.Logger.LogInfo((object)$"Move submit (restored source): {i.Id}, from={i.CurrentAddress}");
			controller.RunNetworkTransaction(value, CallbackFor(i, done, playSound));
		}))
		{
			done(arg1: false, "当前物品不能移动。");
		}
	}

	private static void Fail(Action<bool, string> done, string message, object? details)
	{
		Plugin.Logger.LogWarning((object)(message + " Details: " + details));
		done(arg1: false, message);
	}

	private static Callback CallbackFor(Item i, Action<bool, string> done, bool playSound)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		return (Callback)delegate(IResult result)
		{
			bool failed = result == null || result.Failed;
			string details = ((result != null) ? result.Error : null) ?? "No result";
			StashBehaviour.Post(delegate
			{
				if (failed)
				{
					Fail(done, "移动未成功，已停止后续操作。", details);
				}
				else
				{
					Plugin.Logger.LogInfo((object)("Move committed: " + i.Id));
					Selection.Items.Remove(i.Id);
					Batch.Success("移动成功");
					if (playSound)
					{
						try
						{
							if (Singleton<GUISounds>.Instantiated)
							{
								Singleton<GUISounds>.Instance.PlayItemSound(i.ItemSound, (EInventorySoundType)0, false);
							}
						}
						catch (Exception ex)
						{
							Plugin.Logger.LogWarning((object)("物品已移动，播放声效失败：" + ex.Message));
						}
					}
					done(arg1: true, "");
				}
			});
		};
	}
}
