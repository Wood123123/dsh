using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;

namespace Dsh.TransitOverlay;

/// <summary>
/// 转移点可视化 / 诊断工具（纯客户端、只读）。
///
/// 热键（BepInEx\config\com.dsh.transitoverlay.cfg 可改）：
///   Keypad1 = 信息面板（表格）
///   Keypad2 = 世界坐标标注（点位置 + 距离）
///   Keypad3 = 导出到文件 + 写日志
///   Keypad4 = 显示转移点的碰撞盒线框（★ 穿墙可见，"那个不可见的盒子"）
///
/// 关键实现说明：
///   ★ 不用 Singleton&lt;EFT.TransitController&gt;.Instance —— 实测经常为 null（子类单例不匹配）。
///     改为直接枚举场景里的 TransitPoint 对象，再从它读 parameters / _controller / 碰撞体。
/// </summary>
[BepInPlugin(Guid, "Transit Overlay", "1.2.0")]
public sealed class TransitOverlayPlugin : BaseUnityPlugin
{
    public const string Guid = "com.dsh.transitoverlay";

    internal static ManualLogSource Log;

    private ConfigEntry<KeyboardShortcut> _keyPanel;
    private ConfigEntry<KeyboardShortcut> _keyMarkers;
    private ConfigEntry<KeyboardShortcut> _keyDump;
    private ConfigEntry<KeyboardShortcut> _keyBoxes;
    private ConfigEntry<float> _markerRange;
    private ConfigEntry<bool> _startPanel;
    private ConfigEntry<bool> _startMarkers;

    // ★ 默认全部关闭 —— 只有按热键才显示，避免每个人一进游戏就看到界面
    private bool _showPanel;
    private bool _showMarkers;
    private bool _showBoxes;

    private GUIStyle _worldStyle, _panelTitle, _rowStyle;
    private Texture2D _panelBg;
    private Vector2 _scroll;

    private Material _lineMat;
    private bool _lineMatTried;

    private readonly List<Row> _rows = new List<Row>();
    private readonly List<Bounds> _boxList = new List<Bounds>();
    private readonly List<Color> _boxColors = new List<Color>();

    private static readonly List<TransitPoint> _ptCache = new List<TransitPoint>();
    private static float _ptCacheAt = -99f;
    private static string _diag = "";

    // ★ 性能：反射结果缓存（不要每帧 AccessTools.Field）
    private static FieldInfo _fParams, _fEnabled, _fIsActive, _fController;
    private static FieldInfo _fId, _fLocation, _fActive, _fWaitSet;
    private static bool _fieldsReady;

    // ★ 性能：数据采集节流 —— 采集只在 Update 里跑，~4Hz；OnGUI 只负责画
    private float _collectAt = -99f;
    private bool _collectOk;

    // ★ 性能：Camera.main 内部是 FindGameObjectWithTag，缓存它
    private static Camera _camCache;
    private static float _camCacheAt = -99f;

    private static void EnsureFields()
    {
        if (_fieldsReady) return;
        _fieldsReady = true;
        try
        {
            var t = typeof(TransitPoint);
            _fParams = AccessTools.Field(t, "parameters");
            _fEnabled = AccessTools.Field(t, "_enabled");
            _fIsActive = AccessTools.Field(t, "_isActive");
            _fController = AccessTools.Field(t, "_controller");

            var parsType = _fParams?.FieldType;
            if (parsType != null)
            {
                _fId = AccessTools.Field(parsType, "id");
                _fLocation = AccessTools.Field(parsType, "location");
                _fActive = AccessTools.Field(parsType, "active");
            }
        }
        catch { }
    }

    private static Camera CachedCamera()
    {
        if (_camCache != null && Time.realtimeSinceStartup - _camCacheAt < 1f) return _camCache;
        _camCacheAt = Time.realtimeSinceStartup;
        _camCache = GetCamera();
        return _camCache;
    }

    private struct Row
    {
        public int Id;
        public string Loc, Scene;
        public Vector3 Pos;
        public float Dist;
        public bool Go, Waiting;
        public string Active, Enabled, IsActive;
        public bool OnScreen;
        public Vector3 Screen;
        // 碰撞体
        public int ColCount;
        public string ColInfo;
        public bool AnyTrigger, AnyEnabled;
    }

    private void Awake()
    {
        Log = Logger;

        _keyPanel = Config.Bind("Keys", "TogglePanel", new KeyboardShortcut(KeyCode.Keypad1), "开/关信息面板");
        _keyMarkers = Config.Bind("Keys", "ToggleMarkers", new KeyboardShortcut(KeyCode.Keypad2), "开/关世界坐标标注");
        _keyDump = Config.Bind("Keys", "DumpToFile", new KeyboardShortcut(KeyCode.Keypad3), "导出到文件并写日志");
        _keyBoxes = Config.Bind("Keys", "ToggleTriggerBoxes", new KeyboardShortcut(KeyCode.Keypad4), "开/关碰撞盒线框（穿墙可见）");
        _markerRange = Config.Bind("Display", "MarkerRange", 3000f,
            new ConfigDescription("超过这个距离不画世界标注（米）", new AcceptableValueRange<float>(50f, 20000f)));
        _startPanel = Config.Bind("Display", "PanelVisibleAtStart", false,
            "进游戏时是否默认显示面板（★ 默认 false —— 不开就不会显示任何界面）");
        _startMarkers = Config.Bind("Display", "MarkersVisibleAtStart", false,
            "进游戏时是否默认显示世界标注（★ 默认 false）");

        // ★ 默认全部关闭：不进游戏就给所有人弹界面
        _showPanel = _startPanel.Value;
        _showMarkers = _startMarkers.Value;
        _showBoxes = false;

        Log.LogInfo($"[TransitOverlay] 已加载（默认关闭）。Keypad1=面板 Keypad2=标注 Keypad3=导出 Keypad4=碰撞盒" +
                    $"  当前: 面板={_showPanel} 标注={_showMarkers}");
    }

    private void Update()
    {
        try
        {
            if (_keyPanel.Value.IsDown()) { _showPanel = !_showPanel; Log.LogWarning($"[TransitOverlay] 面板 {(_showPanel ? "开" : "关")}"); }
            if (_keyMarkers.Value.IsDown()) { _showMarkers = !_showMarkers; Log.LogWarning($"[TransitOverlay] 世界标注 {(_showMarkers ? "开" : "关")}"); }
            if (_keyBoxes.Value.IsDown()) { _showBoxes = !_showBoxes; Log.LogWarning($"[TransitOverlay] 碰撞盒线框 {(_showBoxes ? "开" : "关")}"); }
            if (_keyDump.Value.IsDown()) Dump();

            // ★ 数据采集放在这里（每帧最多一次，且内部再按 0.25s 节流）
            if (_showPanel || _showMarkers || _showBoxes) RefreshData(false);
        }
        catch { }
    }

    /// <summary>
    /// 节流后的数据采集。OnGUI / OnRenderObject 只读缓存，绝不再自己采集。
    /// </summary>
    private void RefreshData(bool force)
    {
        if (!force && Time.realtimeSinceStartup - _collectAt < 0.25f) return;
        _collectAt = Time.realtimeSinceStartup;
        try { _collectOk = Collect(); }
        catch (Exception e)
        {
            if (Time.frameCount % 300 == 0) Log.LogError("[TransitOverlay] 采集异常: " + e.Message);
            _collectOk = false;
        }
    }

    // ─────────────────────────────────────────────
    // 数据采集
    // ─────────────────────────────────────────────

    /// <summary>每 2 秒刷新一次场景里的 TransitPoint 列表（FindObjectsOfTypeAll 不便宜）。</summary>
    private static void RefreshPointCache()
    {
        if (Time.realtimeSinceStartup - _ptCacheAt < 5f && _ptCache.Count > 0) return;
        _ptCacheAt = Time.realtimeSinceStartup;
        _ptCache.Clear();
        try
        {
            var all = Resources.FindObjectsOfTypeAll<TransitPoint>();
            if (all == null) { _diag = "FindObjectsOfTypeAll 返回 null"; return; }
            foreach (var p in all)
            {
                if (p == null) continue;
                try
                {
                    var sc = p.gameObject.scene;
                    if (!sc.IsValid() || string.IsNullOrEmpty(sc.name)) continue;   // 过滤未实例化的预制体
                }
                catch { continue; }
                _ptCache.Add(p);
            }
            _diag = $"场景里找到 {_ptCache.Count} 个 TransitPoint";
        }
        catch (Exception e) { _diag = "枚举异常: " + e.Message; }
    }

    private static Player GetMe()
    {
        try { return GamePlayerOwner.MyPlayer; } catch { return null; }
    }

    private static Camera GetCamera()
    {
        try { var c = Camera.main; if (c != null) return c; } catch { }
        try { var a = Camera.allCameras; if (a != null && a.Length > 0) return a[0]; } catch { }
        return null;
    }

    /// <summary>采集所有转移点。返回 false 表示现在不在战局里。</summary>
    private bool Collect()
    {
        _rows.Clear();
        _boxList.Clear();
        _boxColors.Clear();

        Player me = GetMe();
        if (me == null) return false;

        EnsureFields();
        RefreshPointCache();
        if (_ptCache.Count == 0) return false;

        Vector3 myPos;
        try { myPos = me.Position; } catch { return false; }

        Camera cam = CachedCamera();

        // 等待可见集合（能从 _controller 拿到就顺便读，只取一次）
        IEnumerable waitSet = null;
        try
        {
            if (_fController != null && _fWaitSet == null)
            {
                var ctrl0 = _fController.GetValue(_ptCache[0]);
                if (ctrl0 != null)
                {
                    var wf = AccessTools.Field(ctrl0.GetType(), "_waitForVisibleTransitPoints");
                    if (wf != null) _fWaitSet = wf;
                }
            }
            if (_fController != null && _fWaitSet != null)
            {
                var ctrl0 = _fController.GetValue(_ptCache[0]);
                if (ctrl0 != null) waitSet = _fWaitSet.GetValue(ctrl0) as IEnumerable;
            }
        }
        catch { }

        foreach (var pt in _ptCache)
        {
            if (pt == null) continue;
            var row = new Row { Id = -1, Loc = "?", Scene = "?", Active = "?", Enabled = "?", IsActive = "?" };

            try
            {
                object pars = _fParams?.GetValue(pt);
                if (pars != null)
                {
                    object idv = _fId?.GetValue(pars);
                    if (idv != null) row.Id = Convert.ToInt32(idv);
                    row.Loc = _fLocation?.GetValue(pars)?.ToString() ?? "?";
                    row.Active = _fActive?.GetValue(pars)?.ToString() ?? "?";
                }
            }
            catch { }

            try { row.Enabled = _fEnabled?.GetValue(pt)?.ToString() ?? "?"; } catch { }
            try { row.IsActive = _fIsActive?.GetValue(pt)?.ToString() ?? "?"; } catch { }
            try { row.Go = pt.gameObject.activeSelf; } catch { }
            try { row.Pos = pt.transform.position; } catch { }
            try { row.Scene = pt.gameObject.scene.name; } catch { }
            try { row.Dist = Vector3.Distance(myPos, row.Pos); } catch { row.Dist = -1f; }

            if (waitSet != null)
            {
                try { foreach (var x in waitSet) { if (ReferenceEquals(x, pt)) { row.Waiting = true; break; } } } catch { }
            }

            // ★ 碰撞体（这就是"不可见的盒子"）—— 只在采集时读，不在绘制时读
            try
            {
                var cols = pt.GetComponentsInChildren<Collider>(true);
                row.ColCount = cols?.Length ?? 0;
                var sb = new List<string>();
                foreach (var c in cols)
                {
                    if (c == null) continue;
                    if (c.isTrigger) row.AnyTrigger = true;
                    if (c.enabled) row.AnyEnabled = true;
                    var b = c.bounds;
                    sb.Add($"{c.GetType().Name}(trig={c.isTrigger},en={c.enabled},size={b.size.x:F1}x{b.size.y:F1}x{b.size.z:F1},ctr=({b.center.x:F0},{b.center.y:F0},{b.center.z:F0}))");
                    _boxList.Add(b);
                    _boxColors.Add(c.isTrigger
                        ? (c.enabled ? new Color(1f, 0.9f, 0.1f, 0.95f) : new Color(1f, 0.2f, 0.2f, 0.95f))
                        : new Color(0.2f, 0.7f, 1f, 0.95f));
                }
                row.ColInfo = sb.Count > 0 ? string.Join(" | ", sb) : "(无碰撞体)";
            }
            catch (Exception e) { row.ColInfo = "碰撞体读取异常:" + e.Message; }

            // 屏幕投影（给 OnGUI 用）
            if (cam != null)
            {
                try
                {
                    Vector3 sp = cam.WorldToScreenPoint(row.Pos);
                    row.Screen = sp;
                    row.OnScreen = sp.z > 0f && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height;
                }
                catch { }
            }

            _rows.Add(row);
        }

        _rows.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        return true;
    }

    // ─────────────────────────────────────────────
    // 绘制
    // ─────────────────────────────────────────────
    private void EnsureStyles()
    {
        if (_worldStyle != null) return;
        _panelBg = new Texture2D(1, 1);
        _panelBg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.80f));
        _panelBg.Apply();

        _worldStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, richText = true };
        _worldStyle.normal.textColor = Color.white;
        _panelTitle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        _panelTitle.normal.textColor = Color.yellow;
        _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        _rowStyle.normal.textColor = Color.white;
    }

    private static string ShortLoc(string loc) => loc switch
    {
        "TarkovStreets" => "街区",
        "Interchange" => "立交桥",
        "laboratory" => "实验室",
        "Sandbox_high" => "中心区(高)",
        "Sandbox" => "中心区",
        "RezervBase" => "储备站",
        "factory4_day" => "工厂(日)",
        "factory4_night" => "工厂(夜)",
        "Shoreline" => "海岸线",
        "Woods" => "森林",
        "bigmap" => "海关",
        "Lighthouse" => "灯塔",
        _ => loc
    };

    private void OnGUI()
    {
        try
        {
            // ★ 不在这里采集 —— 只读 Update 里刷新的缓存
            EnsureStyles();
            if (_collectOk && _showMarkers) DrawWorldMarkers();
            if (_showPanel) DrawPanel(_collectOk);
        }
        catch (Exception e)
        {
            if (Time.frameCount % 300 == 0) Log.LogError("[TransitOverlay] OnGUI 异常: " + e.Message);
        }
    }

    private void DrawWorldMarkers()
    {
        Player me = GetMe();
        Camera cam = GetCamera();
        if (me != null && cam != null)
        {
            try
            {
                var pos = me.Position;
                Vector3 sp = cam.WorldToScreenPoint(pos);
                if (sp.z > 0f)
                    GUI.Label(new Rect(sp.x - 90f, Screen.height - sp.y + 12f, 180f, 20f),
                        $"<b>▲ 我 ({pos.x:F0},{pos.y:F0},{pos.z:F0})</b>", _worldStyle);
            }
            catch { }
        }

        foreach (var row in _rows)
        {
            if (row.Dist < 0f || row.Dist > _markerRange.Value || !row.OnScreen) continue;

            var rect = new Rect(row.Screen.x - 80f, Screen.height - row.Screen.y - 28f, 160f, 42f);
            GUI.DrawTexture(rect, _panelBg);

            var st = new GUIStyle(_worldStyle);
            st.normal.textColor = row.Waiting ? new Color(1f, 0.35f, 0.35f)
                               : !row.Go ? new Color(1f, 0.6f, 0f)
                               : row.AnyTrigger && !row.AnyEnabled ? new Color(1f, 0.35f, 0.35f)
                               : new Color(0.4f, 1f, 0.4f);
            GUI.Label(rect, $"<b>#{row.Id} {ShortLoc(row.Loc)}</b>\n{row.Dist:F0}m  trig={row.AnyTrigger} en={row.AnyEnabled}", st);
        }
    }

    private void DrawPanel(bool inRaid)
    {
        Player me = GetMe();
        Vector3 myPos = Vector3.zero;
        string mapId = "?";
        try { if (me != null) myPos = me.Position; } catch { }
        try { mapId = Singleton<GameWorld>.Instance?.LocationId ?? "?"; } catch { }

        var win = new Rect(20f, 20f, 1180f, 500f);
        GUI.DrawTexture(win, _panelBg);
        GUILayout.BeginArea(new Rect(win.x + 10f, win.y + 8f, win.width - 20f, win.height - 16f));

        GUILayout.Label($"转移点面板  [{_keyPanel.Value}]面板 [{_keyMarkers.Value}]标注 [{_keyBoxes.Value}]碰撞盒 [{_keyDump.Value}]导出", _panelTitle);

        if (!inRaid)
        {
            GUILayout.Label("★ 当前不在战局里（找不到 MyPlayer 或场景里没有 TransitPoint）", _rowStyle);
            GUILayout.Label($"   地图={mapId}  玩家={(me == null ? "NULL" : "有")}  枚举={_diag}", _rowStyle);
            GUILayout.Label("   进图后这里会列出所有转移点。", _rowStyle);
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"地图={mapId}({ShortLoc(mapId)})  玩家=({myPos.x:F1},{myPos.y:F1},{myPos.z:F1})  共 {_rows.Count} 个点  枚举={_diag}", _rowStyle);
        GUILayout.Label("id  目的地      距离    active _enabled _isActive GO   等待 碰撞体(trig/en/尺寸)                     坐标             场景", _rowStyle);

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(win.height - 96f));
        foreach (var row in _rows)
        {
            string col = row.ColCount == 0 ? "无" : $"{(row.AnyTrigger ? "T" : "-")}/{(row.AnyEnabled ? "E" : "-")}x{row.ColCount}";
            string line = $"{row.Id,-3} {ShortLoc(row.Loc),-11} {row.Dist,7:F1}m {row.Active,-6} {row.Enabled,-8} {row.IsActive,-9} {row.Go,-5} {(row.Waiting ? "★是" : "否"),-4} {col,-8} " +
                          $"({row.Pos.x:F0},{row.Pos.y:F0},{row.Pos.z:F0}),".PadRight(18) + row.Scene;
            var st = new GUIStyle(_rowStyle);
            if (row.Waiting) st.normal.textColor = new Color(1f, 0.4f, 0.4f);
            else if (!row.Go) st.normal.textColor = new Color(1f, 0.6f, 0f);
            else if (row.AnyTrigger && !row.AnyEnabled) st.normal.textColor = new Color(1f, 0.4f, 0.4f);
            GUILayout.Label(line, st);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ─────────────────────────────────────────────
    // ★ 碰撞盒线框（穿墙可见）
    // ─────────────────────────────────────────────
    private void EnsureLineMat()
    {
        if (_lineMat != null || _lineMatTried) return;
        _lineMatTried = true;
        try
        {
            var sh = Shader.Find("Hidden/Internal-Colored");
            if (sh == null) { Log.LogWarning("[TransitOverlay] 找不到 Hidden/Internal-Colored，碰撞盒无法绘制"); return; }
            _lineMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMat.SetInt("_ZWrite", 0);
            // ★ ZTest=Always → 穿墙可见（找埋在墙里/地板下的盒子必须这样）
            _lineMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }
        catch (Exception e) { Log.LogWarning("[TransitOverlay] 创建线材质失败: " + e.Message); }
    }

    private void OnRenderObject()
    {
        if (!_showBoxes || _boxList.Count == 0) return;
        try
        {
            EnsureLineMat();
            if (_lineMat == null) return;
            if (!_lineMat.SetPass(0)) return;

            GL.PushMatrix();
            GL.Begin(GL.LINES);
            for (int i = 0; i < _boxList.Count; i++)
            {
                GL.Color(_boxColors[i]);
                DrawBoxEdges(_boxList[i]);
            }
            GL.End();
            GL.PopMatrix();
        }
        catch { }
    }

    private static void DrawBoxEdges(Bounds b)
    {
        Vector3 c = b.center, e = b.extents;
        Vector3[] v =
        {
            c + new Vector3(-e.x, -e.y, -e.z), c + new Vector3( e.x, -e.y, -e.z),
            c + new Vector3( e.x, -e.y,  e.z), c + new Vector3(-e.x, -e.y,  e.z),
            c + new Vector3(-e.x,  e.y, -e.z), c + new Vector3( e.x,  e.y, -e.z),
            c + new Vector3( e.x,  e.y,  e.z), c + new Vector3(-e.x,  e.y,  e.z)
        };
        int[,] pairs = { {0,1},{1,2},{2,3},{3,0}, {4,5},{5,6},{6,7},{7,4}, {0,4},{1,5},{2,6},{3,7} };
        for (int i = 0; i < pairs.GetLength(0); i++)
        {
            GL.Vertex(v[pairs[i, 0]]);
            GL.Vertex(v[pairs[i, 1]]);
        }
    }

    // ─────────────────────────────────────────────
    // 导出
    // ─────────────────────────────────────────────
    private void Dump()
    {
        try
        {
            RefreshData(true);
            if (!_collectOk)
            {
                Log.LogWarning($"[TransitOverlay] 导出失败：不在战局里，或场景无 TransitPoint。枚举={_diag}");
                return;
            }

            Player me = GetMe();
            Vector3 myPos = Vector3.zero;
            try { if (me != null) myPos = me.Position; } catch { }
            string mapId = "?";
            try { mapId = Singleton<GameWorld>.Instance?.LocationId ?? "?"; } catch { }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# TransitOverlay dump {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"# map={mapId} player=({myPos.x:F1},{myPos.y:F1},{myPos.z:F1}) points={_rows.Count} {_diag}");
            sb.AppendLine("id\tdest\tdist_m\tactive\t_enabled\t_isActive\tGO\twaiting\ttrigger\tenabled\tcolCount\tcols\tpos\tscene");
            foreach (var r in _rows)
                sb.AppendLine($"{r.Id}\t{r.Loc}\t{r.Dist:F1}\t{r.Active}\t{r.Enabled}\t{r.IsActive}\t{r.Go}\t{r.Waiting}\t{r.AnyTrigger}\t{r.AnyEnabled}\t{r.ColCount}\t{r.ColInfo}\t({r.Pos.x:F1},{r.Pos.y:F1},{r.Pos.z:F1})\t{r.Scene}");

            string dir = Path.GetDirectoryName(Info.Location) ?? ".";
            string file = Path.Combine(dir, $"transit_dump_{DateTime.Now:MMdd_HHmmss}.txt");
            File.WriteAllText(file, sb.ToString());

            Log.LogWarning($"[TransitOverlay] ===== 导出 map={mapId} player=({myPos.x:F1},{myPos.y:F1},{myPos.z:F1}) 共 {_rows.Count} 点 =====");
            foreach (var r in _rows)
            {
                Log.LogWarning($"[TransitOverlay] id={r.Id} →{r.Loc} 距离={r.Dist:F1}m active={r.Active} _enabled={r.Enabled} _isActive={r.IsActive} " +
                               $"GO={r.Go} 等待={r.Waiting} 碰撞体={r.ColCount}(trig={r.AnyTrigger},en={r.AnyEnabled}) pos=({r.Pos.x:F0},{r.Pos.y:F0},{r.Pos.z:F0}) 场景={r.Scene}");
                if (!string.IsNullOrEmpty(r.ColInfo)) Log.LogWarning($"[TransitOverlay]      {r.ColInfo}");
            }
            Log.LogWarning($"[TransitOverlay] ===== 文件: {file} =====");
        }
        catch (Exception e) { Log.LogError("[TransitOverlay] 导出异常: " + e); }
    }
}
