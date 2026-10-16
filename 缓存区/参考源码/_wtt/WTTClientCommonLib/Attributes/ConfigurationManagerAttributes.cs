using System;
using BepInEx.Configuration;

namespace WTTClientCommonLib.Attributes;

internal sealed class ConfigurationManagerAttributes
{
	public delegate void CustomHotkeyDrawerFunc(ConfigEntryBase setting, ref bool isCurrentlyAcceptingInput);

	public bool? Browsable;

	public string Category;

	public Action<ConfigEntryBase> CustomDrawer;

	public CustomHotkeyDrawerFunc CustomHotkeyDrawer;

	public object DefaultValue;

	public string Description;

	public string DispName;

	public bool? HideDefaultButton;

	public bool? HideSettingName;

	public bool? IsAdvanced;

	public Func<object, string> ObjToStr;

	public int? Order;

	public bool? ReadOnly;

	public bool? ShowRangeAsPercent;

	public Func<string, object> StrToObj;
}
