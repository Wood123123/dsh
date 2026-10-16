using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StashTools;

internal sealed class ValuePanel
{
	private readonly record struct Paint(string Title, string Detail, Vector2 Position, float Width, float TitleSize, float DetailSize, string TitleColor, string DetailColor);

	private RectTransform? _root;

	private RectTransform? _parent;

	private TextMeshProUGUI? _title;

	private TextMeshProUGUI? _detail;

	private Paint? _paint;

	internal void Hide()
	{
		if ((Object)(object)_root != (Object)null && ((Component)_root).gameObject.activeSelf)
		{
			((Component)_root).gameObject.SetActive(false);
		}
	}

	internal void Dispose()
	{
		if ((Object)(object)_root != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)_root).gameObject);
		}
		_root = null;
		_parent = null;
		_title = null;
		_detail = null;
		_paint = null;
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

	private static TextMeshProUGUI Label(string name, RectTransform parent, TMP_Text native)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		TextMeshProUGUI obj = ((Component)Rect(name, (Transform)(object)parent)).gameObject.AddComponent<TextMeshProUGUI>();
		((TMP_Text)obj).font = native.font;
		((TMP_Text)obj).fontSharedMaterial = native.fontSharedMaterial;
		((TMP_Text)obj).spriteAsset = native.spriteAsset;
		((Graphic)obj).raycastTarget = false;
		((MaskableGraphic)obj).maskable = true;
		((TMP_Text)obj).enableAutoSizing = false;
		((TMP_Text)obj).enableWordWrapping = false;
		((TMP_Text)obj).richText = true;
		((TMP_Text)obj).overflowMode = (TextOverflowModes)0;
		((TMP_Text)obj).margin = Vector4.zero;
		((TMP_Text)obj).alignment = (TextAlignmentOptions)257;
		return obj;
	}

	internal static Color Color(string value, string fallback)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		Color result = default(Color);
		if (ColorUtility.TryParseHtmlString(value, ref result))
		{
			return result;
		}
		ColorUtility.TryParseHtmlString(fallback, ref result);
		return result;
	}

	internal static string Notice(int pending, int missing)
	{
		return ((pending > 0) ? $"  <size=65%>（{pending} 类估价中）</size>" : "") + ((missing > 0) ? $"  <size=65%>（{missing} 类无报价）</size>" : "");
	}

	internal void Show(RectTransform parent, TMP_Text native, ValuePanelOptions options, string title, string detail, Vector2 origin, float width)
	{
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0329: Unknown result type (might be due to invalid IL or missing references)
		//IL_0352: Unknown result type (might be due to invalid IL or missing references)
		if (!options.Enabled.Value || (Object)(object)native.font == (Object)null)
		{
			Hide();
			return;
		}
		if ((Object)(object)_root == (Object)null || _parent?.Identity() != parent.Identity())
		{
			Dispose();
			_parent = parent;
			_root = Rect("StashToolsValuePanel", (Transform)(object)parent);
			((Component)_root).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
			CanvasGroup obj = ((Component)_root).gameObject.AddComponent<CanvasGroup>();
			obj.interactable = false;
			obj.blocksRaycasts = false;
			_title = Label("Total", _root, native);
			_detail = Label("Detail", _root, native);
		}
		if (!((Component)_root).gameObject.activeSelf)
		{
			((Component)_root).gameObject.SetActive(true);
		}
		Paint value = new Paint(title, detail, origin + new Vector2(options.X.Value, 0f - options.Y.Value), Math.Max(100f, width), options.TitleSize.Value, options.DetailSize.Value, options.TitleColor.Value, options.DetailColor.Value);
		if (!(_paint == value))
		{
			_root.anchoredPosition = value.Position;
			bool flag = !string.IsNullOrEmpty(detail);
			_root.sizeDelta = new Vector2(value.Width, value.TitleSize * 1.3f + (flag ? (value.DetailSize * 1.4f + 2f) : 0f));
			((TMP_Text)_title).fontSize = value.TitleSize;
			((TMP_Text)_title).fontStyle = (FontStyles)1;
			((Graphic)_title).color = Color(value.TitleColor, "#E0E3D9");
			((TMP_Text)_detail).fontSize = value.DetailSize;
			((TMP_Text)_detail).fontStyle = (FontStyles)0;
			((Graphic)_detail).color = Color(value.DetailColor, "#B8BDB8");
			if (((TMP_Text)_title).text != title)
			{
				((TMP_Text)_title).text = title;
			}
			if (((TMP_Text)_detail).text != detail)
			{
				((TMP_Text)_detail).text = detail;
			}
			if (((Component)_detail).gameObject.activeSelf != flag)
			{
				((Component)_detail).gameObject.SetActive(flag);
			}
			((TMP_Text)_title).rectTransform.sizeDelta = new Vector2(value.Width, value.TitleSize * 1.4f);
			((TMP_Text)_detail).rectTransform.anchoredPosition = new Vector2(0f, 0f - (value.TitleSize * 1.3f + 2f));
			((TMP_Text)_detail).rectTransform.sizeDelta = new Vector2(value.Width, value.DetailSize * 1.4f);
			_paint = value;
		}
	}
}
