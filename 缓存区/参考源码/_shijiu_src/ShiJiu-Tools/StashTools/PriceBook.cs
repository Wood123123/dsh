using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.Ragfair;
using UnityEngine;

namespace StashTools;

internal sealed class PriceBook
{
	private sealed class Entry
	{
		internal bool Flea;

		internal bool Done;

		internal bool FleaDone;

		internal string Template = "";

		internal int DogtagLevel;

		internal double FleaPrice;

		internal double Price;

		internal float Deadline;

		internal float RetryAt;
	}

	private readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

	private readonly Dictionary<string, float> SupplyAttempts = new Dictionary<string, float>();

	private ITradingSession? _session;

	private PendingRead? _supply;

	private int _generation;

	private float _next;

	private bool _includeCustom;

	private bool _active = true;

	private bool _frozen;

	internal void Reset()
	{
		Suspend();
		Entries.Clear();
		_frozen = false;
		_active = true;
		_next = 0f;
		_includeCustom = Plugin.IncludeCustomTraderPrices.Value;
	}

	internal void Suspend()
	{
		_active = false;
		_generation++;
		_session = null;
		_supply = null;
		SupplyAttempts.Clear();
	}

	internal void Resume()
	{
		_active = true;
		if (!_frozen)
		{
			return;
		}
		_frozen = false;
		foreach (Entry item in Entries.Values.Where((Entry e) => e.Price <= 0.0))
		{
			item.Done = false;
			item.FleaDone = false;
			item.Deadline = Time.unscaledTime + 15f;
		}
	}

	internal void Freeze()
	{
		Suspend();
		_frozen = true;
		foreach (Entry value in Entries.Values)
		{
			if (value.Flea && !value.FleaDone)
			{
				value.FleaPrice = MarketQuotes.Peek(value.Template) ?? value.FleaPrice;
			}
			value.Price = ValuationPolicy.A(value.Price, value.FleaPrice);
			value.Done = true;
		}
	}

	internal void RememberSession()
	{
		if (!_active || MarketQuotes.Stopped)
		{
			return;
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
			if (session == null)
			{
				obj = null;
			}
			else
			{
				RagFair ragFair = session.RagFair;
				obj = ((ragFair != null) ? ragFair.TradingSession : null);
			}
		}
		ITradingSession val = (ITradingSession)obj;
		if (!(val?.Identity() == _session?.Identity()))
		{
			_generation++;
			_supply = null;
			SupplyAttempts.Clear();
			_session = val;
		}
	}

	internal static string Key(Item item)
	{
		DogtagComponent itemComponent = item.GetItemComponent<DogtagComponent>();
		if (itemComponent != null)
		{
			return item.StringTemplateId + "/tag/" + itemComponent.Level;
		}
		return item.StringTemplateId;
	}

	internal void Register(Item item)
	{
		string key = Key(item);
		if (!Entries.ContainsKey(key))
		{
			bool canSellOnRagfair = item.Template.CanSellOnRagfair;
			double num = ((_frozen & canSellOnRagfair) ? MarketQuotes.Peek(item.StringTemplateId).GetValueOrDefault() : 0.0);
			Entries.Add(key, new Entry
			{
				Template = item.StringTemplateId,
				Flea = canSellOnRagfair,
				Price = num,
				FleaPrice = num,
				DogtagLevel = (item.GetItemComponent<DogtagComponent>()?.Level ?? 0),
				Done = _frozen,
				Deadline = Time.unscaledTime + 15f
			});
		}
	}

	internal void Refresh()
	{
		if (_frozen)
		{
			return;
		}
		_generation++;
		SupplyAttempts.Clear();
		_supply = null;
		_includeCustom = Plugin.IncludeCustomTraderPrices.Value;
		foreach (Entry value in Entries.Values)
		{
			value.Done = false;
			value.FleaDone = false;
			value.FleaPrice = 0.0;
			value.Deadline = Time.unscaledTime + 15f;
		}
	}

	internal double? Get(string template)
	{
		if (!Entries.TryGetValue(template, out Entry value) || (!value.Done && !(value.Price > 0.0)))
		{
			if (!_frozen)
			{
				return null;
			}
			return 0.0;
		}
		return value.Price;
	}

	internal void Tick()
	{
		if (!_active || MarketQuotes.Stopped || Time.unscaledTime < _next)
		{
			return;
		}
		_next = Time.unscaledTime + 0.1f;
		RememberSession();
		PendingRead? supply = _supply;
		if (supply != null && supply.Tick())
		{
			_supply = null;
		}
		foreach (KeyValuePair<string, Entry> entry in Entries)
		{
			Entry value = entry.Value;
			if (value.Done)
			{
				if (value.Price > 0.0 || Time.unscaledTime < value.RetryAt)
				{
					continue;
				}
				value.Done = false;
				value.FleaDone = false;
				value.Deadline = Time.unscaledTime + 15f;
				SupplyAttempts.Clear();
			}
			value.RetryAt = Time.unscaledTime + 60f;
			if (value.Flea && !value.FleaDone)
			{
				MarketQuotes.Quote quote = MarketQuotes.Get(value.Template);
				if (quote.Pending)
				{
					continue;
				}
				value.FleaPrice = quote.Price;
				value.FleaDone = true;
			}
			if (_session == null)
			{
				if (!(Time.unscaledTime < value.Deadline))
				{
					value.Price = ValuationPolicy.A(value.Price, value.FleaPrice);
					value.Done = true;
				}
				continue;
			}
			bool flag = false;
			List<Trader> list = new List<Trader>();
			IEnumerator<Trader> enumerator2 = _session.Traders.GetEnumerator();
			try
			{
				while (enumerator2.MoveNext())
				{
					Trader trader = enumerator2.Current;
					if (!Sales.Available(trader) || !TraderPricingPolicy.Includes(trader.Id, _includeCustom))
					{
						continue;
					}
					list.Add(trader);
					if (TradeAccess.Supply(trader) != null)
					{
						continue;
					}
					if (SupplyAttempts.TryGetValue(trader.Id, out var value2))
					{
						if (Time.unscaledTime < value2)
						{
							flag = true;
						}
						continue;
					}
					flag = true;
					if (_supply != null)
					{
						continue;
					}
					int generation = _generation;
					float timeout = Time.unscaledTime + 12f;
					SupplyAttempts[trader.Id] = timeout;
					Task<Result<SupplyData>> task = _session.GetSupplyData(trader.Id);
					if (task == null)
					{
						SupplyAttempts[trader.Id] = Time.unscaledTime;
						continue;
					}
					_supply = new PendingRead(() => _active && generation == _generation && !MarketQuotes.Stopped && Time.unscaledTime < timeout, () => task.IsCompleted, delegate
					{
						//IL_0041: Unknown result type (might be due to invalid IL or missing references)
						//IL_0046: Unknown result type (might be due to invalid IL or missing references)
						SupplyAttempts[trader.Id] = Time.unscaledTime;
						if (!task.IsCanceled && !task.IsFaulted)
						{
							Result<SupplyData> result = task.Result;
							if (!result.Failed && result.Value != null)
							{
								TradeAccess.SetSupply(trader, result.Value);
							}
						}
					}, delegate(Exception ex2)
					{
						Plugin.Logger.LogWarning((object)("商人报价加载失败：" + ex2.Message));
					});
				}
			}
			finally
			{
				enumerator2.Dispose();
			}
			if (flag)
			{
				continue;
			}
			try
			{
				ItemFactory factory = Singleton<ItemFactory>.Instance;
				if (factory == null)
				{
					if (!(Time.unscaledTime < value.Deadline))
					{
						value.Price = ValuationPolicy.A(value.Price, value.FleaPrice);
						value.Done = true;
					}
					continue;
				}
				Item val = factory.CreateItem(Guid.NewGuid().ToString("N").Substring(0, 24), value.Template, (UnparsedData)null);
				val.StackObjectsCount = 1;
				ArmorValuation.PrepareBodySample(val, (string tpl) => factory.CreateItem(Guid.NewGuid().ToString("N").Substring(0, 24), tpl, (UnparsedData)null));
				DogtagComponent itemComponent = val.GetItemComponent<DogtagComponent>();
				if (itemComponent != null)
				{
					itemComponent.Level = value.DogtagLevel;
				}
				double num = 0.0;
				foreach (Trader item in list)
				{
					if (TradeAccess.Supply(item) != null)
					{
						Sales.TraderQuote traderQuote = Sales.ReadTraderQuote(item, val, logUnavailable: false);
						if (traderQuote != null)
						{
							num = Math.Max(num, traderQuote.RoubleValue);
						}
					}
				}
				num = ValuationPolicy.A(value.FleaPrice, num);
				if (num > 0.0 || value.Price <= 0.0)
				{
					value.Price = num;
				}
				value.Done = true;
				if (value.Price <= 0.0)
				{
					Plugin.Logger.LogInfo((object)("估值无报价：" + entry.Key + "，物品=" + LocalizationExtensions.LocalizedName(val)));
				}
				break;
			}
			catch (Exception ex)
			{
				value.Price = ValuationPolicy.A(value.Price, value.FleaPrice);
				value.Done = true;
				Plugin.Logger.LogWarning((object)("物品无法估价：" + entry.Key + " " + ex.Message));
				break;
			}
		}
	}
}
