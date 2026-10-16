using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NekoParty.GroupEX.UI;

internal static class NekoPartyPlayerRowUi
{
	internal static void RemoveInlineActionButtons(Transform row)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Expected O, but got Unknown
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		if ((Object)row == (Object)null)
		{
			return;
		}
		List<GameObject> list = new List<GameObject>();
		Transform[] componentsInChildren = ((Component)row).GetComponentsInChildren<Transform>(true);
		foreach (Transform val in componentsInChildren)
		{
			if ((Object)val != (Object)null && (((Object)val).name == "NekoPartyActionButton" || ((Object)val).name == "PartyActionButton"))
			{
				list.Add(((Component)val).gameObject);
			}
		}
		foreach (Object item in list)
		{
			Object.Destroy(item);
		}
	}

	internal static void FitButtonToLabel(RectTransform buttonRect, TextMeshProUGUI label, float horizontalPadding = 18f, float minWidth = 80f, float maxWidth = 130f)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)label == (Object)null))
		{
			((TMP_Text)label).enableWordWrapping = false;
			((TMP_Text)label).overflowMode = (TextOverflowModes)0;
			((TMP_Text)label).ForceMeshUpdate(false, false);
			float num = Mathf.Clamp(((TMP_Text)label).preferredWidth + horizontalPadding, minWidth, maxWidth);
			buttonRect.sizeDelta = new Vector2(num, buttonRect.sizeDelta.y);
		}
	}

	internal static void PlaceInlineActionButton(RectTransform buttonRect, GameObject rowObject, float padding = 10f, float height = 26f)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Expected O, but got Unknown
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Expected O, but got Unknown
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Expected O, but got Unknown
		//IL_014c: Unknown result type (might be due to invalid IL or missing references)
		//IL_012c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Expected O, but got Unknown
		//IL_01cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01da: Expected O, but got Unknown
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_0228: Unknown result type (might be due to invalid IL or missing references)
		//IL_022d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0232: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0243: Unknown result type (might be due to invalid IL or missing references)
		//IL_024a: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)rowObject == (Object)null)
		{
			return;
		}
		Transform transform = rowObject.transform;
		RectTransform val = (RectTransform)((transform is RectTransform) ? transform : null);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		MainMenuUIPlayer component = rowObject.GetComponent<MainMenuUIPlayer>();
		object obj;
		if (!((Object)component != (Object)null))
		{
			Transform val2 = rowObject.transform.Find("Nickname");
			obj = (((Object)(object)val2 != (Object)null) ? ((Component)val2).GetComponent<TextMeshProUGUI>() : null);
		}
		else
		{
			obj = component.PlayerName;
		}
		TextMeshProUGUI val3 = (TextMeshProUGUI)obj;
		float num = Mathf.Max(buttonRect.sizeDelta.x, 80f);
		((Transform)buttonRect).SetParent((Transform)val, false);
		((Transform)buttonRect).localScale = Vector3.one;
		((Transform)buttonRect).SetAsLastSibling();
		(((Component)buttonRect).GetComponent<LayoutElement>() ?? ((Component)buttonRect).gameObject.AddComponent<LayoutElement>()).ignoreLayout = true;
		buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
		buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
		buttonRect.pivot = new Vector2(0.5f, 0.5f);
		buttonRect.sizeDelta = new Vector2(num, height);
		if ((Object)val3 == (Object)null)
		{
			buttonRect.anchoredPosition = Vector2.zero;
			return;
		}
		Canvas.ForceUpdateCanvases();
		((TMP_Text)val3).ForceMeshUpdate(false, false);
		float num2 = ((Transform)((TMP_Text)val3).rectTransform).lossyScale.x;
		if (Mathf.Approximately(num2, 0f))
		{
			num2 = 1f;
		}
		Vector3[] array = (Vector3[])(object)new Vector3[4];
		((TMP_Text)val3).rectTransform.GetWorldCorners(array);
		float num3 = array[0].x + ((TMP_Text)val3).preferredWidth * num2;
		float num4 = num3 + (num * 0.5f + padding) * num2;
		TextMeshProUGUI val4 = (((Object)component != (Object)null) ? component.PlayerLevel : null);
		if ((Object)val4 != (Object)null)
		{
			Vector3[] array2 = (Vector3[])(object)new Vector3[4];
			((TMP_Text)val4).rectTransform.GetWorldCorners(array2);
			float x = array2[0].x;
			float num5 = (num3 + x) * 0.5f;
			if (num5 > num4)
			{
				num4 = num5;
			}
		}
		Rect rect = val.rect;
		Vector3 val5 = ((Transform)val).TransformPoint(Vector2.op_Implicit(((Rect)(ref rect)).center));
		((Transform)buttonRect).position = new Vector3(num4, val5.y, val5.z);
	}
}
