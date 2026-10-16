using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SemanticVersioning;

namespace NekoParty.Server.GroupEX;

public sealed record ModMetadata : IModMetadata
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

	public ModMetadata()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		ModGuid = "com.nekoparty.server.groupex";
		Name = "NekoParty.Server.GroupEX";
		Author = "Neko17";
		Version = new Version("1.0.0", false);
		SptVersion = new Range(">=4.1.3", false);
		ModDependencies = new Dictionary<string, Range>
		{
			{
				"com.nekoparty.server.core",
				new Range(">=1.0.0", false)
			},
			{
				"Fika",
				new Range(">=2.2.1", false)
			}
		};
		License = "MIT";
		base._002Ector();
	}
}
