using System;
using System.Collections.Generic;
using StashTools.History;

namespace StashTools;

public static class HistoryCoopBridge
{
	private static string _provider = "";

	private static Func<string, CoopRaidContext?>? _context;

	private static Func<string, bool?>? _identity;

	private static Func<bool?>? _transit;

	internal static bool? ContinuingTransit
	{
		get
		{
			try
			{
				return _transit?.Invoke();
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogWarning((object)("联机转图状态暂不可用：" + ex.Message));
				return null;
			}
		}
	}

	public static void Register(string provider, Func<string, CoopRaidContext?> context, Func<string, bool?>? victimIdentity)
	{
		Register(provider, context, victimIdentity, null);
	}

	public static void Register(string provider, Func<string, CoopRaidContext?> context, Func<string, bool?>? victimIdentity = null, Func<bool?>? continuingTransit = null)
	{
		if (context == null)
		{
			throw new ArgumentNullException("context");
		}
		TeamRules.Validate(new CoopRaidContext
		{
			Provider = provider,
			ActionId = "validation",
			Members = new List<TeamMemberIdentity>
			{
				new TeamMemberIdentity
				{
					Profile = "validation"
				}
			}
		});
		if (_context != null && _provider != provider)
		{
			throw new InvalidOperationException("另一个联机记录适配器已经注册");
		}
		_provider = provider;
		_context = context;
		_identity = victimIdentity;
		_transit = continuingTransit;
	}

	public static void Unregister(string provider)
	{
		if (_provider == provider)
		{
			_provider = "";
			_context = null;
			_identity = null;
			_transit = null;
		}
	}

	internal static CoopRaidContext? Capture(string characterProfile)
	{
		if (_context == null)
		{
			return null;
		}
		CoopRaidContext coopRaidContext = _context(characterProfile);
		if (coopRaidContext == null)
		{
			return null;
		}
		TeamRules.Validate(coopRaidContext);
		if (coopRaidContext.Provider != _provider)
		{
			throw new InvalidOperationException("联机记录提供者标识不一致");
		}
		return TeamRules.Copy(coopRaidContext);
	}

	internal static bool? VictimIdentity(string victimProfile)
	{
		try
		{
			return _identity?.Invoke(victimProfile);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("击杀身份暂不可用：" + ex.Message));
			return null;
		}
	}
}
