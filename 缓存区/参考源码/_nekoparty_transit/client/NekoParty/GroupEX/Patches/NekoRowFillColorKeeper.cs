using System;
using UnityEngine;
using UnityEngine.UI;

namespace NekoParty.GroupEX.Patches;

internal sealed class NekoRowFillColorKeeper : MonoBehaviour
{
	private static readonly Color FillColor;

	private static readonly Color BorderColor;

	private static Sprite _whiteSprite;

	public Image Target;

	private bool _frameApplied;

	private void LateUpdate()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected O, but got Unknown
		//IL_0084: Expected O, but got Unknown
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)Target == (Object)null))
		{
			if ((Object)_whiteSprite == (Object)null)
			{
				Texture2D whiteTexture = Texture2D.whiteTexture;
				_whiteSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, (float)((Texture)whiteTexture).width, (float)((Texture)whiteTexture).height), new Vector2(0.5f, 0.5f));
			}
			if ((Object)Target.sprite != (Object)_whiteSprite)
			{
				Target.sprite = _whiteSprite;
			}
			((Graphic)Target).color = FillColor;
			EnsureFrame();
		}
	}

	private void EnsureFrame()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Expected O, but got Unknown
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Expected O, but got Unknown
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected O, but got Unknown
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		if (_frameApplied)
		{
			return;
		}
		Transform parent = ((Component)Target).transform.parent;
		RectTransform val = (RectTransform)((parent is RectTransform) ? parent : null);
		if ((Object)(object)val != (Object)null)
		{
			Graphic val2 = (Graphic)((Component)val).GetComponent<Image>();
			if ((Object)val2 == (Object)null)
			{
				GameObject val3 = new GameObject("NekoRowFrame", new Type[1] { typeof(RectTransform) });
				RectTransform val4 = (RectTransform)val3.transform;
				((Transform)val4).SetParent((Transform)val, false);
				val4.anchorMin = Vector2.zero;
				val4.anchorMax = Vector2.one;
				val4.offsetMin = Vector2.zero;
				val4.offsetMax = Vector2.zero;
				Image obj = val3.AddComponent<Image>();
				((Graphic)obj).color = new Color(0f, 0f, 0f, 0f);
				((Graphic)obj).raycastTarget = false;
				((Transform)val4).SetAsFirstSibling();
				val2 = (Graphic)obj;
			}
			if ((Object)((Component)val2).GetComponent<Outline>() == (Object)null)
			{
				Outline obj2 = ((Component)val2).gameObject.AddComponent<Outline>();
				((Shadow)obj2).effectColor = BorderColor;
				((Shadow)obj2).effectDistance = new Vector2(1f, 1f);
			}
			_frameApplied = true;
		}
	}

	static NekoRowFillColorKeeper()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		FillColor = new Color(0.67f, 0.66f, 0.6f, 0.96f);
		BorderColor = new Color(0.6f, 0.62f, 0.55f, 0.85f);
	}
}
