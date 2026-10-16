using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SPTarkov.Server.Core.Services.Locales;
using SemanticVersioning;

namespace StashTools.Server;

internal static class StartupLogDisplay
{
	private const string Owner = "local.stashtools.history";

	private const string PatchId = "local.stashtools.history.startup-display";

	private static readonly object Gate = new object();

	internal static bool Installed { get; private set; }

	internal static void Install()
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		lock (Gate)
		{
			if (Installed)
			{
				return;
			}
			MethodInfo methodInfo = typeof(AbstractLocalisationService<ServerLocalisationService>).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public).Single((MethodInfo m) => m.Name == "GetText" && !m.IsGenericMethod && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[1].ParameterType == typeof(object));
			MethodInfo method = typeof(StartupLogDisplay).GetMethod("FormatArguments", BindingFlags.Static | BindingFlags.NonPublic);
			Harmony val = new Harmony("local.stashtools.history.startup-display");
			try
			{
				val.Patch((MethodBase)methodInfo, new HarmonyMethod(method), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				Installed = true;
			}
			catch
			{
				try
				{
					val.UnpatchSelf();
				}
				catch
				{
				}
				throw;
			}
		}
	}

	private static void FormatArguments(string key, ref object? args)
	{
		if (key != "modloader-loaded_mod" || args == null)
		{
			return;
		}
		try
		{
			Type type = args.GetType();
			string text = type.GetProperty("name")?.GetValue(args)?.ToString();
			string text2 = type.GetProperty("version")?.GetValue(args)?.ToString();
			string text3 = type.GetProperty("author")?.GetValue(args)?.ToString();
			if (text != "【拾玖】多项功能优化" || text3 != "拾玖" || text2 == null)
			{
				return;
			}
			int num = text2.IndexOf(" (GUID: local.stashtools.history | targets SPT: ", StringComparison.Ordinal);
			if (num > 0 && text2.EndsWith(')'))
			{
				string text4 = text2.Substring(0, num);
				Version val = default(Version);
				if (Version.TryParse(text4, ref val))
				{
					args = new
					{
						name = text,
						version = text4,
						author = text3
					};
				}
			}
		}
		catch
		{
		}
	}
}
