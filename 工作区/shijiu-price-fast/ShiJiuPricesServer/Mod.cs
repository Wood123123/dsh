using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;

namespace ShiJiuPrices;

/// <summary>
/// Server side of "ShiJiu Price Fast".
///
/// Registers GET /shijiu/prices which returns the ENTIRE flea price table in one
/// response, keyed by template id, as a flat JSON object: { "&lt;tpl&gt;": 12345.6, ... }
///
/// This exists because ShiJiu-Tools prices each template with its own serialized
/// POST /client/ragfair/itemMarketPrice, rate-limited to one request every 0.25s
/// (see MarketTransport.StartGate). That caps it at ~4 templates/second, so freshly
/// looted items can sit unpriced for tens of seconds. One bulk table removes that
/// entirely while leaving all of ShiJiu's own valuation logic untouched.
/// </summary>
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.local.shijiuprices";
    public string Name { get; init; } = "ShiJiu Price Fast (server)";
    public string Author { get; init; } = "local";
    public List<string> Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string> Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range> ModDependencies { get; init; }
    public string Url { get; init; } = "";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; }
}

[Injectable]
public class ShiJiuPricesRouter : StaticRouter
{
    public ShiJiuPricesRouter(JsonUtil jsonUtil, ShiJiuPricesCallback callback)
        : base(jsonUtil, new List<RouteAction>
        {
            new RouteAction<EmptyRequestData>(
                "/shijiu/prices",
                (url, info, sessionId, output, cancellationToken) =>
                    callback.HandleGetPrices(url, info, sessionId))
        })
    {
    }
}

[Injectable]
public class ShiJiuPricesCallback
{
    private readonly HttpResponseUtil _httpResponseUtil;
    private readonly RagfairPriceService _ragfairPriceService;
    private readonly RagfairOfferService _ragfairOfferService;

    public ShiJiuPricesCallback(
        HttpResponseUtil httpResponseUtil,
        RagfairPriceService ragfairPriceService,
        RagfairOfferService ragfairOfferService)
    {
        _httpResponseUtil = httpResponseUtil;
        _ragfairPriceService = ragfairPriceService;
        _ragfairOfferService = ragfairOfferService;
    }

    public ValueTask<string> HandleGetPrices(string url, EmptyRequestData info, MongoId sessionId)
    {
        var prices = new Dictionary<string, double>();

        // 1) The static flea base table. Some SPT builds / price-editing mods leave
        //    NaN or Infinity in here, which System.Text.Json refuses to write.
        foreach (var kvp in _ragfairPriceService.GetAllFleaPrices())
        {
            if (!IsUsablePrice(kvp.Value)) continue;
            prices[kvp.Key.ToString()] = kvp.Value;
        }

        // 2) Live fake-player offers, so dynamically priced items (and barter-heavy
        //    listings) reflect what the flea market actually shows. Averages override
        //    the base table for every template that has at least one live offer.
        var sums = new Dictionary<string, double>();
        var counts = new Dictionary<string, int>();

        foreach (var offer in _ragfairOfferService.GetOffers())
        {
            if (offer == null) continue;
            if (offer.CreatedBy != OfferCreator.FakePlayer) continue;

            var items = offer.Items;
            if (items == null || items.Count == 0) continue;

            double cost = offer.RequirementsCost ?? 0;
            if (!IsUsablePrice(cost) || cost <= 0) continue;

            var first = items[0];
            if (first == null) continue;

            string template = first.Template.ToString();
            if (string.IsNullOrEmpty(template)) continue;

            sums.TryGetValue(template, out double s);
            counts.TryGetValue(template, out int c);
            sums[template] = s + cost;
            counts[template] = c + 1;
        }

        foreach (var kvp in sums)
        {
            int count = counts[kvp.Key];
            if (count <= 0) continue;
            double avg = kvp.Value / count;
            if (!IsUsablePrice(avg)) continue;
            prices[kvp.Key] = avg;
        }

        return new ValueTask<string>(_httpResponseUtil.GetBody(JsonSerializer.Serialize(prices)));
    }

    private static bool IsUsablePrice(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);
}
