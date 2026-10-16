using System;
using BepInEx.Configuration;
using EFT.UI.SessionEnd;
using TMPro;
using UnityEngine;

namespace StashTools;

internal static class RaidSummary
{
	internal static bool Ready;

	private static ValuePanelOptions _options = null;

	private static readonly ValuePanel Panel = new ValuePanel();

	private static SessionResultExitStatus? _screen;

	internal static void Configure(ConfigFile config)
	{
		_options = new ValuePanelOptions(config, "07 对局结算", "显示结算收益", 40f, 220f, "相对结算页面左上角", hasNet: true);
		Ready = true;
	}

	internal static void Hide()
	{
		Panel.Hide();
		_screen = null;
	}

	internal static void Dispose()
	{
		Panel.Dispose();
	}

	internal static void Open(SessionResultExitStatus screen)
	{
		if (Ready)
		{
			_screen = screen;
			Tick();
		}
	}

	internal static void Tick()
	{
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		if (!Ready)
		{
			return;
		}
		try
		{
			if ((Object)(object)_screen == (Object)null || !((Component)_screen).gameObject.activeInHierarchy || !RaidEarnings.HasSummary || RaidEarnings.AwaitTransit || !_options.Enabled.Value)
			{
				Panel.Hide();
				return;
			}
			RectTransform component = ((Component)_screen).GetComponent<RectTransform>();
			CustomTextMeshProUGUI raidTime = _screen._raidTime;
			if (!((Object)(object)component == (Object)null) && !((Object)(object)raidTime == (Object)null))
			{
				RaidLedger.Totals totals = RaidEarnings.Totals;
				string text = _options.Amount(totals.Net);
				if (totals.Net < -0.5)
				{
					text = "<color=#" + ColorUtility.ToHtmlStringRGBA(ValuePanel.Color(_options.NegativeColor.Value, "#E08275")) + ">" + text + "</color>";
				}
				ValuePanel panel = Panel;
				ValuePanelOptions options = _options;
				string title = "总收益：" + _options.Amount(totals.Income) + ValuePanel.Notice(totals.Pending, totals.Unpriced);
				string detail = "本局消耗：" + _options.Amount(totals.Expense) + "  净收益：" + text;
				Vector2 zero = Vector2.zero;
				Rect rect = component.rect;
				panel.Show(component, (TMP_Text)(object)raidTime, options, title, detail, zero, Math.Min(700f, ((Rect)(ref rect)).width - 60f));
			}
		}
		catch (Exception ex)
		{
			Ready = false;
			Panel.Hide();
			Plugin.Logger.LogError((object)("结算面板已停用，其他功能继续运行：" + ex));
		}
	}
}
