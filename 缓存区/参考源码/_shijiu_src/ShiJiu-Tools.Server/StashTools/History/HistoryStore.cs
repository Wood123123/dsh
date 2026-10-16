using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StashTools.History;

public sealed class HistoryStore
{
	private readonly string m_A;

	private readonly object m_A = new object();

	public static readonly JsonSerializerOptions Json = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	public HistoryStore(string root)
	{
		this.m_A = Path.GetFullPath(root);
		Directory.CreateDirectory(this.m_A);
	}

	private static string A(string P_0)
	{
		if (!Regex.IsMatch(P_0 ?? "", "^[a-zA-Z0-9_-]{1,64}$"))
		{
			throw new ArgumentException("记录标识无效");
		}
		return P_0;
	}

	private string a(string P_0)
	{
		return Path.Combine(this.m_A, A(P_0));
	}

	private static void A(string P_0, string P_1)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(P_0));
		string text = P_0 + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			File.WriteAllText(text, P_1);
			File.Move(text, P_0, overwrite: true);
		}
		finally
		{
			if (File.Exists(text))
			{
				File.Delete(text);
			}
		}
	}

	public void Save(string owner, RaidRecord record)
	{
		A(owner);
		A(record.Id);
		if (record.Id.Equals("favorites", StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("保留的记录标识");
		}
		if (record.Profile != owner)
		{
			throw new InvalidOperationException("不能写入其他玩家的记录");
		}
		if (record.Items == null || record.Kills == null || record.Route == null || record.Events == null || record.Tasks == null || record.Achievements == null)
		{
			throw new ArgumentException("记录缺少必要集合");
		}
		if (record.StartedUtc == default(DateTime) || record.EndedUtc < record.StartedUtc)
		{
			throw new ArgumentException("记录时间无效");
		}
		if (!double.IsFinite(record.PausedSeconds) || record.PausedSeconds < 0.0 || record.PausedSeconds > (record.EndedUtc - record.StartedUtc).TotalSeconds + 0.1)
		{
			throw new ArgumentException("暂停时长无效");
		}
		if (record.Schema != 1 || record.Items.Count > 10000 || record.Kills.Count > 2000 || record.Route.Count > 128 || record.Revision < 0)
		{
			throw new ArgumentException("记录规模或版本无效");
		}
		if (!double.IsFinite(record.Income) || !double.IsFinite(record.Expense) || !double.IsFinite(record.Loadout) || record.Income < 0.0 || record.Expense > 0.0)
		{
			throw new ArgumentException("记录金额无效");
		}
		if (record.Items.Any((HistoryItem P_0) => P_0 == null || string.IsNullOrEmpty(P_0.Id) || string.IsNullOrEmpty(P_0.Template) || P_0.Destination == null || P_0.Name == null || P_0.Parent == null || P_0.Slot == null || P_0.X < 0 || P_0.Y < 0 || P_0.X > 1000 || P_0.Y > 1000 || P_0.Count < 0 || P_0.Count > 10000000 || P_0.Width < 1 || P_0.Height < 1 || P_0.Width > 128 || P_0.Height > 128 || !double.IsFinite(P_0.Price) || !double.IsFinite(P_0.Units) || !double.IsFinite(P_0.BroughtUnits) || P_0.BroughtUnits < 0.0 || P_0.Price < 0.0 || P_0.Units < 0.0))
		{
			throw new ArgumentException("物品记录无效");
		}
		if (record.Kills.Any((HistoryKill P_0) => P_0 == null || !double.IsFinite(P_0.Seconds) || !double.IsFinite(P_0.Distance)))
		{
			throw new ArgumentException("击杀记录无效");
		}
		if (record.Items.Select((HistoryItem P_0) => P_0.Id).Distinct().Count() != record.Items.Count)
		{
			throw new ArgumentException("物品标识重复");
		}
		string text = JsonSerializer.Serialize(record, Json);
		if (text.Length > 4000000)
		{
			throw new ArgumentException("记录过大");
		}
		lock (this.m_A)
		{
			A(owner, record);
			string text2 = Path.Combine(a(owner), record.Id + ".json");
			if (File.Exists(text2))
			{
				RaidRecord raidRecord = JsonSerializer.Deserialize<RaidRecord>(File.ReadAllText(text2), Json);
				if (raidRecord.Revision >= record.Revision || (raidRecord.Final && !record.Final))
				{
					return;
				}
				if (raidRecord.StartedUtc != record.StartedUtc)
				{
					throw new ArgumentException("记录起始时间不一致");
				}
				if (raidRecord.Team == null != (record.Team == null) || (raidRecord.Team != null && !A(raidRecord.Team, record.Team)))
				{
					throw new ArgumentException("不能更改记录所属行动");
				}
			}
			A(text2, text);
		}
	}

	private HashSet<string> B(string P_0)
	{
		string path = Path.Combine(a(P_0), "favorites.json");
		if (!File.Exists(path))
		{
			return new HashSet<string>();
		}
		return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path), Json) ?? new HashSet<string>();
	}

	public HistoryReply Favorite(string viewer, HistoryQuery q)
	{
		lock (this.m_A)
		{
			string item = A(q.Profile) + "/" + A(q.Id);
			if (!File.Exists(Path.Combine(a(q.Profile), q.Id + ".json")))
			{
				throw new ArgumentException("记录不存在");
			}
			HashSet<string> hashSet = B(A(viewer));
			if (q.Favorite)
			{
				hashSet.Add(item);
			}
			else
			{
				hashSet.Remove(item);
			}
			A(Path.Combine(a(viewer), "favorites.json"), JsonSerializer.Serialize(hashSet, Json));
			return new HistoryReply
			{
				Ok = true
			};
		}
	}

	public HistoryReply Query(string viewer, HistoryQuery q)
	{
		lock (this.m_A)
		{
			string path = a(q.Profile);
			HashSet<string> favorites = B(A(viewer));
			List<RaidRecord> list = new List<RaidRecord>();
			if (Directory.Exists(path))
			{
				foreach (string item in Directory.EnumerateFiles(path, "*.json"))
				{
					if (Path.GetFileName(item) == "favorites.json")
					{
						continue;
					}
					try
					{
						RaidRecord raidRecord = JsonSerializer.Deserialize<RaidRecord>(File.ReadAllText(item), Json);
						if (raidRecord != null && !(raidRecord.Profile != q.Profile) && raidRecord.Items != null && raidRecord.Kills != null && raidRecord.Route != null && raidRecord.Events != null && raidRecord.Tasks != null && raidRecord.Achievements != null && (string.IsNullOrEmpty(q.Id) || !(raidRecord.Id != A(q.Id))) && (!q.FavoritesOnly || favorites.Contains(q.Profile + "/" + raidRecord.Id)))
						{
							string text = (q.Search ?? "").Trim();
							if (text.Length <= 0 || string.Join(" ", raidRecord.Route.Concat(raidRecord.Items.Select((HistoryItem P_0) => P_0.Name)).Append(raidRecord.StartedUtc.ToString("yyyy-MM-dd")).Append(raidRecord.Status)).Contains(text, StringComparison.OrdinalIgnoreCase))
							{
								list.Add(raidRecord);
							}
						}
					}
					catch (Exception ex) when (((ex is JsonException || ex is IOException || ex is NullReferenceException) ? 1 : 0) != 0)
					{
					}
				}
			}
			int max = Math.Max(1, (list.Count + 19) / 20);
			int num = Math.Clamp(q.Page, 1, max);
			List<RaidRecord> list2 = (from P_0 in list
				orderby P_0.StartedUtc descending, P_0.Id
				select P_0).Skip((num - 1) * 20).Take(20).ToList();
			if (string.IsNullOrEmpty(q.Id))
			{
				foreach (RaidRecord item2 in list2)
				{
					item2.Items.Clear();
					item2.Events.Clear();
					item2.Tasks.Clear();
					item2.Achievements.Clear();
				}
			}
			return new HistoryReply
			{
				Ok = true,
				Total = list.Count,
				SurvivalRate = ((list.Count == 0) ? 0.0 : (100.0 * (double)list.Count((RaidRecord P_0) => P_0.Status == "Survived") / (double)list.Count)),
				KillCount = list.Sum((RaidRecord P_0) => P_0.Kills.Count),
				NetTotal = list.Sum((RaidRecord P_0) => P_0.Income + P_0.Expense),
				Page = num,
				Records = list2,
				Favorites = (from P_0 in list2
					where favorites.Contains(q.Profile + "/" + P_0.Id)
					select P_0.Id).ToList(),
				Team = ((!string.IsNullOrEmpty(q.Id) && list2.Count == 1) ? A(list2[0]) : null)
			};
		}
	}

	private string A(CoopRaidContext P_0)
	{
		return Path.Combine(this.m_A, "_teams", TeamRules.Key(P_0) + ".json");
	}

	private TeamManifest? a(CoopRaidContext P_0)
	{
		string path = A(P_0);
		if (!File.Exists(path))
		{
			return null;
		}
		return JsonSerializer.Deserialize<TeamManifest>(File.ReadAllText(path), Json);
	}

	public void Join(string owner, TeamJoin join)
	{
		Join(owner, join, allowRosterExtension: false);
	}

	public void Join(string owner, TeamJoin join, bool allowRosterExtension)
	{
		A(owner);
		A(join.RecordId);
		TeamRules.Validate(join.Context);
		if (join.RecordId.Equals("favorites", StringComparison.OrdinalIgnoreCase) || join.StartedUtc == default(DateTime) || !join.Context.Members.Any((TeamMemberIdentity P_0) => P_0.Profile == owner))
		{
			throw new ArgumentException("当前玩家不属于该行动成员");
		}
		lock (this.m_A)
		{
			TeamManifest manifest = a(join.Context) ?? new TeamManifest
			{
				Context = TeamRules.Copy(join.Context),
				AllowRosterExtension = allowRosterExtension
			};
			if (!TeamRules.Same(manifest.Context, join.Context))
			{
				if (!allowRosterExtension || !manifest.AllowRosterExtension || manifest.Context.Provider != join.Context.Provider || manifest.Context.ActionId != join.Context.ActionId || !join.Context.Members.Any((TeamMemberIdentity P_0) => manifest.Context.Members.Any((TeamMemberIdentity teamMemberIdentity) => teamMemberIdentity.Profile == P_0.Profile)))
				{
					throw new InvalidOperationException("队伍成员名单不一致，不能关联对局");
				}
				foreach (TeamMemberIdentity member in join.Context.Members)
				{
					if (!manifest.Context.Members.Any((TeamMemberIdentity P_0) => P_0.Profile == member.Profile))
					{
						manifest.Context.Members.Add(new TeamMemberIdentity
						{
							Profile = member.Profile,
							Name = member.Name
						});
					}
				}
				TeamRules.Validate(manifest.Context);
			}
			if (manifest.Registrations.TryGetValue(owner, out TeamRegistration value))
			{
				if (value.RecordId != join.RecordId || value.StartedUtc != join.StartedUtc)
				{
					throw new InvalidOperationException("行动编号已用于另一条记录");
				}
				A(A(join.Context), JsonSerializer.Serialize(manifest, Json));
			}
			else
			{
				manifest.Registrations.Add(owner, new TeamRegistration
				{
					RecordId = join.RecordId,
					StartedUtc = join.StartedUtc
				});
				A(A(join.Context), JsonSerializer.Serialize(manifest, Json));
			}
		}
	}

	private void A(string P_0, RaidRecord P_1)
	{
		if (P_1.Team != null)
		{
			TeamRules.Validate(P_1.Team);
			TeamManifest teamManifest = a(P_1.Team);
			if (teamManifest == null || !A(teamManifest, P_1.Team) || !teamManifest.Registrations.TryGetValue(P_0, out TeamRegistration value) || value.RecordId != P_1.Id || value.StartedUtc != P_1.StartedUtc)
			{
				throw new InvalidOperationException("该玩家尚未登记这次队伍行动");
			}
		}
	}

	private TeamRaidView? A(RaidRecord P_0)
	{
		try
		{
			return a(P_0);
		}
		catch (Exception ex) when (((ex is JsonException || ex is IOException || ex is ArgumentException || ex is InvalidOperationException || ex is NullReferenceException) ? 1 : 0) != 0)
		{
			return null;
		}
	}

	private TeamRaidView? a(RaidRecord P_0)
	{
		if (P_0.Team == null)
		{
			return null;
		}
		TeamManifest teamManifest = a(P_0.Team);
		if (teamManifest == null || teamManifest.Registrations == null || !A(teamManifest, P_0.Team))
		{
			return null;
		}
		TeamRules.Validate(teamManifest.Context);
		TeamRaidView teamRaidView = new TeamRaidView
		{
			Provider = teamManifest.Context.Provider,
			ActionId = teamManifest.Context.ActionId
		};
		foreach (TeamMemberIdentity member in teamManifest.Context.Members)
		{
			RaidRecord raidRecord = null;
			bool flag = teamManifest.Registrations.TryGetValue(member.Profile, out TeamRegistration value);
			if (flag)
			{
				try
				{
					string path = Path.Combine(a(member.Profile), A(value.RecordId) + ".json");
					if (File.Exists(path))
					{
						raidRecord = JsonSerializer.Deserialize<RaidRecord>(File.ReadAllText(path), Json);
					}
					if (raidRecord == null || raidRecord.Profile != member.Profile || raidRecord.Id != value.RecordId || raidRecord.StartedUtc != value.StartedUtc || raidRecord.Team == null || !A(teamManifest, raidRecord.Team) || raidRecord.Kills == null)
					{
						raidRecord = null;
					}
				}
				catch (Exception ex) when (((ex is JsonException || ex is IOException || ex is ArgumentException || ex is NullReferenceException) ? 1 : 0) != 0)
				{
					raidRecord = null;
				}
			}
			teamRaidView.Members.Add(TeamRules.Summary(member.Profile, member.Name, raidRecord, flag));
		}
		return teamRaidView;
	}

	private static bool A(TeamManifest P_0, CoopRaidContext P_1)
	{
		if (!TeamRules.Same(P_0.Context, P_1))
		{
			if (P_0.AllowRosterExtension && P_0.Context.Provider == P_1.Provider && P_0.Context.ActionId == P_1.ActionId)
			{
				return P_1.Members.All((TeamMemberIdentity teamMemberIdentity) => P_0.Context.Members.Any((TeamMemberIdentity teamMemberIdentity2) => teamMemberIdentity2.Profile == teamMemberIdentity.Profile));
			}
			return false;
		}
		return true;
	}

	private bool A(CoopRaidContext P_0, CoopRaidContext P_1)
	{
		if (TeamRules.Same(P_0, P_1))
		{
			return true;
		}
		TeamManifest teamManifest = a(P_1);
		if (teamManifest != null && teamManifest.AllowRosterExtension && P_0.Provider == P_1.Provider && P_0.ActionId == P_1.ActionId && P_0.Members.All((TeamMemberIdentity teamMemberIdentity) => P_1.Members.Any((TeamMemberIdentity teamMemberIdentity2) => teamMemberIdentity2.Profile == teamMemberIdentity.Profile)) && A(teamManifest, P_0))
		{
			return A(teamManifest, P_1);
		}
		return false;
	}
}
