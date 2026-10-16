using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.Ragfair;
using UnityEngine;

namespace StashTools;

internal static class Sales
{
	internal sealed record TraderQuote(Trader Trader, int Amount, double RoubleValue);

	private const string Roubles = "5449016a4bdc2d6f028b456f";

	private static PendingRead? _pendingSupply;

	private static PendingRead? _pendingQuote;

	private static OfferBudget _offerBudget = new OfferBudget(0, 0);

	internal static void Tick()
	{
		PendingRead? pendingSupply = _pendingSupply;
		if (pendingSupply != null && pendingSupply.Tick())
		{
			_pendingSupply = null;
		}
		PendingRead? pendingQuote = _pendingQuote;
		if (pendingQuote != null && pendingQuote.Tick())
		{
			_pendingQuote = null;
		}
	}

	internal static bool Available(Trader t)
	{
		if (t != null && t.Info != null && t.Info.Available && !t.Info.Disabled)
		{
			return t.Info.Unlocked;
		}
		return false;
	}

	private static void LoadSupply(Trader trader, ITradingSession session, Action<bool, string> done)
	{
		if (!Available(trader))
		{
			done(arg1: true, "");
			return;
		}
		if (TradeAccess.Supply(trader) != null)
		{
			done(arg1: true, "");
			return;
		}
		int generation = Batch.Generation;
		Plugin.Logger.LogInfo((object)("加载商人价格数据：" + trader.Id));
		Task<Result<SupplyData>> request = session.GetSupplyData(trader.Id);
		if (request == null)
		{
			done(arg1: false, "无法请求商人价格数据，未提交出售。");
			return;
		}
		_pendingSupply = new PendingRead(() => Batch.Busy && Batch.Generation == generation && StashBehaviour.CanSell, () => request.IsCompleted, delegate
		{
			//IL_006d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0072: Unknown result type (might be due to invalid IL or missing references)
			if (request.IsCanceled || request.IsFaulted)
			{
				Plugin.Logger.LogWarning((object)("商人价格请求失败：" + trader.Id + " " + request.Exception));
				done(arg1: false, "商人价格数据加载失败，未提交出售，请重试。");
			}
			else
			{
				Result<SupplyData> result = request.Result;
				if (result.Failed || result.Value == null)
				{
					Plugin.Logger.LogWarning((object)("商人价格返回无效：" + trader.Id + " " + result.Error));
					done(arg1: false, "商人价格数据不完整，未提交出售。");
				}
				else
				{
					TradeAccess.SetSupply(trader, result.Value);
					Plugin.Logger.LogInfo((object)("商人价格数据就绪：" + trader.Id));
					done(arg1: true, "");
				}
			}
		}, delegate(Exception ex)
		{
			Plugin.Logger.LogError((object)("加载商人价格数据异常：" + trader.Id + " " + ex));
			done(arg1: false, "商人价格数据加载异常，未提交出售。");
		});
	}

	private static bool Eligible(Item i, bool forceFir)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		if (Selection.Owned(i) && (int)i.PinLockState == 0 && !i.QuestItem && (!i.IsContainer || ItemExtensions.IsEmpty(i)) && ((!Plugin.ProtectFir.Value | forceFir) || !i.MarkedAsSpawnedInSession))
		{
			return TradeAccess.Profile(ItemUiContext.Instance).Examined(i);
		}
		return false;
	}

	internal static void Start(Item[] raw, SellMode mode)
	{
		if (!StashBehaviour.CanSell || Batch.Busy)
		{
			return;
		}
		bool force = Input.GetKey((KeyCode)308);
		Item[] array = (from i in raw
			group i by i.Id into g
			select g.First() into i
			where Eligible(i, force)
			select i).ToArray();
		int num = raw.Length - array.Length;
		if (array.Length == 0)
		{
			StashBehaviour.Notify("没有可出售的物品：检查固定/锁定、任务物品、非空容器或未检视状态。");
			return;
		}
		if (num > 0)
		{
			StashBehaviour.Notify($"跳过 {num} 件受保护或不可出售物品。");
		}
		ItemUiContext instance = ItemUiContext.Instance;
		RagFair rag = instance.Session.RagFair;
		ITradingSession session = rag.TradingSession;
		FleaPriceBasis basis = Plugin.PriceBasis.Value;
		double factor = Plugin.PriceFactor.Value;
		Plugin.Logger.LogInfo((object)$"Sale pricing: mode={mode}, basis={basis}, multiplier={factor}");
		_offerBudget = new OfferBudget(rag.MaxOffersCount, rag.MyOffersCount);
		List<(string, Action<Action<bool, string>>)> list = new List<(string, Action<Action<bool, string>>)>();
		if (mode != SellMode.仅跳蚤)
		{
			IEnumerator<Trader> enumerator = session.Traders.GetEnumerator();
			HashSet<string> hashSet = new HashSet<string>();
			while (enumerator.MoveNext())
			{
				Trader trader = enumerator.Current;
				if (Available(trader) && hashSet.Add(trader.Id))
				{
					list.Add(("加载商人价格", delegate(Action<bool, string> done)
					{
						LoadSupply(trader, session, done);
					}));
				}
			}
		}
		foreach (IGrouping<string, Item> item in from i in array
			group i by i.StringTemplateId)
		{
			Item[] batch = item.ToArray();
			list.Add(("查询 " + batch[0].ShortName, delegate(Action<bool, string> done)
			{
				int generation = Batch.Generation;
				if (mode == SellMode.仅商人 || !batch.Any((Item i) => FleaRestriction(i, rag) == null))
				{
					Prepare(batch, mode, 0.0, factor, rag, session, force);
					done(arg1: true, "");
				}
				else
				{
					Task<MarketQuote> request = MarketQuotes.Read(batch[0].StringTemplateId);
					_pendingQuote = new PendingRead(() => Batch.Busy && generation == Batch.Generation && StashBehaviour.CanSell && !MarketQuotes.Stopped, () => request.IsCompleted, delegate
					{
						if (request.IsCanceled || request.IsFaulted)
						{
							_ = request.Exception;
							done(arg1: false, "市场报价暂不可用，未提交出售，请稍后重试。");
							return;
						}
						try
						{
							MarketQuote result = request.Result;
							double num2 = result.Select(basis);
							Plugin.Logger.LogInfo((object)$"Market quote: tpl={batch[0].StringTemplateId}, min={result.Min}, avg={result.Average}, max={result.Max}, basis={basis}, factor={factor}, unitPrice={MarketPricing.A(num2, factor)}");
							Prepare(batch, mode, num2, factor, rag, session, force);
							done(arg1: true, "");
						}
						catch (Exception ex)
						{
							StashBehaviour.Error("价格计算失败", ex);
							done(arg1: false, "价格计算失败，停止出售。");
						}
					}, delegate(Exception ex)
					{
						Plugin.Logger.LogWarning((object)("独立市场报价失败：" + ex));
						done(arg1: false, "市场报价读取失败，未提交出售。");
					});
				}
			}));
		}
		Batch.Start("批量出售", list);
	}

	private static TraderQuote? BestTrader(Item i, ITradingSession session)
	{
		TraderQuote traderQuote = null;
		IEnumerator<Trader> enumerator = session.Traders.GetEnumerator();
		while (enumerator.MoveNext())
		{
			Trader current = enumerator.Current;
			if (Available(current))
			{
				TraderQuote traderQuote2 = ReadTraderQuote(current, i);
				if (traderQuote2 != null && (traderQuote == null || traderQuote2.RoubleValue > traderQuote.RoubleValue))
				{
					traderQuote = traderQuote2;
				}
			}
		}
		return traderQuote;
	}

	internal static TraderQuote? ReadTraderQuote(Trader t, Item i, bool logUnavailable = true)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		if (!Available(t))
		{
			return null;
		}
		if (TradeAccess.Supply(t) == null)
		{
			throw new InvalidOperationException("商人价格数据尚未加载：" + t.Id);
		}
		ItemPrice? val = NativeTraderPrice.Read(t, i);
		if (!val.HasValue || val.Value.Amount <= 0)
		{
			return null;
		}
		ItemPrice value = val.Value;
		string text = NativeTraderPrice.Currency(value);
		if (text == null)
		{
			return null;
		}
		double value2 = 1.0;
		if (text != "5449016a4bdc2d6f028b456f" && (t.CurrencyCourses == null || !t.CurrencyCourses.TryGetValue(text, out value2) || value2 <= 0.0))
		{
			return null;
		}
		double num = (double)value.Amount * value2;
		if (logUnavailable)
		{
			Plugin.Logger.LogInfo((object)$"Trader quote: item={i.Id}, trader={t.Id}, amount={value.Amount}, currency={text}, roubles={num}");
		}
		if (!double.IsFinite(num))
		{
			return null;
		}
		return new TraderQuote(t, value.Amount, num);
	}

	internal static string? FleaRestriction(Item i, RagFair rag)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Invalid comparison between Unknown and I4
		if (rag.Disabled)
		{
			return "跳蚤市场未开放";
		}
		if (!rag.Available)
		{
			if ((int)rag.Status != 2)
			{
				return "跳蚤市场当前不可交易";
			}
			return $"跳蚤交易需要 {RagFair.Settings.minUserLevel} 级";
		}
		FleaRules.Tick(force: true);
		bool isOnlyFoundInRaidAllowed = RagFair.Settings.isOnlyFoundInRaidAllowed;
		return FleaEligibility.Restriction(i.CanSellOnRagfair, !isOnlyFoundInRaidAllowed || i.CanSellOnRagfairRaidRelated, isOnlyFoundInRaidAllowed);
	}

	private static void Prepare(Item[] items, SellMode mode, double market, double factor, RagFair rag, ITradingSession session, bool force)
	{
		Dictionary<int, List<Item>> dictionary = new Dictionary<int, List<Item>>();
		foreach (Item i2 in items)
		{
			if (!Eligible(i2, force))
			{
				continue;
			}
			TraderQuote traderQuote = ((mode != SellMode.仅跳蚤) ? BestTrader(i2, session) : null);
			string text = ((mode == SellMode.仅商人) ? null : FleaRestriction(i2, rag));
			if (mode != SellMode.仅商人 && !rag.Available)
			{
				Batch.Skip(text ?? "跳蚤市场当前不可交易");
				continue;
			}
			bool flag = mode != SellMode.仅商人 && text == null && market > 0.0;
			int num = (flag ? MarketPricing.A(market, factor) : 0);
			if (num <= 0)
			{
				flag = false;
			}
			double num2 = (flag ? PriceCalculator.CalculateTaxPrice(i2, i2.StackObjectsCount, (double)num, false) : 0.0);
			double num3 = (double)num * (double)i2.StackObjectsCount - num2;
			if (!double.IsFinite(num2) || num2 < 0.0 || num3 <= 0.0)
			{
				flag = false;
			}
			if (flag && (mode == SellMode.仅跳蚤 || traderQuote == null || num3 > traderQuote.RoubleValue))
			{
				if (!dictionary.TryGetValue(num, out var value))
				{
					value = (dictionary[num] = new List<Item>());
				}
				value.Add(i2);
				continue;
			}
			if (traderQuote != null && mode != SellMode.仅跳蚤)
			{
				TraderQuote local = traderQuote;
				Batch.Append("卖给商人 " + i2.ShortName, delegate(Action<bool, string> done)
				{
					SellTrader(i2, local, session, force, done);
				});
				continue;
			}
			Batch.Skip(text ?? "无有效报价或收益不足");
			Plugin.Logger.LogInfo((object)$"Sale skipped: item={i2.Id}, tpl={i2.StringTemplateId}, market={market}, tax={num2}, trader={traderQuote?.RoubleValue}, restriction={text}");
		}
		foreach (KeyValuePair<int, List<Item>> item in dictionary)
		{
			int price = item.Key;
			Item[] group = item.Value.ToArray();
			Batch.Append($"上架跳蚤 {group.Length} 件", delegate(Action<bool, string> done)
			{
				SellFlea(group, price, rag, force, done);
			});
		}
	}

	private static void SellTrader(Item i, TraderQuote quote, ITradingSession session, bool force, Action<bool, string> done)
	{
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Expected O, but got Unknown
		if (!Eligible(i, force))
		{
			done(arg1: false, "物品状态变化，停止出售。");
			return;
		}
		TraderQuote traderQuote = ReadTraderQuote(quote.Trader, i);
		if (traderQuote == null)
		{
			done(arg1: false, "商人报价已失效。");
			return;
		}
		TradingItemReference[] array = (TradingItemReference[])(object)new TradingItemReference[1]
		{
			new TradingItemReference
			{
				Item = i,
				Count = i.StackObjectsCount
			}
		};
		Plugin.Logger.LogInfo((object)$"Trader submit: item={i.Id}, trader={quote.Trader.Id}, count={i.StackObjectsCount}, amount={traderQuote.Amount}");
		session.ConfirmSell(quote.Trader.Id, array, traderQuote.Amount, (Callback)delegate(IResult r)
		{
			StashBehaviour.Post(delegate
			{
				if (r == null || r.Failed)
				{
					ManualLogSource logger = Plugin.Logger;
					IResult obj = r;
					logger.LogWarning((object)("Trader rejected: " + ((obj != null) ? obj.Error : null)));
					done(arg1: false, "商人未接受此次出售，详情见日志。");
				}
				else
				{
					Selection.Items.Remove(i.Id);
					Batch.Success("卖给商人");
					Plugin.Logger.LogInfo((object)("Trader committed: " + i.Id));
					done(arg1: true, "");
				}
			});
		});
	}

	private static void SellFlea(Item[] items, int price, RagFair rag, bool force, Action<bool, string> done)
	{
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_0195: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Expected O, but got Unknown
		if (items.Any((Item i) => !Eligible(i, force)))
		{
			done(arg1: false, "物品状态变化，停止上架。");
			return;
		}
		if (!rag.Available || rag.Disabled || RagFair.IsAddOffersTemporaryDisabled())
		{
			Batch.Skip("跳蚤市场当前不可上架", items.Length);
			done(arg1: true, "");
			return;
		}
		List<Item> list = new List<Item>();
		Item[] array = items;
		foreach (Item val in array)
		{
			string text = FleaRestriction(val, rag);
			if (text != null)
			{
				Batch.Skip(text);
			}
			else
			{
				list.Add(val);
			}
		}
		items = list.ToArray();
		if (items.Length == 0)
		{
			done(arg1: true, "");
			return;
		}
		if (!_offerBudget.A(rag.MaxOffersCount, rag.MyOffersCount))
		{
			Batch.Skip("跳蚤报价栏位已满", items.Length);
			done(arg1: true, "");
			return;
		}
		OfferBudget budget = _offerBudget;
		string[] array2 = items.Select((Item i) => i.Id).ToArray();
		BarterTemplate[] array3 = (BarterTemplate[])(object)new BarterTemplate[1]
		{
			new BarterTemplate
			{
				_tpl = "5449016a4bdc2d6f028b456f",
				count = price
			}
		};
		Plugin.Logger.LogInfo((object)string.Format("Flea submit: {0}, unitPrice={1}", string.Join(",", items.Select((Item i) => i.Id)), price));
		rag.AddOffer(false, array2, array3, (Action)delegate
		{
			StashBehaviour.Post(delegate
			{
				bool flag = false;
				for (int i = 0; i < rag.MyOffers.Count; i++)
				{
					Item item = rag.MyOffers[i].Item;
					if (((item != null) ? item.Id : null) == items[0].Id)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					done(arg1: false, "上架回调已返回，但尚未确认报价。停止后续提交，请查看报价列表。");
				}
				else
				{
					Item[] array4 = items;
					foreach (Item val2 in array4)
					{
						Selection.Items.Remove(val2.Id);
					}
					budget.A();
					Batch.Success("上架跳蚤", items.Length);
					Plugin.Logger.LogInfo((object)("Flea committed: " + string.Join(",", items.Select((Item val3) => val3.Id))));
					done(arg1: true, "");
				}
			});
		});
	}
}
