using System;
using System.Globalization;
using BepInEx.Configuration;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StashTools;

internal static class RaidPanel
{
	private readonly record struct PaintState(float X, float Y, float TitleSize, float DetailSize, string IncomeColor, string CostColor, string NetColor, string NegativeColor, RaidAmountUnit Unit, RaidLedger.Totals Totals, float Width);

	private static ConfigEntry<bool> _enabled;

	private static ConfigEntry<float> _x;

	private static ConfigEntry<float> _y;

	private static ConfigEntry<float> _titleSize;

	private static ConfigEntry<float> _detailSize;

	private static ConfigEntry<string> _incomeColor;

	private static ConfigEntry<string> _costColor;

	private static ConfigEntry<string> _netColor;

	private static ConfigEntry<string> _negativeColor;

	private static ConfigEntry<RaidAmountUnit> _unit;

	private static RectTransform? _root;

	private static RectTransform? _parent;

	private static TextMeshProUGUI? _title;

	private static TextMeshProUGUI? _detail;

	private static PaintState? _painted;

	internal static void Configure(ConfigFile config)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Expected O, but got Unknown
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Expected O, but got Unknown
		_enabled = config.Bind<bool>("05 对局收益", "显示对局收益", true, ChineseSettings.ToggleDescription("仅在对局装备页显示。总收益是当前持有的本局获得物资；本局消耗只计算带入物资的损失，显示负数；净收益＝总收益＋本局消耗。关闭面板仍继续记账，重新开启不会丢失本局记录。"));
		_x = config.Bind<float>("05 对局收益", "水平位置X", 20f, new ConfigDescription("整块收益文字的位置：以背包物品界面左上角为原点，正数向右，负数向左。打开 F12 时也会即时移动，可直接预览。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(-2000f, 2000f), Array.Empty<object>()));
		_y = config.Bind<float>("05 对局收益", "垂直位置Y", 60f, new ConfigDescription("整块收益文字的位置：以背包物品界面左上角为原点，正数向下，负数向上。打开 F12 时也会即时移动，可直接预览。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(-1200f, 1200f), Array.Empty<object>()));
		_titleSize = config.Bind<float>("05 对局收益", "总收益字号", 26f, new ConfigDescription("第一行加粗显示。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(12f, 48f), Array.Empty<object>()));
		_detailSize = config.Bind<float>("05 对局收益", "消耗与净收益字号", 18f, new ConfigDescription("第二行普通字重，两个字段间保留两个空格。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(10f, 36f), Array.Empty<object>()));
		_incomeColor = config.Bind<string>("05 对局收益", "总收益颜色", "#E0E3D9", "第一行文字颜色。 输入颜色代码 #RRGGBB 或 #RRGGBBAA，例如 #FFFFFF 为白色。无效输入暂用默认颜色。");
		_costColor = config.Bind<string>("05 对局收益", "本局消耗颜色", "#B8BDB8", "第二行的消耗颜色，损失以负数显示。 输入颜色代码 #RRGGBB 或 #RRGGBBAA，例如 #FFFFFF 为白色。无效输入暂用默认颜色。");
		_netColor = config.Bind<string>("05 对局收益", "净收益颜色", "#CCD1C4", "净收益为零或正数时使用。 输入颜色代码 #RRGGBB 或 #RRGGBBAA，例如 #FFFFFF 为白色。无效输入暂用默认颜色。");
		_negativeColor = config.Bind<string>("05 对局收益", "净收益为负颜色", "#E08275", "净收益为负数时使用。 输入颜色代码 #RRGGBB 或 #RRGGBBAA，例如 #FFFFFF 为白色。无效输入暂用默认颜色。");
		_unit = config.Bind<RaidAmountUnit>("05 对局收益", "收益金额显示单位", RaidAmountUnit.完整金额, "默认完整金额，例如1,682,962；千位K和百万M始终使用对应单位。仅改变显示，不改变计算。逐件比较跳蚤平均价与可用商人最高收购价，取较高值；禁售或无均价时使用商人价，本局固定报价。相同型号弹药合并后按后合入先取出的顺序追踪来源。");
	}

	private static Color ReadColor(ConfigEntry<string> entry, string fallback)
	{
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		string text = entry.Value.Trim().TrimStart('#');
		if (text.Length == 6)
		{
			text += "FF";
		}
		if (text.Length != 8 || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var _))
		{
			text = fallback + "FF";
		}
		uint num = uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		return new Color((float)(num >> 24) / 255f, (float)((num >> 16) & 0xFF) / 255f, (float)((num >> 8) & 0xFF) / 255f, (float)(num & 0xFF) / 255f);
	}

	internal static void Hide()
	{
		if ((Object)(object)_root != (Object)null && ((Component)_root).gameObject.activeSelf)
		{
			((Component)_root).gameObject.SetActive(false);
		}
	}

	internal static void Dispose()
	{
		if ((Object)(object)_root != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)_root).gameObject);
		}
		_root = null;
		_parent = null;
		_title = null;
		_detail = null;
		_painted = null;
	}

	private static RectTransform Rect(string name, Transform parent)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		RectTransform component = new GameObject(name, new Type[1] { typeof(RectTransform) }).GetComponent<RectTransform>();
		((Transform)component).SetParent(parent, false);
		((Transform)component).localScale = Vector3.one;
		((Transform)component).localRotation = Quaternion.identity;
		Vector2 val = default(Vector2);
		((Vector2)(ref val))._002Ector(0f, 1f);
		component.pivot = val;
		Vector2 anchorMin = (component.anchorMax = val);
		component.anchorMin = anchorMin;
		return component;
	}

	private static TextMeshProUGUI Label(string name, TMP_Text native)
	{
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		TextMeshProUGUI obj = ((Component)Rect(name, (Transform)(object)_root)).gameObject.AddComponent<TextMeshProUGUI>();
		((TMP_Text)obj).font = native.font;
		((TMP_Text)obj).fontSharedMaterial = native.fontSharedMaterial;
		((TMP_Text)obj).spriteAsset = native.spriteAsset;
		((Graphic)obj).raycastTarget = false;
		((TMP_Text)obj).enableAutoSizing = false;
		((TMP_Text)obj).enableWordWrapping = false;
		((TMP_Text)obj).richText = true;
		((TMP_Text)obj).overflowMode = (TextOverflowModes)0;
		((TMP_Text)obj).margin = Vector4.zero;
		((TMP_Text)obj).alignment = (TextAlignmentOptions)257;
		return obj;
	}

	internal static void Tick(RaidLedger.Totals totals)
	{
		//IL_01ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0249: Unknown result type (might be due to invalid IL or missing references)
		//IL_025e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_029d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_030d: Unknown result type (might be due to invalid IL or missing references)
		//IL_03aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0399: Unknown result type (might be due to invalid IL or missing references)
		//IL_03af: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0451: Unknown result type (might be due to invalid IL or missing references)
		//IL_046b: Unknown result type (might be due to invalid IL or missing references)
		//IL_049b: Unknown result type (might be due to invalid IL or missing references)
		//IL_04b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_04ce: Unknown result type (might be due to invalid IL or missing references)
		InventoryScreen screenView = StashBehaviour.ScreenView;
		if (!_enabled.Value || !RaidEarnings.Active || !((Object)(object)screenView != (Object)null) || !((Component)screenView).gameObject.activeInHierarchy || !StashBehaviour.InRaid || !UiValueAccess.GearActive(screenView))
		{
			Hide();
			return;
		}
		EquipmentTab val = screenView._itemsPanel?._equipmentPanel;
		ItemsPanel itemsPanel = screenView._itemsPanel;
		RectTransform val2 = ((itemsPanel != null) ? ((Component)itemsPanel).GetComponent<RectTransform>() : null);
		if ((Object)(object)val2 == (Object)null || !((Component)val2).gameObject.activeInHierarchy)
		{
			Hide();
			return;
		}
		if ((Object)(object)_root == (Object)null || _parent?.Identity() != val2.Identity())
		{
			Dispose();
			TextMeshProUGUI componentInChildren = ((Component)val).GetComponentInChildren<TextMeshProUGUI>();
			if ((Object)(object)((componentInChildren != null) ? ((TMP_Text)componentInChildren).font : null) == (Object)null)
			{
				return;
			}
			_parent = val2;
			_root = Rect("StashToolsRaidEarnings", (Transform)(object)val2);
			((Component)_root).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
			CanvasGroup obj = ((Component)_root).gameObject.AddComponent<CanvasGroup>();
			obj.interactable = false;
			obj.blocksRaycasts = false;
			_title = Label("Income", (TMP_Text)(object)componentInChildren);
			_detail = Label("CostsAndNet", (TMP_Text)(object)componentInChildren);
		}
		if (!((Component)_root).gameObject.activeSelf)
		{
			((Component)_root).gameObject.SetActive(true);
		}
		float value = _x.Value;
		float value2 = _y.Value;
		float value3 = _titleSize.Value;
		float value4 = _detailSize.Value;
		string value5 = _incomeColor.Value;
		string value6 = _costColor.Value;
		string value7 = _netColor.Value;
		string value8 = _negativeColor.Value;
		RaidAmountUnit value9 = _unit.Value;
		Rect rect = val2.rect;
		PaintState value10 = new PaintState(value, value2, value3, value4, value5, value6, value7, value8, value9, totals, ((Rect)(ref rect)).width);
		if (!(_painted == value10))
		{
			_painted = value10;
			_root.anchoredPosition = new Vector2(_x.Value, 0f - _y.Value);
			RectTransform? root = _root;
			rect = val2.rect;
			root.sizeDelta = new Vector2(Math.Max(100f, ((Rect)(ref rect)).width - _x.Value - 12f), _titleSize.Value + _detailSize.Value + 14f);
			((TMP_Text)_title).fontSize = _titleSize.Value;
			((TMP_Text)_title).fontStyle = (FontStyles)1;
			((Graphic)_title).color = ReadColor(_incomeColor, "E0E3D9");
			((TMP_Text)_detail).fontSize = _detailSize.Value;
			((TMP_Text)_detail).fontStyle = (FontStyles)0;
			((Graphic)_detail).color = ReadColor(_costColor, "B8BDB8");
			string text = ((totals.Pending > 0) ? $"  <size=65%>（{totals.Pending} 类估价中）</size>" : "");
			if (totals.Unpriced > 0)
			{
				text += $"  <size=65%>（{totals.Unpriced} 类无报价）</size>";
			}
			string text2 = "总收益：" + amount(totals.Income) + text;
			Color val3 = ((totals.Net < -0.5) ? ReadColor(_negativeColor, "E08275") : ReadColor(_netColor, "CCD1C4"));
			string text3 = "本局消耗：" + amount(totals.Expense) + "  <color=#" + ColorUtility.ToHtmlStringRGBA(val3) + ">净收益：" + amount(totals.Net) + "</color>";
			if (((TMP_Text)_title).text != text2)
			{
				((TMP_Text)_title).text = text2;
			}
			if (((TMP_Text)_detail).text != text3)
			{
				((TMP_Text)_detail).text = text3;
			}
			((TMP_Text)_title).rectTransform.sizeDelta = new Vector2(_root.sizeDelta.x, _titleSize.Value * 1.4f);
			((TMP_Text)_detail).rectTransform.anchoredPosition = new Vector2(0f, 0f - (_titleSize.Value * 1.3f + 2f));
			((TMP_Text)_detail).rectTransform.sizeDelta = new Vector2(_root.sizeDelta.x, _detailSize.Value * 1.4f);
		}
		static string amount(double num)
		{
			return RaidLedger.A(num, _unit.Value);
		}
	}
}
