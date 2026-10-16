using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx.Configuration;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StashTools;

internal static class LoadoutValue
{
	private readonly record struct Node(Item Item, bool Gear);

	internal static bool Ready;

	private static ValuePanelOptions _options = null;

	private static ConfigEntry<bool> _knife = null;

	private static ConfigEntry<bool> _showEquipment = null;

	private static ConfigEntry<bool> _showSupplies = null;

	private static ConfigEntry<bool> _showQuoteStatus = null;

	private static ConfigEntry<float> _quoteMinutes = null;

	private static ConfigEntry<float> _inventorySeconds = null;

	private static readonly ValuePanel Panel = new ValuePanel();

	private static readonly PriceBook Prices = new PriceBook();

	private static readonly List<LoadoutPolicy.Entry> Entries = new List<LoadoutPolicy.Entry>();

	private static LoadoutPolicy.Totals _totals = new LoadoutPolicy.Totals(0.0, 0.0, 0, 0);

	private static float _next;

	private static float _quoteReset;

	private static float _calculateAt;

	private static float _warningAt;

	private static float _scanAt;

	private static string _title = "";

	private static string _detail = "";

	private static RaidAmountUnit? _unit;

	private static IntPtr _equipment;

	private static bool _custom;

	private static bool _lastKnife;

	private static bool _visible;

	private static RectTransform? _anchor;

	private static RectTransform? _parent;

	private static TMP_Text? _font;

	private static InventoryController? _controller;

	private static Action<AddItemEventArgs>? _added;

	private static Action<RemoveItemEventArgs>? _removed;

	private static Action<RefreshItemEventArgs>? _refreshed;

	private static bool _dirty = true;

	internal static bool IncludeKnife => _knife?.Value ?? false;

	internal static void Configure(ConfigFile config)
	{
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Expected O, but got Unknown
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Expected O, but got Unknown
		_options = new ValuePanelOptions(config, "06 仓库战备", "显示总战备", 0f, -80f, "相对装备滚动区域中胸挂一列的左上角；文字随装备区域滚动");
		_knife = config.Bind<bool>("06 仓库战备", "计算装备栏刀具价值", false, ChineseSettings.ToggleDescription("默认不计算刀鞘栏的近战武器。背包里作为物资携带的刀仍计入物资。枪械配件、护甲插板归装备；弹匣和弹药归物资。保险箱本体和口袋本体不估价，其内部物品照常计入。臂带和特殊栏物品归物资。自身佩戴狗牌不估价，拾取的狗牌按等级估价。"));
		_showEquipment = config.Bind<bool>("06 仓库战备", "显示装备价值", true, ChineseSettings.ToggleDescription("仅控制第二行装备金额的显示，不改变总战备计算。"));
		_showSupplies = config.Bind<bool>("06 仓库战备", "显示物资价值", true, ChineseSettings.ToggleDescription("仅控制第二行物资金额的显示；两项都关闭只显示总战备。"));
		_showQuoteStatus = config.Bind<bool>("06 仓库战备", "显示报价状态提示", false, ChineseSettings.ToggleDescription("默认隐藏估价中、无报价提示。缺失报价仍不计入总额，具体物品记录在日志中。"));
		_quoteMinutes = config.Bind<float>("06 仓库战备", "报价刷新间隔（分钟）", 60f, new ConfigDescription("默认60分钟。到期后台更新跳蚤与商人报价，更新期间保留旧价格。换装备和增减物资仍及时重算数量，不等待报价刷新。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 1440f), Array.Empty<object>()));
		_inventorySeconds = config.Bind<float>("06 仓库战备", "库存兜底检查间隔（秒）", 3f, new ConfigDescription("仅核对身上物品是否变化，不重新请求市场价格；物品变化事件仍会及时更新。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 3600f), Array.Empty<object>()));
		Ready = true;
	}

	internal static void Dispose()
	{
		SuspendPrices();
		Panel.Dispose();
	}

	internal static void SuspendPrices()
	{
		Prices.Suspend();
		Unsubscribe();
	}

	private static void Unsubscribe()
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
				Plugin.Logger.LogWarning((object)("战备新增物品监听清理失败：" + ex.Message));
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
				Plugin.Logger.LogWarning((object)("战备移除物品监听清理失败：" + ex2.Message));
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
				Plugin.Logger.LogWarning((object)("战备刷新物品监听清理失败：" + ex3.Message));
			}
		}
		_controller = null;
		_added = null;
		_removed = null;
		_refreshed = null;
	}

	private static IEnumerable<Node> Children(Node node)
	{
		ContainerCollection val = node.Item.TryCast<ContainerCollection>();
		if (val == null)
		{
			yield break;
		}
		IEnumerator<IContainer> containers = val.Containers.GetEnumerator();
		try
		{
			while (containers.MoveNext())
			{
				if (ArmorValuation.FixedSlot(containers.Current))
				{
					continue;
				}
				IContainer current = containers.Current;
				bool storage = current.TryCast<Grid>() != null;
				IEnumerator<Item> items = current.Items.GetEnumerator();
				try
				{
					while (items.MoveNext())
					{
						Item current2 = items.Current;
						yield return new Node(current2, LoadoutPolicy.A(node.Gear, storage, current2.TryCast<Ammo>() != null, current2.TryCast<Magazine>() != null));
					}
				}
				finally
				{
					items.Dispose();
				}
			}
		}
		finally
		{
			containers.Dispose();
		}
	}

	private static void Scan(InventoryEquipment equipment)
	{
		Entries.Clear();
		HashSet<string> seen = new HashSet<string>();
		Slot[] slots = ((CompoundItem)equipment).Slots;
		foreach (Slot val in slots)
		{
			Item containedItem = val.ContainedItem;
			if (containedItem == null)
			{
				continue;
			}
			InventoryTraversal.Walk(new Node(containedItem, LoadoutPolicy.A(val.ID)), (Node n) => n.Item.Id, Children, delegate(Node n)
			{
				if (seen.Add(n.Item.Id))
				{
					Prices.Register(n.Item);
					Entries.Add(new LoadoutPolicy.Entry(n.Item.Id, PriceBook.Key(n.Item), ItemValuation.Units(n.Item), n.Gear));
				}
			}, LoadoutPolicy.A(val.ID, _knife.Value));
		}
	}

	internal static void Tick()
	{
		//IL_05b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_05bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_05c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_05da: Unknown result type (might be due to invalid IL or missing references)
		//IL_05df: Unknown result type (might be due to invalid IL or missing references)
		//IL_05e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_05eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_05ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0602: Unknown result type (might be due to invalid IL or missing references)
		//IL_0607: Unknown result type (might be due to invalid IL or missing references)
		//IL_0611: Unknown result type (might be due to invalid IL or missing references)
		//IL_061d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0622: Unknown result type (might be due to invalid IL or missing references)
		//IL_064f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0657: Unknown result type (might be due to invalid IL or missing references)
		//IL_065c: Unknown result type (might be due to invalid IL or missing references)
		if (!Ready)
		{
			return;
		}
		try
		{
			InventoryScreen screenView = StashBehaviour.ScreenView;
			if (!_options.Enabled.Value || (Object)(object)screenView == (Object)null || !((Component)screenView).gameObject.activeInHierarchy || !UiValueAccess.GearActive(screenView) || InventoryAccess.Controller(screenView)?.TryCast<BackEndInventoryController>() == null || RaidEarnings.Active)
			{
				Panel.Hide();
				_visible = false;
				SuspendPrices();
				return;
			}
			if (MarketQuotes.Stopped)
			{
				Panel.Hide();
				_visible = false;
				SuspendPrices();
				return;
			}
			Prices.Resume();
			InventoryEquipment val = UiValueAccess.Equipment(screenView._itemsPanel?._equipmentPanel);
			ContainersPanel val2 = screenView._itemsPanel?._containers;
			RectTransform val3 = null;
			if (UiValueAccess.Slots(val2) != null && UiValueAccess.Slots(val2).TryGetValue((EquipmentSlot)6, out SlotView value))
			{
				val3 = ((value != null) ? ((Component)value).GetComponent<RectTransform>() : null);
			}
			if (val3 == null)
			{
				val3 = ((val2 != null) ? ((Component)val2).GetComponent<RectTransform>() : null);
			}
			if (val == null || (Object)(object)val3 == (Object)null || !((Component)val3).gameObject.activeInHierarchy)
			{
				Panel.Hide();
				SuspendPrices();
				return;
			}
			if (_controller?.Identity() != InventoryAccess.Controller(screenView).Identity())
			{
				Unsubscribe();
				_controller = InventoryAccess.Controller(screenView);
				if (_controller == null)
				{
					Panel.Hide();
					return;
				}
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
			}
			if ((Object)(object)_anchor == (Object)null || _anchor.Identity() != val3.Identity())
			{
				_anchor = val3;
				ScrollRect componentInParent = ((Component)val3).GetComponentInParent<ScrollRect>();
				_parent = (((Object)(object)((componentInParent != null) ? componentInParent.content : null) != (Object)null && ((Transform)val3).IsChildOf((Transform)(object)componentInParent.content)) ? componentInParent.content : val3);
				_font = (TMP_Text?)(object)((Component)screenView._itemsPanel._equipmentPanel).GetComponentInChildren<TextMeshProUGUI>();
			}
			if ((Object)(object)_font == (Object)null || (Object)(object)_parent == (Object)null)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (_equipment != val.Identity())
			{
				Prices.Reset();
				Prices.RememberSession();
				_equipment = val.Identity();
				_custom = Plugin.IncludeCustomTraderPrices.Value;
				_quoteReset = unscaledTime;
				_next = 0f;
				_scanAt = 0f;
			}
			if (_custom != Plugin.IncludeCustomTraderPrices.Value || unscaledTime - _quoteReset >= _quoteMinutes.Value * 60f)
			{
				Prices.Refresh();
				Prices.RememberSession();
				_custom = Plugin.IncludeCustomTraderPrices.Value;
				_quoteReset = unscaledTime;
			}
			if ((!_visible || _dirty || unscaledTime >= _next || _lastKnife != _knife.Value) && unscaledTime >= _scanAt && !((ItemController)_controller).HasActiveEvents)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				Scan(val);
				_next = unscaledTime + _inventorySeconds.Value;
				_scanAt = unscaledTime + 0.2f;
				_lastKnife = _knife.Value;
				_calculateAt = 0f;
				_dirty = false;
				if (stopwatch.Elapsed.TotalMilliseconds > 8.0 && unscaledTime >= _warningAt)
				{
					_warningAt = unscaledTime + 10f;
					Plugin.Logger.LogWarning((object)$"战备读取耗时 {stopwatch.Elapsed.TotalMilliseconds:F1} ms，物品记录 {Entries.Count}。");
				}
			}
			Prices.Tick();
			if (!_visible || unscaledTime >= _calculateAt || _unit != _options.Unit.Value)
			{
				_totals = LoadoutPolicy.A(Entries, Prices.Get);
				_calculateAt = unscaledTime + 0.2f;
				_unit = _options.Unit.Value;
				_title = "总战备：" + _options.Amount(_totals.Total) + (_showQuoteStatus.Value ? ValuePanel.Notice(_totals.Pending, _totals.Unpriced) : "");
				_detail = ValuationPolicy.A(_showEquipment.Value, _showSupplies.Value, _options.Amount(_totals.Equipment), _options.Amount(_totals.Supplies));
			}
			_visible = true;
			RectTransform obj = val3;
			Rect rect = val3.rect;
			float xMin = ((Rect)(ref rect)).xMin;
			rect = val3.rect;
			Vector3 val4 = ((Transform)obj).TransformPoint(new Vector3(xMin, ((Rect)(ref rect)).yMax, 0f));
			Vector3 val5 = ((Transform)_parent).InverseTransformPoint(val4);
			float x = val5.x;
			rect = _parent.rect;
			float num = x - ((Rect)(ref rect)).xMin;
			float y = val5.y;
			rect = _parent.rect;
			Vector2 val6 = default(Vector2);
			((Vector2)(ref val6))._002Ector(num, y - ((Rect)(ref rect)).yMax);
			ValuePanel panel = Panel;
			RectTransform? parent = _parent;
			TMP_Text? font = _font;
			ValuePanelOptions options = _options;
			string title = _title;
			string detail = _detail;
			Vector2 origin = val6;
			rect = val3.rect;
			panel.Show(parent, font, options, title, detail, origin, Math.Max(460f, ((Rect)(ref rect)).width));
		}
		catch (Exception ex)
		{
			Ready = false;
			Panel.Hide();
			SuspendPrices();
			Plugin.Logger.LogError((object)("战备估值已停用，其他功能继续运行：" + ex));
		}
	}
}
