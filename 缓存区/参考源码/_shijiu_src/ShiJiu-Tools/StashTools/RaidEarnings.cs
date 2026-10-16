using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Vehicle;
using UnityEngine;

namespace StashTools;

internal static class RaidEarnings
{
	private sealed class TrackedItem
	{
		internal readonly Item Item;

		internal readonly bool Stackable;

		private readonly Func<double> _ratio;

		private readonly Ammo? _ammo;

		internal TrackedItem(Item item)
		{
			Item = item;
			Stackable = item.StackMaxSize > 1;
			_ammo = item.TryCast<Ammo>();
			_ratio = () => 1.0;
			if (Stackable)
			{
				return;
			}
			MedKitComponent med = item.GetItemComponent<MedKitComponent>();
			FoodDrinkComponent food = item.GetItemComponent<FoodDrinkComponent>();
			ResourceComponent resource = item.GetItemComponent<ResourceComponent>();
			KeyComponent key = item.GetItemComponent<KeyComponent>();
			RepairKitComponent repair = item.GetItemComponent<RepairKitComponent>();
			if (med != null && med.MaxHpResource > 0)
			{
				_ratio = () => med.HpResource / (float)med.MaxHpResource;
			}
			else if (food != null && food.MaxResource > 0f)
			{
				_ratio = () => food.HpPercent / food.MaxResource;
			}
			else if (resource != null && resource.MaxResource > 0f)
			{
				_ratio = () => resource.Value / resource.MaxResource;
			}
			else if (key != null && key.Template.MaximumNumberOfUsage > 0)
			{
				_ratio = () => 1.0 - (double)key.NumberOfUsages / (double)key.Template.MaximumNumberOfUsage;
			}
			else if (repair != null && repair._template.MaxRepairResource > 0)
			{
				_ratio = () => repair.Resource / (float)repair._template.MaxRepairResource;
			}
		}

		internal double Units()
		{
			Ammo? ammo = _ammo;
			if (ammo != null && ammo.IsUsed)
			{
				return 0.0;
			}
			double num = _ratio();
			if (!double.IsFinite(num))
			{
				throw new InvalidOperationException("物品资源数据无效：" + Item.Id);
			}
			return (double)Math.Max(0, Item.StackObjectsCount) * Math.Clamp(num, 0.0, 1.0);
		}
	}

	internal static bool Ready;

	internal static readonly RaidLedger Ledger = new RaidLedger();

	private static readonly RaidInventoryObserver Observer = new RaidInventoryObserver(Ledger);

	internal static RaidLedger.Totals Totals = new RaidLedger.Totals(0.0, 0.0, 0, 0);

	private static Player? _player;

	private static GameWorld? _world;

	private static InventoryController? _controller;

	private static Action<AddItemEventArgs>? _added;

	private static Action<RemoveItemEventArgs>? _removed;

	private static Action<RefreshItemEventArgs>? _refreshed;

	private static float _next;

	private static float _fallbackAt;

	private static float _timingAt;

	private static bool _failed;

	private static bool _dirty;

	private static bool _finished;

	private static bool _awaitTransit;

	private static string _profileId = "";

	private static readonly Dictionary<string, RaidInventoryObserver.Entry> Shipped = new Dictionary<string, RaidInventoryObserver.Entry>();

	internal static bool HasSummary;

	internal static ExitStatus LastStatus;

	private static readonly Dictionary<string, TrackedItem> Tracked = new Dictionary<string, TrackedItem>();

	internal static bool AwaitTransit => _awaitTransit;

	internal static string ProfileId => _profileId;

	internal static bool Active
	{
		get
		{
			if (Ready && (Object)(object)_player != (Object)null && !_failed)
			{
				return !_finished;
			}
			return false;
		}
	}

	internal static void Begin(Player? player, GameWorld? world)
	{
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Invalid comparison between Unknown and I4
		try
		{
			if (Ready && !((Object)(object)player == (Object)null) && player.IsYourPlayer && !((Object)(object)world == (Object)null) && !(_player?.Identity() == player.Identity()) && !((object)world).GetType().FullName.Contains("Hideout", StringComparison.OrdinalIgnoreCase) && !string.Equals(player.Location, "hideout", StringComparison.OrdinalIgnoreCase))
			{
				LocalGame val = ((!Singleton<AbstractGame>.Instantiated) ? null : Singleton<AbstractGame>.Instance?.TryCast<LocalGame>());
				LocalRaidSettings val2 = (((Object)(object)val == (Object)null) ? null : UiValueAccess.Raid(val));
				bool flag = HistoryCoopBridge.ContinuingTransit ?? (val2 != null && ((int)val2.transitionType != 0 || (val2.transition != null && (int)val2.transition.transitionType > 0)));
				bool resume = (_awaitTransit && _profileId == player.ProfileId) & flag;
				HistoryCapture.Guard(delegate
				{
					HistoryCapture.Persist();
				});
				if (!resume)
				{
					Reset();
					Shipped.Clear();
					RaidPrices.Reset();
					HasSummary = false;
					RaidSummary.Hide();
				}
				else
				{
					Detach();
				}
				_finished = false;
				_awaitTransit = false;
				_failed = false;
				_profileId = player.ProfileId;
				_player = player;
				_world = world;
				MarketQuotes.Resume();
				RaidPrices.Resume();
				RaidPrices.RememberSession();
				List<RaidInventoryObserver.Entry> list = Snapshot();
				if (resume)
				{
					Observer.Resume(list);
				}
				else
				{
					Observer.Begin(list);
				}
				HistoryCapture.Guard(delegate
				{
					HistoryCapture.Begin(player, resume);
				});
				_controller = player.InventoryController;
				_added = delegate
				{
					_dirty = true;
				};
				_removed = delegate
				{
					_dirty = true;
				};
				_refreshed = delegate
				{
					_dirty = true;
				};
				((ItemController)_controller).AddItemEvent += _added;
				((ItemController)_controller).RemoveItemEvent += _removed;
				((ItemController)_controller).RefreshItemEvent += _refreshed;
				_dirty = true;
				Plugin.Logger.LogInfo((object)$"对局收益基线：{player.Location}，{list.Count} 个物品记录，续记={resume}；使用只读库存观察，不安装拆分/合并/压弹底层钩子。");
			}
		}
		catch (Exception ex)
		{
			Fail(ex);
		}
	}

	private static void Detach()
	{
		if (_controller != null)
		{
			try
			{
				if (_added != null)
				{
					((ItemController)_controller).AddItemEvent -= _added;
				}
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogWarning((object)("收益新增物品监听清理失败：" + ex.Message));
			}
			try
			{
				if (_removed != null)
				{
					((ItemController)_controller).RemoveItemEvent -= _removed;
				}
			}
			catch (Exception ex2)
			{
				Plugin.Logger.LogWarning((object)("收益移除物品监听清理失败：" + ex2.Message));
			}
			try
			{
				if (_refreshed != null)
				{
					((ItemController)_controller).RefreshItemEvent -= _refreshed;
				}
			}
			catch (Exception ex3)
			{
				Plugin.Logger.LogWarning((object)("收益刷新物品监听清理失败：" + ex3.Message));
			}
		}
		_controller = null;
		_added = null;
		_removed = null;
		_refreshed = null;
		_player = null;
		_world = null;
		Tracked.Clear();
		RaidPanel.Hide();
	}

	internal static void Reset()
	{
		Detach();
		_failed = false;
		_finished = false;
		_awaitTransit = false;
		Shipped.Clear();
		HasSummary = false;
		Totals = new RaidLedger.Totals(0.0, 0.0, 0, 0);
	}

	internal static void End(GameWorld world)
	{
		if (_world?.Identity() != world.Identity())
		{
			return;
		}
		StopPrices();
		if (!_finished)
		{
			Totals = Ledger.A(RaidPrices.Get);
			HistoryCapture.Guard(delegate
			{
				HistoryCapture.Interrupted(null, "游戏世界正在销毁", readInventory: false);
			});
			HasSummary = false;
			_awaitTransit = false;
			_finished = true;
		}
		Detach();
	}

	private static IEnumerable<RaidInventoryObserver.Entry> TransferSnapshot()
	{
		List<RaidInventoryObserver.Entry> result = new List<RaidInventoryObserver.Entry>();
		GameWorld? world = _world;
		object transfer;
		if (world == null)
		{
			transfer = null;
		}
		else
		{
			BtrController btrController = world.BtrController;
			transfer = ((btrController != null) ? btrController.TransferItemsController : null);
		}
		Read((TransferItemsController?)transfer);
		GameWorld? world2 = _world;
		object transfer2;
		if (world2 == null)
		{
			transfer2 = null;
		}
		else
		{
			TransitController transitController = world2.TransitController;
			transfer2 = ((transitController != null) ? transitController.TransferItemsController : null);
		}
		Read((TransferItemsController?)transfer2);
		return result;
		void Read(TransferItemsController? val)
		{
			Stash val2 = default(Stash);
			if (val != null && val.TryGetTransferContainer(_profileId, ref val2) && val2 != null)
			{
				ItemValuation.Walk((Item)(object)val2, delegate(Item item)
				{
					RaidPrices.Register(item);
					if (!Tracked.ContainsKey(item.Id))
					{
						Tracked.Add(item.Id, new TrackedItem(item));
					}
					result.Add(new RaidInventoryObserver.Entry(new RaidLedger.Sample(item.Id, PriceBook.Key(item), ItemValuation.Units(item)), item.StackMaxSize > 1));
				}, includeRoot: false);
			}
		}
	}

	internal static void Finish(string profileId, ExitStatus status)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Invalid comparison between Unknown and I4
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Invalid comparison between Unknown and I4
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Invalid comparison between Unknown and I4
		//IL_0256: Unknown result type (might be due to invalid IL or missing references)
		StopPrices();
		if (!Active || profileId != _profileId)
		{
			return;
		}
		foreach (RaidInventoryObserver.Entry item in TransferSnapshot())
		{
			Shipped[item.Item.Id] = item;
		}
		List<RaidInventoryObserver.Entry> list = Snapshot();
		Observer.Observe(list, (string id) => (!Tracked.TryGetValue(id, out TrackedItem value)) ? 0.0 : value.Units());
		if ((int)status != 0 && (int)status != 3 && (int)status != 5)
		{
			HashSet<string> retained = new HashSet<string>();
			Slot[] slots = ((CompoundItem)_player.Inventory.Equipment).Slots;
			foreach (Slot val in slots)
			{
				if ((!(val.ID != "SecuredContainer") || !(val.ID != "Scabbard") || val.ID.StartsWith("SpecialSlot", StringComparison.Ordinal)) && val.ContainedItem != null)
				{
					ItemValuation.Walk(val.ContainedItem, delegate(Item item)
					{
						retained.Add(item.Id);
					});
				}
			}
			Ledger.a(from e in list
				where retained.Contains(e.Item.Id) || Shipped.ContainsKey(e.Item.Id)
				select e.Item);
		}
		LastStatus = status;
		_awaitTransit = (int)status == 5;
		_finished = true;
		HistoryCapture.Guard(delegate
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			HistoryCapture.Finish(_player, status);
			GameWorld? world = _world;
			object t;
			if (world == null)
			{
				t = null;
			}
			else
			{
				BtrController btrController = world.BtrController;
				t = ((btrController != null) ? btrController.TransferItemsController : null);
			}
			ReadTransfer((TransferItemsController?)t);
			GameWorld? world2 = _world;
			object t2;
			if (world2 == null)
			{
				t2 = null;
			}
			else
			{
				TransitController transitController = world2.TransitController;
				t2 = ((transitController != null) ? transitController.TransferItemsController : null);
			}
			ReadTransfer((TransferItemsController?)t2);
		});
		HasSummary = !_awaitTransit;
		Totals = Ledger.A(RaidPrices.Get);
		RaidPanel.Hide();
		HistoryCapture.Guard(delegate
		{
			HistoryCapture.Persist();
		});
		Plugin.Logger.LogInfo((object)$"对局结算捕获：{status}，寄送记录={Shipped.Count}，转图续记={_awaitTransit}");
		static void ReadTransfer(TransferItemsController? t)
		{
			Stash val2 = default(Stash);
			if (t != null && t.TryGetTransferContainer(_profileId, ref val2) && val2 != null)
			{
				HistoryCapture.Transfer((Item)(object)val2);
			}
		}
	}

	internal static void Reconcile(Profile profile)
	{
		if (!HasSummary || profile.Id != _profileId || profile.Inventory?.Equipment == null)
		{
			return;
		}
		Dictionary<string, RaidLedger.Sample> samples = new Dictionary<string, RaidLedger.Sample>();
		foreach (RaidInventoryObserver.Entry value in Shipped.Values)
		{
			samples[value.Item.Id] = value.Item;
		}
		Slot slot = profile.Inventory.Equipment.GetSlot((EquipmentSlot)13);
		object obj;
		if (slot == null)
		{
			obj = null;
		}
		else
		{
			Item containedItem = slot.ContainedItem;
			obj = ((containedItem != null) ? containedItem.Id : null);
		}
		string ownDogtag = (string)obj;
		ItemValuation.Walk((Item)(object)profile.Inventory.Equipment, delegate(Item item)
		{
			if (!(item.Id == ownDogtag))
			{
				RaidPrices.Register(item);
				samples[item.Id] = new RaidLedger.Sample(item.Id, PriceBook.Key(item), ItemValuation.Units(item));
			}
		}, includeRoot: false);
		Ledger.a(samples.Values);
		Totals = Ledger.A(RaidPrices.Get);
		HistoryCapture.Guard(delegate
		{
			HistoryCapture.Reconcile(profile);
		});
		Plugin.Logger.LogInfo((object)"收益结算已与游戏加载的结算后角色物品核对。");
	}

	internal static void Fail(Exception ex)
	{
		StopPrices();
		if (!_failed)
		{
			_failed = true;
			HasSummary = false;
			RaidPanel.Hide();
			HistoryCapture.Guard(delegate
			{
				HistoryCapture.Interrupted(null, "收益采集异常：" + ex.GetType().Name, readInventory: false);
			});
			Plugin.Logger.LogError((object)("对局收益模块已停止，原有功能不受影响：" + ex));
		}
	}

	internal static void StopPrices()
	{
		MarketQuotes.Stop();
		RaidPrices.Freeze();
		try
		{
			HoverPrices.Suspend();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("撤离悬浮提示清理失败：" + ex.Message));
		}
		try
		{
			LoadoutValue.SuspendPrices();
		}
		catch (Exception ex2)
		{
			Plugin.Logger.LogWarning((object)("撤离战备报价清理失败：" + ex2.Message));
		}
	}

	private static List<RaidInventoryObserver.Entry> Snapshot()
	{
		List<RaidInventoryObserver.Entry> result = new List<RaidInventoryObserver.Entry>();
		Slot slot = _player.Inventory.Equipment.GetSlot((EquipmentSlot)13);
		object obj;
		if (slot == null)
		{
			obj = null;
		}
		else
		{
			Item containedItem = slot.ContainedItem;
			obj = ((containedItem != null) ? containedItem.Id : null);
		}
		string ownDogtag = (string)obj;
		ItemValuation.Walk((Item)(object)_player.Inventory.Equipment, delegate(Item item)
		{
			if (!(item.Id == ownDogtag))
			{
				if (!Tracked.TryGetValue(item.Id, out TrackedItem value))
				{
					value = new TrackedItem(item);
					Tracked.Add(item.Id, value);
					RaidPrices.Register(item);
				}
				result.Add(new RaidInventoryObserver.Entry(new RaidLedger.Sample(item.Id, PriceBook.Key(item), value.Units()), value.Stackable));
			}
		}, includeRoot: false);
		Dictionary<string, RaidInventoryObserver.Entry> dictionary = result.ToDictionary((RaidInventoryObserver.Entry e) => e.Item.Id);
		foreach (RaidInventoryObserver.Entry value2 in Shipped.Values)
		{
			dictionary.TryAdd(value2.Item.Id, value2);
		}
		foreach (RaidInventoryObserver.Entry item in TransferSnapshot())
		{
			dictionary.TryAdd(item.Item.Id, item);
		}
		return dictionary.Values.ToList();
	}

	internal static void Tick()
	{
		if (!Ready)
		{
			return;
		}
		try
		{
			if (!Active)
			{
				if (HasSummary && Time.unscaledTime >= _next)
				{
					_next = Time.unscaledTime + 0.2f;
					Totals = Ledger.A(RaidPrices.Get);
				}
				RaidPanel.Hide();
				return;
			}
			if (Singleton<GameWorld>.Instantiated)
			{
				IntPtr? intPtr = Singleton<GameWorld>.Instance?.Identity();
				IntPtr? intPtr2 = _world?.Identity();
				if (intPtr.HasValue == intPtr2.HasValue && (!intPtr.HasValue || !(intPtr.GetValueOrDefault() != intPtr2.GetValueOrDefault())) && !((Object)(object)_player == (Object)null))
				{
					float unscaledTime = Time.unscaledTime;
					if (unscaledTime >= _next)
					{
						_next = unscaledTime + 0.2f;
						if (_dirty || unscaledTime >= _fallbackAt)
						{
							InventoryController? controller = _controller;
							if (controller == null || !((ItemController)controller).HasActiveEvents)
							{
								Stopwatch stopwatch = Stopwatch.StartNew();
								Observer.Observe(Snapshot(), (string id) => Tracked[id].Units());
								_dirty = false;
								_fallbackAt = unscaledTime + 1f;
								stopwatch.Stop();
								if (stopwatch.Elapsed.TotalMilliseconds > 8.0 && unscaledTime >= _timingAt)
								{
									_timingAt = unscaledTime + 10f;
									Plugin.Logger.LogWarning((object)$"对局收益库存读取耗时 {stopwatch.Elapsed.TotalMilliseconds:F1} ms，已记录用于排查卡顿。");
								}
							}
						}
						RaidPrices.Tick();
						Totals = Ledger.A(RaidPrices.Get);
					}
					RaidPanel.Tick(Totals);
					return;
				}
			}
			StopPrices();
			if (!_finished)
			{
				Totals = Ledger.A(RaidPrices.Get);
				HistoryCapture.Guard(delegate
				{
					HistoryCapture.Interrupted(null, "游戏世界已离开且未收到结束事件", readInventory: false);
				});
				HasSummary = false;
				_awaitTransit = false;
				_finished = true;
			}
			Detach();
		}
		catch (Exception ex)
		{
			Fail(ex);
		}
	}
}
