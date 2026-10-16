using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Utils;
using StashTools.History;

namespace StashTools.Server;

[Injectable(/*Could not decode attribute arguments.*/)]
public sealed class HistoryRouter : StaticRouter
{
	private static readonly HistoryStore Store = new HistoryStore(Path.Combine(AppContext.BaseDirectory, "user", "stashtools-history"));

	public HistoryRouter(JsonUtil json, SaveServer saves)
		: base(json, (IEnumerable<RouteAction>)new List<RouteAction>
		{
			(RouteAction)(object)new RouteAction<QueryRequest>("/stashtools/history/query", (Func<string, QueryRequest, MongoId, string, CancellationToken, ValueTask<string>>)delegate(string url, QueryRequest request, MongoId session, string? output, CancellationToken ct)
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				return Handle(saves, session, request, "query");
			}),
			(RouteAction)(object)new RouteAction<QueryRequest>("/stashtools/history/save", (Func<string, QueryRequest, MongoId, string, CancellationToken, ValueTask<string>>)delegate(string url, QueryRequest request, MongoId session, string? output, CancellationToken ct)
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				return Handle(saves, session, request, "save");
			}),
			(RouteAction)(object)new RouteAction<QueryRequest>("/stashtools/history/join", (Func<string, QueryRequest, MongoId, string, CancellationToken, ValueTask<string>>)delegate(string url, QueryRequest request, MongoId session, string? output, CancellationToken ct)
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				return Handle(saves, session, request, "join");
			}),
			(RouteAction)(object)new RouteAction<QueryRequest>("/stashtools/history/favorite", (Func<string, QueryRequest, MongoId, string, CancellationToken, ValueTask<string>>)delegate(string url, QueryRequest request, MongoId session, string? output, CancellationToken ct)
			{
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				return Handle(saves, session, request, "favorite");
			})
		})
	{
	}

	private static ValueTask<string> Handle(SaveServer saves, MongoId session, QueryRequest request, string action)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		HistoryReply value;
		try
		{
			Characters characterData = (saves.GetProfile(session) ?? throw new InvalidOperationException("当前玩家未登录")).CharacterData;
			string text = ((BotBase)(((characterData != null) ? characterData.PmcData : null) ?? throw new InvalidOperationException("玩家档案不可用"))).Id.ToString() ?? throw new InvalidOperationException("玩家档案标识为空");
			if (request.Payload.Length > 4000000)
			{
				throw new ArgumentException("请求过大");
			}
			if (action == "join")
			{
				TeamJoin teamJoin = JsonSerializer.Deserialize<TeamJoin>(request.Payload, HistoryStore.Json) ?? throw new ArgumentException("行动登记为空");
				NormalizeTeam(saves, teamJoin.Context);
				Store.Join(text, teamJoin, teamJoin.Context.Provider.StartsWith("shijiu.fika.", StringComparison.Ordinal));
				value = new HistoryReply
				{
					Ok = true
				};
			}
			else if (action == "save")
			{
				RaidRecord raidRecord = JsonSerializer.Deserialize<RaidRecord>(request.Payload, HistoryStore.Json) ?? throw new ArgumentException("记录为空");
				raidRecord.Profile = text;
				if (raidRecord.Team != null)
				{
					NormalizeTeam(saves, raidRecord.Team);
				}
				Store.Save(text, raidRecord);
				value = new HistoryReply
				{
					Ok = true
				};
			}
			else
			{
				HistoryQuery q = JsonSerializer.Deserialize<HistoryQuery>(request.Payload, HistoryStore.Json) ?? new HistoryQuery();
				if (q.Profile.Length == 0)
				{
					q.Profile = text;
				}
				if (q.Profile.StartsWith("aid:", StringComparison.Ordinal))
				{
					if (!int.TryParse(q.Profile.Substring(4), NumberStyles.None, CultureInfo.InvariantCulture, out var aid))
					{
						throw new ArgumentException("资料页账号编号无效");
					}
					List<SptProfile> list = saves.GetProfiles().Values.Where(delegate(SptProfile p)
					{
						Characters characterData2 = p.CharacterData;
						if (characterData2 == null)
						{
							return false;
						}
						PmcData pmcData = characterData2.PmcData;
						return ((pmcData != null) ? ((BotBase)pmcData).Aid : ((int?)null)) == aid;
					}).ToList();
					if (list.Count != 1)
					{
						throw new InvalidOperationException("无法在当前服务器唯一匹配该玩家账号");
					}
					q.Profile = ((BotBase)list[0].CharacterData.PmcData).Id.ToString() ?? throw new InvalidOperationException("玩家角色编号缺失");
				}
				if (!saves.GetProfiles().Values.Any(delegate(SptProfile p)
				{
					Characters characterData2 = p.CharacterData;
					object obj2;
					if (characterData2 == null)
					{
						obj2 = null;
					}
					else
					{
						PmcData pmcData = characterData2.PmcData;
						obj2 = ((pmcData != null) ? ((BotBase)pmcData).Id.ToString() : null);
					}
					return (string?)obj2 == q.Profile;
				}))
				{
					throw new InvalidOperationException("该玩家暂无服务器档案");
				}
				value = ((action == "favorite") ? Store.Favorite(text, q) : Store.Query(text, q));
			}
		}
		catch (Exception ex)
		{
			HistoryReply obj = new HistoryReply
			{
				Ok = false
			};
			bool flag = ((ex is ArgumentException || ex is InvalidOperationException) ? true : false);
			obj.Error = (flag ? ex.Message : "对局记录暂不可用，请检查服务端日志和磁盘");
			value = obj;
		}
		return ValueTask.FromResult(JsonSerializer.Serialize(value, HistoryStore.Json));
	}

	private static void NormalizeTeam(SaveServer saves, CoopRaidContext context)
	{
		TeamRules.Validate(context);
		List<PmcData> source = (from p in saves.GetProfiles().Values.Select(delegate(SptProfile p)
			{
				Characters characterData = p.CharacterData;
				return (characterData == null) ? null : characterData.PmcData;
			})
			where p != (PmcData)null
			select p).ToList();
		foreach (TeamMemberIdentity member in context.Members)
		{
			List<PmcData> list;
			if (!member.Profile.StartsWith("aid_", StringComparison.Ordinal))
			{
				list = source.Where((PmcData p) => ((BotBase)p).Id.ToString() == member.Profile).ToList();
			}
			else
			{
				if (!int.TryParse(member.Profile.Substring(4), NumberStyles.None, CultureInfo.InvariantCulture, out var aid) || aid <= 0)
				{
					throw new ArgumentException("联机账号编号无效");
				}
				list = source.Where((PmcData p) => ((BotBase)p).Aid == aid).ToList();
			}
			List<PmcData> list2 = list;
			if (list2.Count != 1)
			{
				throw new ArgumentException("队伍名单包含当前服务器无法唯一识别的玩家");
			}
			member.Profile = ((BotBase)list2[0]).Id.ToString() ?? throw new ArgumentException("玩家永久档案标识为空");
			TeamMemberIdentity teamMemberIdentity = member;
			Info info = ((BotBase)list2[0]).Info;
			teamMemberIdentity.Name = ((info != null) ? info.Nickname : null) ?? member.Profile;
		}
		if (context.Provider.StartsWith("shijiu.fika.", StringComparison.Ordinal))
		{
			context.Members = (from g in context.Members.GroupBy<TeamMemberIdentity, string>((TeamMemberIdentity m) => m.Profile, StringComparer.Ordinal)
				select g.First()).ToList();
		}
		TeamRules.Validate(context);
	}
}
