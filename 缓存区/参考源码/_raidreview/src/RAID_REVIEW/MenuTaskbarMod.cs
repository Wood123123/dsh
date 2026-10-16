using System;
using System.Collections.Generic;
using System.Reflection;
using EFT.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RAID_REVIEW;

public class MenuTaskbarMod : MonoBehaviour
{
	public static bool Insert(MenuTaskBar instance)
	{
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Expected O, but got Unknown
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Expected O, but got Unknown
		List<ToggleGroup> list = new List<ToggleGroup>(Object.FindObjectsOfType<ToggleGroup>());
		if ((Object)(object)list.Find((ToggleGroup toggleGroupItem) => ((Object)toggleGroupItem).name == "RaidReview") != (Object)null)
		{
			return false;
		}
		foreach (ToggleGroup item in list)
		{
			if (!((Object)(object)item != (Object)null) || !(((Object)item).name == "Handbook"))
			{
				continue;
			}
			ToggleGroup duplicatedToggleGroup = Object.Instantiate<ToggleGroup>(item, ((Component)item).transform.parent);
			((Object)duplicatedToggleGroup).name = "RaidReview";
			HoverTooltipArea component = ((Component)duplicatedToggleGroup).GetComponent<HoverTooltipArea>();
			if (component != null)
			{
				component.SetUnlockStatus(true);
			}
			foreach (Transform item2 in ((Component)duplicatedToggleGroup).transform)
			{
				Transform val = item2;
				if (((Object)val).name == "NewInformation")
				{
					Object.Destroy((Object)(object)((Component)val).gameObject);
					continue;
				}
				((Object)val).name = "RaidReviewButton";
				AnimatedToggle childAnimatedToggle = ((Component)val).GetComponent<AnimatedToggle>();
				if ((Object)(object)childAnimatedToggle != (Object)null)
				{
					((UnityEvent<bool>)(object)((Toggle)childAnimatedToggle).onValueChanged).AddListener((UnityAction<bool>)delegate(bool value)
					{
						if (value)
						{
							Application.OpenURL(RAID_REVIEW.RAID_REVIEW_HTTP_Server);
							((Toggle)childAnimatedToggle).isOn = false;
						}
					});
				}
				foreach (Transform item3 in ((Component)val).transform)
				{
					Transform val2 = item3;
					if (!(((Object)val2).name == "Text"))
					{
						continue;
					}
					LocalizedText component2 = ((Component)val2).GetComponent<LocalizedText>();
					if (!((Object)(object)component2 != (Object)null))
					{
						continue;
					}
					if (component2.LocalizationKey == "HANDBOOK")
					{
						component2.LocalizationKey = "RAID REVIEW";
					}
					Type type = ((object)component2).GetType();
					PropertyInfo[] properties = type.GetProperties();
					FieldInfo[] fields = type.GetFields();
					PropertyInfo[] array = properties;
					foreach (PropertyInfo propertyInfo in array)
					{
						if (propertyInfo.PropertyType == typeof(string) && propertyInfo.CanWrite && (string)propertyInfo.GetValue(component2) == "HANDBOOK")
						{
							propertyInfo.SetValue(component2, "RAID REVIEW");
						}
					}
					FieldInfo[] array2 = fields;
					foreach (FieldInfo fieldInfo in array2)
					{
						if (fieldInfo.FieldType == typeof(string) && (string)fieldInfo.GetValue(component2) == "HANDBOOK")
						{
							fieldInfo.SetValue(component2, "RAID REVIEW");
						}
					}
				}
			}
			int siblingIndex = ((Component)item).transform.GetSiblingIndex();
			ToggleGroup val3 = new List<ToggleGroup>(Object.FindObjectsOfType<ToggleGroup>()).Find((ToggleGroup toggleGroupItem) => ((Object)toggleGroupItem).name == ((Object)duplicatedToggleGroup).name);
			if ((Object)(object)val3 == (Object)null)
			{
				((Component)duplicatedToggleGroup).transform.SetSiblingIndex(siblingIndex + 2);
			}
			else
			{
				((Component)val3).transform.SetSiblingIndex(siblingIndex + 2);
			}
			break;
		}
		return true;
	}
}
