using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FikaServer.Services;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace NekoParty.Server.GroupEX.Services;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoModFingerprintService(IReadOnlyList<SptMod> loadedMods, TemplateTable templateTable, ISptLogger<NekoModFingerprintService> logger)
{
	public sealed class ModEntry
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = "";

		[JsonPropertyName("name")]
		public string Name { get; set; } = "";

		[JsonPropertyName("ver")]
		public string Version { get; set; } = "";
	}

	public sealed class Manifest
	{
		[JsonPropertyName("spt")]
		public string SptVersion { get; set; } = "";

		[JsonPropertyName("fika")]
		public string FikaVersion { get; set; } = "";

		[JsonPropertyName("mods")]
		public List<ModEntry> Mods { get; set; } = new List<ModEntry>();

		[JsonPropertyName("items")]
		public int ItemCount { get; set; }

		[JsonPropertyName("itemsHash")]
		public string ItemsHash { get; set; } = "";
	}

	public sealed class CompareResult
	{
		public bool Mismatch;

		public string Report = "";

		public List<string> Diffs = new List<string>();
	}

	private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	private const int MaxReportLines = 100;

	private readonly object _sync = new object();

	private string? _json;

	private Manifest? _local;

	public string Json
	{
		get
		{
			EnsureBuilt();
			return _json ?? "";
		}
	}

	private void EnsureBuilt()
	{
		if (_json != null)
		{
			return;
		}
		lock (_sync)
		{
			if (_json != null)
			{
				return;
			}
			try
			{
				List<ModEntry> list = loadedMods.Select((SptMod m) => new ModEntry
				{
					Id = m.ModMetadata.ModGuid,
					Name = m.ModMetadata.Name,
					Version = ((object)m.ModMetadata.Version).ToString()
				}).OrderBy<ModEntry, string>((ModEntry m) => m.Id, StringComparer.Ordinal).ToList();
				List<string> list2 = templateTable.Items.Keys.Select(delegate(MongoId k)
				{
					//IL_0003: Unknown result type (might be due to invalid IL or missing references)
					return ((object)k).ToString();
				}).OrderBy<string, string>((string s) => s, StringComparer.Ordinal).ToList();
				_local = new Manifest
				{
					SptVersion = ((object)ProgramStatics.SPT_VERSION()).ToString(),
					FikaVersion = (typeof(MatchService).Assembly.GetName().Version?.ToString() ?? ""),
					Mods = list,
					ItemCount = list2.Count,
					ItemsHash = ShortHash(string.Join("\n", list2))
				};
				_json = JsonSerializer.Serialize(_local, JsonOpts);
				logger.Info($"[NekoModCheck] 本服指纹就绪：SPT {_local.SptVersion} / Fika {_local.FikaVersion} / mods {list.Count} / items {list2.Count} ({_local.ItemsHash})", (Exception)null);
			}
			catch (Exception ex)
			{
				logger.Warning("[NekoModCheck] 计算本服 MOD 指纹失败（跨服比对将跳过）: " + ex.Message, (Exception)null);
				_local = null;
				_json = "";
			}
		}
	}

	public static string ShortHash(string input)
	{
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).Substring(0, 16).ToLowerInvariant();
	}

	public CompareResult Compare(string? remoteJson, string peerName)
	{
		EnsureBuilt();
		CompareResult compareResult = new CompareResult();
		if (_local == null || string.IsNullOrEmpty(remoteJson))
		{
			return compareResult;
		}
		Manifest manifest;
		try
		{
			manifest = JsonSerializer.Deserialize<Manifest>(remoteJson, JsonOpts);
		}
		catch
		{
			return compareResult;
		}
		if (manifest == null)
		{
			return compareResult;
		}
		List<string> list = new List<string>();
		if (!string.Equals(_local.SptVersion, manifest.SptVersion, StringComparison.Ordinal))
		{
			list.Add("SPT 版本不同：本服 " + _local.SptVersion + " / 对方 " + manifest.SptVersion);
		}
		if (!string.Equals(_local.FikaVersion, manifest.FikaVersion, StringComparison.Ordinal))
		{
			list.Add("Fika 版本不同：本服 " + _local.FikaVersion + " / 对方 " + manifest.FikaVersion);
		}
		Dictionary<string, ModEntry> localMods = _local.Mods.ToDictionary<ModEntry, string>((ModEntry m) => m.Id, StringComparer.Ordinal);
		Dictionary<string, ModEntry> remoteMods = manifest.Mods.ToDictionary<ModEntry, string>((ModEntry m) => m.Id, StringComparer.Ordinal);
		foreach (ModEntry item in manifest.Mods.Where((ModEntry m) => !localMods.ContainsKey(m.Id)))
		{
			list.Add($"本服缺少：{item.Name} v{item.Version} ({item.Id})");
		}
		foreach (ModEntry item2 in _local.Mods.Where((ModEntry m) => !remoteMods.ContainsKey(m.Id)))
		{
			list.Add($"本服多出：{item2.Name} v{item2.Version} ({item2.Id})");
		}
		foreach (ModEntry mod in _local.Mods)
		{
			if (remoteMods.TryGetValue(mod.Id, out var value) && !string.Equals(mod.Version, value.Version, StringComparison.Ordinal))
			{
				list.Add($"版本不同：{mod.Name} 本服 v{mod.Version} / 对方 v{value.Version}");
			}
		}
		if (!string.Equals(_local.ItemsHash, manifest.ItemsHash, StringComparison.Ordinal))
		{
			list.Add($"物品数据库不同：本服 {_local.ItemCount} 项 ({_local.ItemsHash}) / 对方 {manifest.ItemCount} 项 ({manifest.ItemsHash})");
		}
		if (list.Count == 0)
		{
			return compareResult;
		}
		StringBuilder stringBuilder = new StringBuilder();
		StringBuilder stringBuilder2 = stringBuilder;
		StringBuilder stringBuilder3 = stringBuilder2;
		StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(20, 1, stringBuilder2);
		handler.AppendLiteral("与 ");
		handler.AppendFormatted(peerName);
		handler.AppendLiteral(" 所在服务器的 MOD 配置不一致：");
		stringBuilder3.AppendLine(ref handler);
		stringBuilder.AppendLine();
		foreach (string item3 in list.Take(100))
		{
			stringBuilder.AppendLine("· " + item3);
		}
		if (list.Count > 100)
		{
			stringBuilder2 = stringBuilder;
			StringBuilder stringBuilder4 = stringBuilder2;
			handler = new StringBuilder.AppendInterpolatedStringHandler(21, 1, stringBuilder2);
			handler.AppendLiteral("· …… 另有 ");
			handler.AppendFormatted(list.Count - 100);
			handler.AppendLiteral(" 项差异（详见服务端日志）");
			stringBuilder4.AppendLine(ref handler);
		}
		stringBuilder.AppendLine();
		stringBuilder.Append("双方服务端安装的内容型 MOD（新物品/武器等）不一致时，跨服战局内可能出现物品丢失、报错甚至崩溃。建议统一双方服务器的 MOD 及版本后再组队。");
		compareResult.Mismatch = true;
		compareResult.Report = stringBuilder.ToString();
		compareResult.Diffs = list;
		return compareResult;
	}
}
