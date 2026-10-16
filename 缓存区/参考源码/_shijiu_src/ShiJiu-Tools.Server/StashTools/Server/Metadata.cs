#define TRACE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SemanticVersioning;

namespace StashTools.Server;

public sealed class Metadata : IModMetadata
{
	public string ModGuid { get; init; }

	public string Name { get; init; }

	public string Author { get; init; }

	public List<string>? Contributors { get; init; }

	public Version Version { get; init; }

	public Range SptVersion { get; init; }

	public bool HasPrepatcher { get; init; }

	public List<string>? Incompatibilities { get; init; }

	public Dictionary<string, Range>? ModDependencies { get; init; }

	public string? Url { get; init; }

	public string License { get; init; }

	public Metadata()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		ModGuid = "local.stashtools.history";
		Name = "【拾玖】多项功能优化";
		Author = "拾玖";
		Version = new Version("1.0.4", false);
		SptVersion = new Range(">=4.1.6 <4.2.0", false);
		License = "MIT";
		base._002Ector();
		try
		{
			StartupLogDisplay.Install();
		}
		catch (Exception ex)
		{
			Trace.WriteLine("ShiJiu-Tools 启动文字精简未启用：" + ex.Message);
		}
	}
}
