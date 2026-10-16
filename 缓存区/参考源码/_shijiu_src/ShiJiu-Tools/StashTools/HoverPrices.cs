using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.UI.Ragfair;
using TMPro;
using UnityEngine;

namespace StashTools;

internal static class HoverPrices
{
	internal static bool Ready;

	internal static ItemView? Scope;

	private static ItemView? _view;

	private static SimpleTooltip? _tooltip;

	private static string _baseText = "";

	private static string _rendered = "";

	private static string _itemId = "";

	private static float _nextUpdate;

	private static IntPtr _session;

	private static readonly Dictionary<string, float> RetrySupply = new Dictionary<string, float>();

	private static readonly HashSet<string> QuoteWarnings = new HashSet<string>();

	private static PendingRead? _supply;

	private static float _supplyDeadline;

	private static readonly HoverRefreshGate Refresh = new HoverRefreshGate();

	private static int _lastCount;

	private static int _lastContentsHash;

	private static string _itemName = "";

	internal static void Invalidate()
	{
		Refresh.Invalidate();
	}

	internal static void Suspend()
	{
		_session = IntPtr.Zero;
		_supply = null;
		RetrySupply.Clear();
		QuoteWarnings.Clear();
		_tooltip = null;
		_view = null;
		Scope = null;
		_baseText = "";
		_rendered = "";
		_itemId = "";
		Refresh.Invalidate();
		ReleaseWidth();
	}

	private static int ContentsHash(Item item)
	{
		CompoundItem? obj = item.TryCast<CompoundItem>();
		if (obj == null)
		{
			return 0;
		}
		return obj.GetContainersHashSum();
	}

	private static bool HasStoredItems(Item item)
	{
		Grid[] array = item.TryCast<CompoundItem>()?.Grids;
		if (array == null)
		{
			return false;
		}
		Grid[] array2 = array;
		foreach (Grid val in array2)
		{
			if (val == null)
			{
				continue;
			}
			IEnumerator<Item> enumerator = val.Items.GetEnumerator();
			try
			{
				if (enumerator.MoveNext())
				{
					return true;
				}
			}
			finally
			{
				enumerator.Dispose();
			}
		}
		return false;
	}

	private static bool EligibleContext(ItemView view)
	{
		bool num = (Object)(object)view.TryCast<TradingItemView>() != (Object)null && !Selection.Owned(view.Item);
		bool flag = (Object)(object)view.TryCast<RagfairOfferItemView>() != (Object)null;
		return HoverPolicy.A(num, flag, Plugin.HoverFleaOffers.Value);
	}

	internal static bool Attach(SimpleTooltip tooltip, ref string text)
	{
		ItemView scope = Scope;
		if (!Ready || MarketQuotes.Stopped || !Plugin.HoverPriceEnabled.Value || (Object)(object)scope == (Object)null || InventoryAccess.Killed(scope) || !scope.IsSearched || scope.Item == null || ChineseSettings.IsOpen || (Object)(object)ItemUiContext.Instance == (Object)null || TradeAccess.Profile(ItemUiContext.Instance) == null || !TradeAccess.Profile(ItemUiContext.Instance).Examined(scope.Item))
		{
			return false;
		}
		if (!EligibleContext(scope))
		{
			ReleaseWidth();
			_tooltip = null;
			_view = null;
			return false;
		}
		string obj = (text.Contains('<') ? Regex.Replace(text, "<[^>]*>", "") : text);
		string text2 = LocalizationExtensions.LocalizedName(scope.Item);
		if (!obj.Contains(text2, StringComparison.Ordinal))
		{
			return false;
		}
		ReleaseWidth();
		_tooltip = tooltip;
		_view = scope;
		_baseText = text.TrimEnd();
		_itemId = scope.Item.Id;
		_itemName = text2;
		Refresh.Begin(Time.unscaledTime);
		try
		{
			_rendered = Compose();
		}
		catch
		{
			_tooltip = null;
			_view = null;
			_rendered = "";
			throw;
		}
		text = _rendered;
		_nextUpdate = Time.unscaledTime + 0.25f;
		return true;
	}

	internal static void Close(Tooltip tooltip)
	{
		if (!(_tooltip?.Identity() != tooltip.Identity()))
		{
			ReleaseWidth();
			_tooltip = null;
			_view = null;
		}
	}

	private static void ReleaseWidth()
	{
		CompactTooltip.Close();
	}

	internal static void Resize(SimpleTooltip tooltip)
	{
		IntPtr? intPtr = _tooltip?.Identity();
		IntPtr intPtr2 = tooltip.Identity();
		if (intPtr.HasValue && !(intPtr.GetValueOrDefault() != intPtr2) && !((Object)(object)tooltip._label == (Object)null))
		{
			CompactTooltip.Show(tooltip, _rendered, _rendered == _baseText);
		}
	}

	internal static void Tick()
	{
		if (!Ready || MarketQuotes.Stopped)
		{
			return;
		}
		try
		{
			CompactTooltip.Tick();
			PendingRead? supply = _supply;
			if (supply != null && supply.Tick())
			{
				_supply = null;
				Refresh.Invalidate();
			}
			if ((Object)(object)_tooltip == (Object)null || (Object)(object)_view == (Object)null)
			{
				return;
			}
			if (!Plugin.HoverPriceEnabled.Value || ChineseSettings.IsOpen)
			{
				TextMeshProUGUI label = _tooltip._label;
				if (((label != null) ? ((TMP_Text)label).text : null) == _rendered)
				{
					_tooltip.SetText(_baseText);
				}
				ReleaseWidth();
				_tooltip = null;
				_view = null;
				return;
			}
			if (!InventoryAccess.Killed(_view) && ((Component)_view).gameObject.activeInHierarchy && _view.IsSearched)
			{
				Item item = _view.Item;
				if (!(((item != null) ? item.Id : null) != _itemId))
				{
					TextMeshProUGUI label2 = _tooltip._label;
					if (!(((label2 != null) ? ((TMP_Text)label2).text : null) != _rendered))
					{
						if (Time.unscaledTime >= _nextUpdate)
						{
							_nextUpdate = Time.unscaledTime + 0.25f;
							if (!EligibleContext(_view))
							{
								_tooltip.SetText(_baseText);
								ReleaseWidth();
								_tooltip = null;
								_view = null;
								return;
							}
							if (_view.Item.StackObjectsCount != _lastCount || ContentsHash(_view.Item) != _lastContentsHash)
							{
								Refresh.Invalidate();
							}
						}
						if (Refresh.Due(Time.unscaledTime))
						{
							Refresh.Begin(Time.unscaledTime);
							string text = Compose();
							if (!(text == _rendered))
							{
								_rendered = text;
								_tooltip.SetText(text);
								Resize(_tooltip);
							}
						}
						return;
					}
				}
			}
			ReleaseWidth();
			_tooltip = null;
			_view = null;
		}
		catch (Exception ex)
		{
			ReleaseWidth();
			if (QuoteWarnings.Add(ex.GetType().Name + ex.Message))
			{
				Plugin.Logger.LogWarning((object)("悬浮价格查询失败：" + ex));
			}
			Refresh.Backoff(Time.unscaledTime);
		}
	}

	private static string Compose()
	{
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		ItemView? view = _view;
		Item val = ((view != null) ? view.Item : null);
		if (val == null)
		{
			return _baseText;
		}
		_lastCount = val.StackObjectsCount;
		_lastContentsHash = ContentsHash(val);
		if (HasStoredItems(val))
		{
			return _baseText;
		}
		ItemUiContext instance = ItemUiContext.Instance;
		object obj;
		if (instance == null)
		{
			obj = null;
		}
		else
		{
			IEftSession session = instance.Session;
			obj = ((session != null) ? session.RagFair : null);
		}
		RagFair val2 = (RagFair)obj;
		ITradingSession val3 = ((val2 != null) ? val2.TradingSession : null);
		if (val3 != null)
		{
			ItemView? view2 = _view;
			if (((view2 != null) ? view2.Item : null) != null)
			{
				if (_session != val3.Identity())
				{
					_session = val3.Identity();
					RetrySupply.Clear();
					QuoteWarnings.Clear();
					_supply = null;
				}
				IntVec2 val4 = val.CalculateCellSize();
				int num = Math.Max(1, val4.X * val4.Y);
				int num2 = Math.Max(1, val.StackObjectsCount);
				List<string> list = new List<string>();
				double? num3 = null;
				double? num4 = null;
				string text = "商人";
				int index = -1;
				int index2 = -1;
				double? num5 = null;
				if (Plugin.HoverTraderPrice.Value)
				{
					bool flag = false;
					Sales.TraderQuote traderQuote = null;
					IEnumerator<Trader> enumerator = val3.Traders.GetEnumerator();
					try
					{
						while (enumerator.MoveNext())
						{
							Trader current = enumerator.Current;
							if (!Sales.Available(current) || !TraderPricingPolicy.Includes(current.Id, Plugin.IncludeCustomTraderPrices.Value))
							{
								continue;
							}
							if (TradeAccess.Supply(current) == null)
							{
								flag = true;
								EnsureSupply(current, val3);
								continue;
							}
							Sales.TraderQuote traderQuote2 = Sales.ReadTraderQuote(current, val, logUnavailable: false);
							if (traderQuote2 != null && (traderQuote == null || traderQuote2.RoubleValue > traderQuote.RoubleValue))
							{
								traderQuote = traderQuote2;
							}
						}
					}
					finally
					{
						enumerator.Dispose();
					}
					if (flag || traderQuote == null)
					{
						list.Add(flag ? "商人: 查询中…" : "商人: 不收购");
					}
					else
					{
						num3 = traderQuote.RoubleValue;
						text = traderQuote.Trader.LocalizedName;
						index = list.Count;
						list.Add("");
					}
				}
				if (!val.CanSellOnRagfair)
				{
					list.Add("跳蚤市场: 禁止交易");
				}
				else
				{
					MarketQuotes.Quote quote = Quote(val.StringTemplateId);
					List<(MarketQuotes.Quote Quote, double Units)> parts = new List<(MarketQuotes.Quote, double)>();
					Weapon val5 = val.TryCast<Weapon>();
					HashSet<string> plateIds = ((val5 == null) ? ArmorValuation.RemovablePlateIds(val) : new HashSet<string>());
					List<(MarketQuotes.Quote Quote, double Units)> plateParts = new List<(MarketQuotes.Quote, double)>();
					ItemValuation.Walk(val, delegate(Item child)
					{
						(MarketQuotes.Quote, double) item = (Quote(child.StringTemplateId), ItemValuation.Units(child));
						parts.Add(item);
						if (plateIds.Contains(child.Id))
						{
							plateParts.Add(item);
						}
					}, includeRoot: false);
					bool num6 = (quote.Pending && quote.Price <= 0.0) || parts.Any(((MarketQuotes.Quote Quote, double Units) p) => p.Quote.Pending && p.Quote.Price <= 0.0);
					bool flag2 = quote.Price <= 0.0 || parts.Any(((MarketQuotes.Quote Quote, double Units) p) => p.Quote.Price <= 0.0 && p.Units > 0.0);
					if (num6)
					{
						list.Add("跳蚤市场: 查询中…");
					}
					else if (flag2)
					{
						list.Add((parts.Count > 0) ? "跳蚤市场: 报价不全" : "跳蚤市场: 暂无报价");
					}
					else
					{
						double value = quote.Price * ItemValuation.Units(val) + parts.Sum(((MarketQuotes.Quote Quote, double Units) p) => p.Quote.Price * p.Units);
						num4 = value;
						index2 = list.Count;
						list.Add("");
					}
					List<(MarketQuotes.Quote, double)> list2 = ((val5 != null) ? parts : plateParts);
					if (list2.Count > 0)
					{
						list.Add(((val5 != null) ? "配件及弹药: " : "可拆插板: ") + (list2.Any<(MarketQuotes.Quote, double)>(((MarketQuotes.Quote Quote, double Units) p) => p.Quote.Pending && p.Quote.Price <= 0.0) ? "查询中…" : (list2.Any<(MarketQuotes.Quote, double)>(((MarketQuotes.Quote Quote, double Units) p) => p.Quote.Price <= 0.0 && p.Units > 0.0) ? "报价不全" : PriceText.A(list2.Sum<(MarketQuotes.Quote, double)>(((MarketQuotes.Quote Quote, double Units) p) => p.Quote.Price * p.Units), Plugin.HoverPriceUnit.Value))));
					}
					if (Plugin.HoverEligibilityNote.Value)
					{
						string text2 = Sales.FleaRestriction(val, val2);
						if (text2 != null)
						{
							list.Add(text2);
						}
					}
				}
				if (num3.HasValue)
				{
					list[index] = PriceText.A(text, num3.Value, num2, num, Plugin.HoverPerSlot.Value, Plugin.HoverPriceColor.Value, Plugin.HoverPriceUnit.Value, PriceText.A(num3.Value, num4));
				}
				if (num4.HasValue)
				{
					list[index2] = PriceText.A("跳蚤市场", num4.Value, num2, num, Plugin.HoverPerSlot.Value, Plugin.HoverPriceColor.Value, Plugin.HoverPriceUnit.Value, PriceText.A(num4.Value, num3));
				}
				if (num3.HasValue || num4.HasValue)
				{
					num5 = Math.Max(num3.GetValueOrDefault(), num4.GetValueOrDefault()) / (double)num2 / (double)num;
				}
				return PriceText.A(_baseText, _itemName, num5, Plugin.HoverNameColor.Value) + "\n<color=#b9c1c3>" + string.Join("\n", list) + "</color>";
			}
		}
		_session = IntPtr.Zero;
		_supply = null;
		RetrySupply.Clear();
		return _baseText + "\n价格数据暂不可用";
	}

	private static MarketQuotes.Quote Quote(string key)
	{
		MarketQuotes.Quote result = MarketQuotes.Get(key);
		Refresh.Track(result.Expires);
		return result;
	}

	private static void EnsureSupply(Trader trader, ITradingSession session)
	{
		if (MarketQuotes.Stopped || _supply != null || Batch.Busy || (RetrySupply.TryGetValue(trader.Id, out var value) && Time.unscaledTime < value))
		{
			return;
		}
		RetrySupply[trader.Id] = Time.unscaledTime + 15f;
		IntPtr pointer = _session;
		Task<Result<SupplyData>> request = session.GetSupplyData(trader.Id);
		if (request == null)
		{
			return;
		}
		_supplyDeadline = Time.unscaledTime + 10f;
		_supply = new PendingRead(delegate
		{
			if (!MarketQuotes.Stopped && _session == pointer)
			{
				ItemUiContext instance = ItemUiContext.Instance;
				if (instance != null)
				{
					IEftSession session2 = instance.Session;
					IntPtr? obj;
					if (session2 == null)
					{
						obj = null;
					}
					else
					{
						RagFair ragFair = session2.RagFair;
						obj = ((ragFair == null) ? ((IntPtr?)null) : ragFair.TradingSession?.Identity());
					}
					if (obj == pointer)
					{
						return Time.unscaledTime < _supplyDeadline;
					}
				}
			}
			return false;
		}, () => request.IsCompleted, delegate
		{
			//IL_0021: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			if (!request.IsCanceled && !request.IsFaulted)
			{
				Result<SupplyData> result = request.Result;
				if (!result.Failed && result.Value != null)
				{
					TradeAccess.SetSupply(trader, result.Value);
				}
			}
		}, delegate(Exception ex)
		{
			Plugin.Logger.LogWarning((object)("悬浮价格数据加载失败：" + trader.Id + " " + ex.Message));
		});
	}
}
