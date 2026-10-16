using System.Collections.Generic;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Web;

namespace Dsh.HeadlessPanel;

/// <summary>
/// SPT mod metadata. Every member is <c>init</c>-only because that is how SPT declares
/// <see cref="IModMetadata"/> / <see cref="IModBlazorMetadata"/>.
///
/// Implementing <see cref="IModBlazorMetadata"/> is what makes SPT pick this mod up as a web
/// page: it scans every loaded mod assembly for that interface, builds a <c>ModPage</c> entry
/// (Name / HomePage / HomePageDescription) and registers the mod's wwwroot folder as a
/// static-file root.
/// </summary>
public sealed class HeadlessPanelMetadata : IModMetadata, IModBlazorMetadata
{
    public string ModGuid { get; init; } = "com.dsh.headlesspanel";

    public string Name { get; init; } = "无头端管理面板";

    public string Author { get; init; } = "DSH";

    public List<string> Contributors { get; init; } = new() { "DSH" };

    public SemanticVersioning.Version Version { get; init; } = new(1, 0, 0);

    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");

    public bool HasPrepatcher { get; init; }

    public List<string> Incompatibilities { get; init; } = new();

    public Dictionary<string, SemanticVersioning.Range> ModDependencies { get; init; } = new();

    public string Url { get; init; } = "";

    public string License { get; init; } = "MIT";

    // ---- IModBlazorMetadata -------------------------------------------------

    /// <summary>Folder served as static files, relative to the mod folder.</summary>
    public string WWWRootUrl { get; init; } = "wwwroot";

    /// <summary>
    /// Page route shown in the SPT web UI / launcher. SPT's NormalizeHomePage prepends a
    /// leading slash when missing, so plain text is fine here. Our controller serves the HTML.
    /// </summary>
    public string HomePage { get; init; } = "headlesspanel";

    public string HomePageDescription { get; init; } = "监控 / 重启 Fika 无头客户端（需 SPT Web 登录）";
}
