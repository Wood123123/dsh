using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StashTools.History;

public static class TeamRules
{
	public static void Validate(CoopRaidContext context)
	{
		if (context == null || !Regex.IsMatch(context.Provider ?? "", "^[a-zA-Z0-9_.-]{1,64}$") || string.IsNullOrWhiteSpace(context.ActionId) || context.ActionId.Length > 256 || context.ActionId.Any(char.IsControl) || context.Members == null || context.Members.Count < 1 || context.Members.Count > 32)
		{
			throw new ArgumentException("队伍行动信息无效");
		}
		if (context.Members.Any((TeamMemberIdentity P_0) => P_0 == null || !Regex.IsMatch(P_0.Profile ?? "", "^[a-zA-Z0-9_-]{1,64}$") || P_0.Name == null || P_0.Name.Length > 128) || context.Members.Select((TeamMemberIdentity P_0) => P_0.Profile).Distinct().Count() != context.Members.Count)
		{
			throw new ArgumentException("队伍成员信息无效");
		}
	}

	public static string Key(CoopRaidContext context)
	{
		Validate(context);
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(Encoding.UTF8.GetBytes(context.Provider + "\n" + context.ActionId))).Replace("-", "");
	}

	public static bool Same(CoopRaidContext a, CoopRaidContext b)
	{
		if (a.Provider == b.Provider && a.ActionId == b.ActionId)
		{
			return a.Members.Select((TeamMemberIdentity P_0) => P_0.Profile).OrderBy<string, string>((string P_0) => P_0, StringComparer.Ordinal).SequenceEqual(b.Members.Select((TeamMemberIdentity P_0) => P_0.Profile).OrderBy<string, string>((string P_0) => P_0, StringComparer.Ordinal));
		}
		return false;
	}

	public static CoopRaidContext Copy(CoopRaidContext context)
	{
		return new CoopRaidContext
		{
			Provider = context.Provider,
			ActionId = context.ActionId,
			Members = context.Members.Select((TeamMemberIdentity P_0) => new TeamMemberIdentity
			{
				Profile = P_0.Profile,
				Name = P_0.Name
			}).ToList()
		};
	}

	public static TeamMemberSummary Summary(string profile, string name, RaidRecord? record, bool joined = false)
	{
		return new TeamMemberSummary
		{
			Profile = profile,
			Name = ((name.Length > 0) ? name : (record?.Name ?? profile)),
			RecordId = (record?.Id ?? ""),
			HasRecord = (record != null),
			Joined = joined,
			Final = (record?.Final ?? false),
			Reconciled = (record?.Reconciled ?? false),
			Status = (record?.Status ?? ""),
			Income = record?.Income,
			Expense = record?.Expense,
			PmcKills = record?.Kills.Count(delegate(HistoryKill P_0)
			{
				string side = P_0.Side;
				return (side == "Usec" || side == "Bear") ? true : false;
			}),
			ScavKills = record?.Kills.Count((HistoryKill P_0) => P_0.Side == "Savage"),
			OtherKills = record?.Kills.Count(delegate(HistoryKill P_0)
			{
				bool flag;
				switch (P_0.Side)
				{
				case "Usec":
				case "Bear":
				case "Savage":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				return !flag;
			}),
			AiKills = record?.Kills.Count((HistoryKill P_0) => P_0.IsAi == true),
			PlayerKills = record?.Kills.Count((HistoryKill P_0) => P_0.IsAi == false),
			UnknownIdentityKills = record?.Kills.Count((HistoryKill P_0) => !P_0.IsAi.HasValue)
		};
	}
}
