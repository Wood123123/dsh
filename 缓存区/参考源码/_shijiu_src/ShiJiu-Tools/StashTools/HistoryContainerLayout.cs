using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using EFT.Utilities;
using UnityEngine;

namespace StashTools;

internal static class HistoryContainerLayout
{
	private static readonly Dictionary<string, HistoryGridSpec[]> Cache = new Dictionary<string, HistoryGridSpec[]>();

	internal static HistoryGridSpec[] Read(string template)
	{
		//IL_0189: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_0197: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a7: Unknown result type (might be due to invalid IL or missing references)
		if (Cache.TryGetValue(template, out HistoryGridSpec[] value))
		{
			return value;
		}
		ItemFactory instance = Singleton<ItemFactory>.Instance;
		if (instance == null)
		{
			return Array.Empty<HistoryGridSpec>();
		}
		HistoryGridSpec[] array = Array.Empty<HistoryGridSpec>();
		try
		{
			Item val = instance.CreateItem(Guid.NewGuid().ToString("N").Substring(0, 24), template, (UnparsedData)null);
			Grid[] array2 = val.TryCast<CompoundItem>()?.Grids?.ToArray();
			if (array2 != null && array2.Length != 0)
			{
				array = array2.Select((Grid g) => new HistoryGridSpec(g.ID, g.GridWidth, g.GridHeight)).ToArray();
				GridLayoutComponent itemComponent = val.GetItemComponent<GridLayoutComponent>();
				object obj;
				if (itemComponent == null)
				{
					obj = null;
				}
				else
				{
					IGridLayoutComponentTemplate template2 = itemComponent.Template;
					obj = ((template2 != null) ? template2.LayoutName : null);
				}
				string text = (string)obj;
				if (!string.IsNullOrWhiteSpace(text))
				{
					TemplatedGridsView val2 = ResourcesCache.Pop<ContainedGridsView>("UI/Rig Layouts/" + text)?.TryCast<TemplatedGridsView>();
					GridView[] array3 = val2?._presetGridViews;
					if ((Object)(object)val2 != (Object)null && array3 != null && array3.Length == array2.Length)
					{
						RectTransform component = ((Component)val2).GetComponent<RectTransform>();
						Vector3[] array4 = (Vector3[])(object)new Vector3[4];
						for (int num = 0; num < array3.Length; num++)
						{
							GridView obj2 = array3[num];
							RectTransform val3 = ((obj2 != null) ? ((Component)obj2).GetComponent<RectTransform>() : null);
							if (!((Object)(object)val3 == (Object)null) && !((Object)(object)component == (Object)null))
							{
								val3.GetWorldCorners(array4);
								float num2 = float.PositiveInfinity;
								float num3 = float.NegativeInfinity;
								for (int num4 = 0; num4 < 4; num4++)
								{
									Vector3 val4 = ((Transform)component).InverseTransformPoint(array4[num4]);
									num2 = Math.Min(num2, val4.x);
									num3 = Math.Max(num3, val4.y);
								}
								array[num] = array[num]with
								{
									X = num2 / 63f,
									Y = (0f - num3) / 63f
								};
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogInfo((object)("历史容器布局使用通用排列：" + template + " " + ex.Message));
		}
		if (Cache.Count >= 256)
		{
			Cache.Clear();
		}
		Cache[template] = array;
		return array;
	}
}
