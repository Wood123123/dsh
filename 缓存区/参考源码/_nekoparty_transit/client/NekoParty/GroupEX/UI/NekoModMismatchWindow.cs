using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Comfort.Common;
using EFT.UI;
using NekoParty.GroupEX.Notifications;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NekoParty.GroupEX.UI;

public static class NekoModMismatchWindow
{
	private const float WindowWidth = 900f;

	private const float WindowHeight = 660f;

	private const float BodyFontSize = 19f;

	private const float BodyMaxHeight = 440f;

	private const float BodyMinHeight = 180f;

	private const int DetailIndent = 190;

	private const string ScrollRootName = "NekoReportScroll";

	private const string ColorDim = "#8F8E7E";

	private const string ColorMissing = "#D96A6A";

	private const string ColorExtra = "#D9A05B";

	private const string ColorVersion = "#D9C468";

	private const string ColorCore = "#E0685B";

	private const string ColorItems = "#6FA8DC";

	private const string ColorNickname = "#51C878";

	private const string SectionMark = "#FFFFFF14";

	private static readonly FieldInfo ContainerField = typeof(PreloaderUI).GetField("_criticalErrorScreenContainer", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo DescriptionField = typeof(ErrorScreen).GetField("_errorDescription", BindingFlags.Instance | BindingFlags.NonPublic);

	public static void Show(NekoModMismatchNotification notification)
	{
		if (notification != null && Singleton<PreloaderUI>.Instantiated)
		{
			PreloaderUI instance = Singleton<PreloaderUI>.Instance;
			string text = (notification.Blocked ? "MOD 校验未通过" : "MOD 兼容性警告");
			string text2 = (string.IsNullOrEmpty(notification.Report) ? ("与 " + notification.PeerNickname + " 所在服务器的 MOD 配置不一致。") : notification.Report);
			instance.ShowCriticalErrorScreen(text, text2, (EButtonType)0, 0f);
			TryRestyle(instance, BuildStyledReport(notification, text2), notification.Blocked);
		}
	}

	private static void TryRestyle(PreloaderUI preloader, string styledReport, bool blocked)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Expected O, but got Unknown
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Expected O, but got Unknown
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Expected O, but got Unknown
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Expected O, but got Unknown
		try
		{
			ErrorScreen val = FindActiveCriticalScreen(preloader);
			if ((Object)val == (Object)null)
			{
				return;
			}
			RectTransform windowTransform = ((Window<ErrorWindowContext>)(object)val).WindowTransform;
			if ((Object)windowTransform != (Object)null)
			{
				windowTransform.sizeDelta = new Vector2(Mathf.Max(windowTransform.sizeDelta.x, 900f), Mathf.Max(windowTransform.sizeDelta.y, 660f));
			}
			TextMeshProUGUI caption = ((Window<ErrorWindowContext>)(object)val).Caption;
			if ((Object)caption != (Object)null)
			{
				((Graphic)caption).color = Color32.op_Implicit(blocked ? new Color32((byte)217, (byte)106, (byte)106, byte.MaxValue) : new Color32((byte)217, (byte)196, (byte)104, byte.MaxValue));
			}
			object obj = DescriptionField?.GetValue(val);
			TextMeshProUGUI val2 = (TextMeshProUGUI)((obj is TextMeshProUGUI) ? obj : null);
			if ((Object)(object)val2 != (Object)null)
			{
				ApplyTextStyle(val2);
				ScrollRect val3 = TrySetupScroll(val2, windowTransform);
				if ((Object)val3 == (Object)null)
				{
					RestyleWithoutScroll(val2);
				}
				((TMP_Text)val2).text = styledReport;
				if ((Object)val3 != (Object)null)
				{
					FitBodyHeightToContent(val3, val2, windowTransform);
					val3.verticalNormalizedPosition = 1f;
				}
				else
				{
					LayoutRebuilder.ForceRebuildLayoutImmediate(((TMP_Text)val2).rectTransform);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static void ApplyTextStyle(TextMeshProUGUI description)
	{
		((TMP_Text)description).richText = true;
		((TMP_Text)description).alignment = (TextAlignmentOptions)257;
		((TMP_Text)description).lineSpacing = 8f;
		((TMP_Text)description).paragraphSpacing = 12f;
		((TMP_Text)description).enableAutoSizing = false;
		((TMP_Text)description).fontSize = 19f;
		((Graphic)description).raycastTarget = true;
	}

	private static void RestyleWithoutScroll(TextMeshProUGUI description)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		RectTransform rectTransform = ((TMP_Text)description).rectTransform;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = new Vector2(46f, 112f);
		rectTransform.offsetMax = new Vector2(-46f, -88f);
		((TMP_Text)description).enableAutoSizing = true;
		((TMP_Text)description).fontSizeMax = 20f;
		((TMP_Text)description).fontSizeMin = 13f;
	}

	private static ScrollRect TrySetupScroll(TextMeshProUGUI description, RectTransform window)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Expected O, but got Unknown
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Expected O, but got Unknown
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Expected O, but got Unknown
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Unknown result type (might be due to invalid IL or missing references)
		//IL_0142: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Expected O, but got Unknown
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Expected O, but got Unknown
		//IL_0151: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Expected O, but got Unknown
		RectTransform rectTransform = ((TMP_Text)description).rectTransform;
		ScrollRect componentInParent = ((Component)rectTransform).GetComponentInParent<ScrollRect>();
		if ((Object)componentInParent != (Object)null && ((Object)((Component)componentInParent).gameObject).name == "NekoReportScroll")
		{
			return componentInParent;
		}
		Transform parent = ((Transform)rectTransform).parent;
		RectTransform val = (RectTransform)((parent is RectTransform) ? parent : null);
		if ((Object)val == (Object)null)
		{
			return null;
		}
		int siblingIndex = ((Transform)rectTransform).GetSiblingIndex();
		Vector2 anchorMin = rectTransform.anchorMin;
		Vector2 anchorMax = rectTransform.anchorMax;
		Vector2 pivot = rectTransform.pivot;
		Vector2 anchoredPosition = rectTransform.anchoredPosition;
		Vector2 sizeDelta = rectTransform.sizeDelta;
		ScrollRect val2;
		ContentSizeFitter addedFitter;
		try
		{
			val2 = BuildScrollHierarchy(description, val, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta, siblingIndex, out addedFitter);
		}
		catch (Exception)
		{
			return null;
		}
		if ((Object)window != (Object)null)
		{
			LayoutRebuilder.ForceRebuildLayoutImmediate(window);
		}
		RectTransform val3 = (RectTransform)((Component)val2).transform;
		Rect rect = val3.rect;
		if (!(((Rect)(ref rect)).height < 120f))
		{
			rect = val3.rect;
			if (!(((Rect)(ref rect)).width < 200f))
			{
				return val2;
			}
		}
		((Transform)rectTransform).SetParent((Transform)val, false);
		rectTransform.anchorMin = anchorMin;
		rectTransform.anchorMax = anchorMax;
		rectTransform.pivot = pivot;
		rectTransform.anchoredPosition = anchoredPosition;
		rectTransform.sizeDelta = sizeDelta;
		((Transform)rectTransform).SetSiblingIndex(siblingIndex);
		if ((Object)addedFitter != (Object)null)
		{
			Object.DestroyImmediate((Object)addedFitter);
		}
		Object.DestroyImmediate((Object)((Component)val2).gameObject);
		return null;
	}

	private static ScrollRect BuildScrollHierarchy(TextMeshProUGUI description, RectTransform host, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta, int siblingIndex, out ContentSizeFitter addedFitter)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected O, but got Unknown
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Expected O, but got Unknown
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Expected O, but got Unknown
		//IL_0145: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_016f: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_01aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Expected O, but got Unknown
		RectTransform rectTransform = ((TMP_Text)description).rectTransform;
		GameObject val = new GameObject("NekoReportScroll", new Type[1] { typeof(RectTransform) });
		RectTransform val2 = (RectTransform)val.transform;
		((Transform)val2).SetParent((Transform)host, false);
		val2.anchorMin = anchorMin;
		val2.anchorMax = anchorMax;
		val2.pivot = pivot;
		val2.anchoredPosition = anchoredPosition;
		val2.sizeDelta = sizeDelta;
		LayoutElement obj = val.AddComponent<LayoutElement>();
		obj.minHeight = 180f;
		obj.preferredHeight = 440f;
		obj.flexibleWidth = 1f;
		Image obj2 = val.AddComponent<Image>();
		((Graphic)obj2).color = new Color(0f, 0f, 0f, 0.004f);
		((Graphic)obj2).raycastTarget = true;
		RectTransform val3 = (RectTransform)new GameObject("Viewport", new Type[2]
		{
			typeof(RectTransform),
			typeof(RectMask2D)
		}).transform;
		((Transform)val3).SetParent((Transform)val2, false);
		val3.anchorMin = Vector2.zero;
		val3.anchorMax = Vector2.one;
		val3.offsetMin = Vector2.zero;
		val3.offsetMax = new Vector2(-14f, 0f);
		((Transform)rectTransform).SetParent((Transform)val3, false);
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(1f, 1f);
		rectTransform.pivot = new Vector2(0.5f, 1f);
		rectTransform.anchoredPosition = Vector2.zero;
		rectTransform.sizeDelta = Vector2.zero;
		((Transform)val2).SetSiblingIndex(siblingIndex);
		addedFitter = null;
		ContentSizeFitter val4 = ((Component)description).gameObject.GetComponent<ContentSizeFitter>();
		if ((Object)val4 == (Object)null)
		{
			val4 = (addedFitter = ((Component)description).gameObject.AddComponent<ContentSizeFitter>());
		}
		val4.horizontalFit = (FitMode)0;
		val4.verticalFit = (FitMode)2;
		Scrollbar verticalScrollbar = BuildScrollbar(val2);
		ScrollRect obj3 = val.AddComponent<ScrollRect>();
		obj3.viewport = val3;
		obj3.content = rectTransform;
		obj3.horizontal = false;
		obj3.vertical = true;
		obj3.movementType = (MovementType)2;
		obj3.inertia = true;
		obj3.decelerationRate = 0.2f;
		obj3.scrollSensitivity = 42f;
		obj3.verticalScrollbar = verticalScrollbar;
		obj3.verticalScrollbarVisibility = (ScrollbarVisibility)1;
		return obj3;
	}

	private static void FitBodyHeightToContent(ScrollRect scroll, TextMeshProUGUI description, RectTransform window)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected O, but got Unknown
		RectTransform val = (RectTransform)((Component)scroll).transform;
		LayoutRebuilder.ForceRebuildLayoutImmediate(val);
		LayoutElement component = ((Component)scroll).GetComponent<LayoutElement>();
		if ((Object)component != (Object)null)
		{
			float preferredHeight = Mathf.Clamp(((TMP_Text)description).preferredHeight + 16f, 180f, 440f);
			component.preferredHeight = preferredHeight;
		}
		if ((Object)window != (Object)null)
		{
			LayoutRebuilder.ForceRebuildLayoutImmediate(window);
		}
		else
		{
			LayoutRebuilder.ForceRebuildLayoutImmediate(val);
		}
	}

	private static Scrollbar BuildScrollbar(RectTransform scrollRoot)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Expected O, but got Unknown
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e6: Expected O, but got Unknown
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0146: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Expected O, but got Unknown
		GameObject val = new GameObject("Scrollbar", new Type[1] { typeof(RectTransform) });
		RectTransform val2 = (RectTransform)val.transform;
		((Transform)val2).SetParent((Transform)scrollRoot, false);
		val2.anchorMin = new Vector2(1f, 0f);
		val2.anchorMax = Vector2.one;
		val2.pivot = new Vector2(1f, 0.5f);
		val2.sizeDelta = new Vector2(6f, 0f);
		val2.anchoredPosition = Vector2.zero;
		((Graphic)val.AddComponent<Image>()).color = new Color(1f, 1f, 1f, 0.06f);
		GameObject val3 = new GameObject("Handle", new Type[1] { typeof(RectTransform) });
		RectTransform val4 = (RectTransform)val3.transform;
		((Transform)val4).SetParent((Transform)val2, false);
		val4.anchorMin = Vector2.zero;
		val4.anchorMax = Vector2.one;
		val4.offsetMin = Vector2.zero;
		val4.offsetMax = Vector2.zero;
		Image val5 = val3.AddComponent<Image>();
		((Graphic)val5).color = new Color(1f, 1f, 1f, 0.28f);
		Scrollbar obj = val.AddComponent<Scrollbar>();
		obj.handleRect = val4;
		((Selectable)obj).targetGraphic = (Graphic)val5;
		obj.direction = (Direction)2;
		return obj;
	}

	private static string BuildStyledReport(NekoModMismatchNotification notification, string plain)
	{
		try
		{
			ParseReport(notification.Report, out var bullets, out var overflow, out var footer);
			if (bullets.Count == 0 && string.IsNullOrEmpty(footer))
			{
				return Sanitize(plain);
			}
			string value = Sanitize(string.IsNullOrEmpty(notification.PeerNickname) ? "对方" : notification.PeerNickname);
			StringBuilder stringBuilder = new StringBuilder(2048);
			stringBuilder.Append("<size=112%>与 <color=").Append("#51C878").Append("><b>")
				.Append(value)
				.Append("</b></color> 所在服务器的 MOD 配置不一致</size>\n");
			stringBuilder.Append("<size=84%><color=").Append("#8F8E7E").Append('>');
			if (bullets.Count > 0)
			{
				stringBuilder.Append("共 ").Append(bullets.Count).Append((overflow == null) ? " 项差异 · " : "+ 项差异 · ");
			}
			stringBuilder.Append("</color>");
			stringBuilder.Append(notification.Blocked ? "<color=#D96A6A>已自动拦截本次组队 / 战局邀请</color>" : "<color=#D9C468>仅警告：仍可继续，但存在物品丢失与报错风险</color>");
			stringBuilder.Append("</size>\n\n");
			if (bullets.Count > 0)
			{
				AppendSectionLabel(stringBuilder, "差异明细");
				foreach (string item in bullets)
				{
					stringBuilder.Append(FormatBullet(item)).Append('\n');
				}
				if (overflow != null)
				{
					stringBuilder.Append("<color=").Append("#8F8E7E").Append("><size=86%><i>· ")
						.Append(Sanitize(overflow))
						.Append("</i></size></color>\n");
				}
			}
			if (!string.IsNullOrEmpty(footer))
			{
				stringBuilder.Append('\n');
				AppendSectionLabel(stringBuilder, "建议");
				stringBuilder.Append("<color=").Append("#8F8E7E").Append("><size=86%>")
					.Append(Sanitize(footer))
					.Append("</size></color>");
			}
			return stringBuilder.ToString();
		}
		catch (Exception)
		{
			return Sanitize(plain);
		}
	}

	private static void ParseReport(string report, out List<string> bullets, out string overflow, out string footer)
	{
		bullets = new List<string>();
		overflow = null;
		StringBuilder stringBuilder = new StringBuilder();
		if (!string.IsNullOrEmpty(report))
		{
			bool flag = false;
			string[] array = report.Replace("\r\n", "\n").Split('\n');
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.Length == 0)
				{
					continue;
				}
				if (text.StartsWith("·", StringComparison.Ordinal))
				{
					flag = true;
					string text2 = text.TrimStart('·', ' ');
					if (text2.StartsWith("……", StringComparison.Ordinal) || text2.StartsWith("另有", StringComparison.Ordinal))
					{
						overflow = text2;
					}
					else
					{
						bullets.Add(text2);
					}
				}
				else if (flag)
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append('\n');
					}
					stringBuilder.Append(text);
				}
			}
		}
		footer = stringBuilder.ToString();
	}

	private static void AppendSectionLabel(StringBuilder sb, string label)
	{
		sb.Append("<mark=").Append("#FFFFFF14").Append("><color=#B8B7A6><size=82%>\u3000")
			.Append(label)
			.Append("\u3000</size></color></mark>\n");
	}

	private static string FormatBullet(string content)
	{
		content = Sanitize(content);
		int num = content.IndexOf('：');
		if (num <= 0 || num >= 16)
		{
			return "· " + DimParenthetical(content);
		}
		string text = content.Substring(0, num);
		string text2 = DimParenthetical(content.Substring(num + 1).Trim());
		string text3 = PickColor(text);
		return $"<color={text3}><b>· {text}</b></color><indent={190}>{text2}</indent>";
	}

	private static string PickColor(string label)
	{
		if (label.Contains("缺少"))
		{
			return "#D96A6A";
		}
		if (label.Contains("多出"))
		{
			return "#D9A05B";
		}
		if (label.Contains("SPT") || label.Contains("Fika"))
		{
			return "#E0685B";
		}
		if (label.Contains("物品"))
		{
			return "#6FA8DC";
		}
		return "#D9C468";
	}

	private static string DimParenthetical(string text)
	{
		return Regex.Replace(text, "\\(([^()]{1,80})\\)", "<color=#8F8E7E><size=86%>($1)</size></color>");
	}

	private static string Sanitize(string text)
	{
		if (!string.IsNullOrEmpty(text))
		{
			return text.Replace('<', '‹').Replace('>', '›');
		}
		return "";
	}

	private static ErrorScreen FindActiveCriticalScreen(PreloaderUI preloader)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		object obj = ContainerField?.GetValue(preloader);
		Component val = (Component)((obj is Component) ? obj : null);
		if ((Object)(object)val != (Object)null)
		{
			ErrorScreen[] componentsInChildren = val.GetComponentsInChildren<ErrorScreen>(true);
			for (int num = componentsInChildren.Length - 1; num >= 0; num--)
			{
				if (((Component)componentsInChildren[num]).gameObject.activeSelf)
				{
					return componentsInChildren[num];
				}
			}
		}
		return null;
	}
}
