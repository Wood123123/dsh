using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Dsh.ManimalAudioGate;

/// <summary>
/// Gates the Manimal mods' sound-occlusion patches to their own maps.
///
/// Both mods ship sound patches that run on every map:
///
///   ManimalInterchangeRework
///     DistanceAudioFadePatch     Postfix on PickClipsByDistance  (volume chosen by distance)
///     OwnedSoundGagPatch         Prefix  on IsChoked             (whether a sound is muffled)
///     AdvancedSoundPolicyPatch   Pre/Postfix on PlaySound
///     EffectSoundPolicyPatch     Transpiler on Emit
///
///   ManimalIcebreaker
///     Patch_SpatialAudioInit     Prefix on SpatialAudioSystem.Initialize
///                                (off-map it still runs TryPrepareSpatialAudio, which builds
///                                 Icebreaker's acoustic rooms/portals into the current scene)
///
/// The result off-map is that other players' interaction sounds come out far too quiet while
/// your own stay normal, because only third-party sounds go through the occlusion/distance
/// maths these patches rewrite.
///
/// This plugin does not touch either DLL. It adds a Harmony prefix to the mods' own patch
/// methods and returns false when the active map is not that mod's map, which makes Harmony
/// skip the patch method. On the owning map everything behaves exactly as before.
/// </summary>
[BepInPlugin(Guid, "Manimal Audio Gate", "1.1.0")]
public sealed class ManimalAudioGatePlugin : BaseUnityPlugin
{
    public const string Guid = "com.dsh.manimalaudiogate";

    internal static ManualLogSource Log;

    private const string InterchangeAssembly = "ManimalInterchange";

    /// <summary>Assembly name confirmed from the DLL metadata (the file is ManimalIcebreakerClient.dll).</summary>
    private const string IcebreakerAssembly = "ManimalIcebreakerClient";

    /// <summary>Interchange-owned sound patches; only meaningful on the Interchange map.</summary>
    private static readonly (string Type, string Method)[] InterchangePatches =
    {
        ("Manimal.Interchange.Client.DistanceAudioFadePatch", "Postfix"),
        ("Manimal.Interchange.Client.OwnedSoundGagPatch", "Prefix"),
        ("Manimal.Interchange.Client.AdvancedSoundPolicyPatch", "Prefix"),
        ("Manimal.Interchange.Client.AdvancedSoundPolicyPatch", "Postfix"),
        ("Manimal.Interchange.Client.EffectSoundPolicyPatch", "Transpiler"),
    };

    /// <summary>Icebreaker-owned acoustics patch; only meaningful on the Icebreaker map.</summary>
    private static readonly (string Type, string Method)[] IcebreakerPatches =
    {
        ("Manimal.Icebreaker.Patch_SpatialAudioInit", "Prefix"),
    };

    private void Awake()
    {
        Log = Logger;

        var harmony = new Harmony(Guid);
        var total = 0;
        var done = 0;

        Gate(harmony, InterchangeAssembly, InterchangePatches, nameof(GateInterchange), "interchange", ref total, ref done);
        Gate(harmony, IcebreakerAssembly, IcebreakerPatches, nameof(GateIcebreaker), "icebreaker", ref total, ref done);

        Log.LogInfo($"[AudioGate] 完成：{done}/{total} 个声音补丁已按地图门控。");
    }

    private static void Gate(
        Harmony harmony,
        string assemblyName,
        (string Type, string Method)[] patches,
        string gateMethodName,
        string owningMap,
        ref int total,
        ref int done)
    {
        total += patches.Length;

        var assembly = ResolveAssembly(assemblyName);
        if (assembly == null)
        {
            Log.LogWarning($"[AudioGate] 找不到程序集 {assemblyName} —— {owningMap} 的门控未启用（游戏照常运行）。");
            return;
        }

        var gate = new HarmonyMethod(typeof(ManimalAudioGatePlugin)
            .GetMethod(gateMethodName, BindingFlags.Public | BindingFlags.Static));

        foreach (var (typeName, methodName) in patches)
        {
            try
            {
                var type = assembly.GetType(typeName, throwOnError: false);
                if (type == null)
                {
                    Log.LogWarning($"[AudioGate] 找不到类型 {typeName}（版本可能变了），跳过。");
                    continue;
                }

                var method = type.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

                if (method == null)
                {
                    Log.LogWarning($"[AudioGate] 找不到方法 {typeName}.{methodName}，跳过。");
                    continue;
                }

                harmony.Patch(method, prefix: gate);
                done++;
                Log.LogInfo($"[AudioGate] 已门控 {typeName}.{methodName}（仅 {owningMap} 生效）");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[AudioGate] 处理 {typeName}.{methodName} 失败：{ex.Message}");
            }
        }
    }

    private static Assembly ResolveAssembly(string name)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));
        if (loaded != null)
        {
            return loaded;
        }

        // Not loaded yet: load it from the plugins tree so we never depend on plugin order.
        try
        {
            var self = Assembly.GetExecutingAssembly().Location;
            var pluginsRoot = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(self));
            if (string.IsNullOrEmpty(pluginsRoot))
            {
                return null;
            }

            // Both mods keep their DLL one folder down, but the folder name does not always match
            // the assembly name, so probe the likely spots.
            foreach (var candidate in new[]
                     {
                         System.IO.Path.Combine(pluginsRoot, name, name + ".dll"),
                         System.IO.Path.Combine(pluginsRoot, "ManimalIcebreaker", "ManimalIcebreakerClient.dll"),
                     })
            {
                if (System.IO.File.Exists(candidate))
                {
                    return Assembly.LoadFrom(candidate);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Log?.LogWarning($"[AudioGate] 加载 {name} 失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>Gate for ManimalInterchange patches: keep them only on Interchange.</summary>
    public static bool GateInterchange() => OnMap("interchange");

    /// <summary>Gate for ManimalIcebreaker patches: keep them only on Icebreaker.</summary>
    public static bool GateIcebreaker() => OnMap("icebreaker");

    /// <summary>
    /// True when the active map matches. Defaults to true whenever we cannot tell, so an
    /// unexpected state never silently disables a mod's behaviour.
    /// </summary>
    private static bool OnMap(string expected)
    {
        try
        {
            var world = Comfort.Common.Singleton<EFT.GameWorld>.Instance;
            if (world == null)
            {
                return true; // Not in a raid yet (menu / loading): leave the mod alone.
            }

            var locationId = world.LocationId;
            if (string.IsNullOrEmpty(locationId))
            {
                return true;
            }

            return string.Equals(locationId, expected, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }
}
