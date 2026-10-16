using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using CookingGrenades.Config;
using EFT;
using EFT.InventoryLogic;
using EFT.NetworkPackets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CookingGrenades;

public class MedicineWheel : WheelBase<MedicineWheel>
{
	private class GroupedMedicine
	{
		public string TemplateId;

		public string Name;

		public string FullName;

		public int Count;

		public Color Color;

		public Item FirstItem;
	}

	private readonly List<GroupedMedicine> _displayedMedicine = new List<GroupedMedicine>();

	protected override string WheelLogName => "MedicineWheel";

	protected override bool EnabledByConfig => ConfigManager.EnableMedicineWheel.Value;

	protected override KeyCode HoldKey
	{
		get
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return ConfigManager.MedicineWheelKey.Value;
		}
	}

	protected override int DisplayCount => _displayedMedicine.Count;

	protected override bool IsOtherWheelOpen()
	{
		if ((Object)(object)WheelBase<GrenadeWheel>.Instance != (Object)null)
		{
			return WheelBase<GrenadeWheel>.Instance.IsOpen;
		}
		return false;
	}

	protected override string GetSlotName(int index)
	{
		return _displayedMedicine[index].Name;
	}

	protected override string GetSlotFullName(int index)
	{
		return _displayedMedicine[index].FullName;
	}

	protected override int GetSlotCount(int index)
	{
		return _displayedMedicine[index].Count;
	}

	protected override void LoadSlotIcon(int index, Image iconImage, TextMeshProUGUI fallbackText)
	{
		GroupedMedicine groupedMedicine = _displayedMedicine[index];
		LoadItemIconAsync(groupedMedicine.FirstItem, groupedMedicine.TemplateId, iconImage, fallbackText);
	}

	protected override void OnSelect(int index)
	{
		UseMedicine(_displayedMedicine[index]);
	}

	protected override void ScanItems()
	{
		Plugin.log.LogInfo((object)"[MedicineWheel] 扫描药品...");
		ScanMedicine();
	}

	private void ScanMedicine()
	{
		_displayedMedicine.Clear();
		try
		{
			bool value = ConfigManager.MedicineWheelScanBackpack.Value;
			bool value2 = ConfigManager.MedicineWheelScanSecure.Value;
			bool value3 = ConfigManager.MedicineWheelIncludeFood.Value;
			InventoryEquipment val = _player.Inventory?.Equipment;
			if (val == null)
			{
				return;
			}
			Item root = null;
			Item root2 = null;
			try
			{
				Slot slot = val.GetSlot((EquipmentSlot)5);
				root = ((slot != null) ? slot.ContainedItem : null);
			}
			catch (Exception)
			{
			}
			try
			{
				Slot slot2 = val.GetSlot((EquipmentSlot)4);
				root2 = ((slot2 != null) ? slot2.ContainedItem : null);
			}
			catch (Exception)
			{
			}
			List<Item> list = new List<Item>();
			List<Item> list2 = new List<Item>();
			Stack<Item> stack = new Stack<Item>();
			stack.Push((Item)(object)val);
			int num = 0;
			while (stack.Count > 0 && num++ < 5000)
			{
				Item val2 = stack.Pop();
				if (val2 == null)
				{
					continue;
				}
				bool flag = false;
				for (int i = 0; i < list2.Count; i++)
				{
					if (list2[i] == val2)
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					continue;
				}
				list2.Add(val2);
				if (val2 is Meds || (value3 && val2 is FoodDrink))
				{
					if ((!value2 && IsUnderSafe(val2, root)) || (!value && IsUnderSafe(val2, root2)))
					{
						continue;
					}
					list.Add(val2);
				}
				ContainerCollection val3 = (ContainerCollection)(object)((val2 is ContainerCollection) ? val2 : null);
				if (val3 == null)
				{
					continue;
				}
				IEnumerable<IContainer> enumerable = null;
				try
				{
					enumerable = val3.Containers;
				}
				catch (Exception)
				{
					continue;
				}
				if (enumerable == null)
				{
					continue;
				}
				foreach (IContainer item in enumerable)
				{
					if (item == null)
					{
						continue;
					}
					IEnumerable<Item> enumerable2 = null;
					try
					{
						enumerable2 = item.Items;
					}
					catch (Exception)
					{
						continue;
					}
					if (enumerable2 == null)
					{
						continue;
					}
					foreach (Item item2 in enumerable2)
					{
						if (item2 != null)
						{
							stack.Push(item2);
						}
					}
				}
			}
			Plugin.log.LogInfo((object)$"[MedicineWheel] 扫描完成，遍历 {list2.Count} 个物品，命中药品 {list.Count} 个");
			_displayedMedicine.Clear();
			_displayedMedicine.AddRange(list.GroupBy(delegate(Item it)
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0006: Unknown result type (might be due to invalid IL or missing references)
				return ((object)it.TemplateId/*cast due to constrained. prefix*/).ToString();
			}).Select(delegate(IGrouping<string, Item> g)
			{
				//IL_0065: Unknown result type (might be due to invalid IL or missing references)
				//IL_006a: Unknown result type (might be due to invalid IL or missing references)
				Item val4 = g.First();
				return new GroupedMedicine
				{
					TemplateId = g.Key,
					Name = (string.IsNullOrEmpty(val4.ShortName) ? LocalizationExtensions.Localized(val4.Name, (string)null) : LocalizationExtensions.Localized(val4.ShortName, (string)null)),
					FullName = LocalizationExtensions.Localized(val4.Name, (string)null),
					Count = g.Count(),
					Color = GetMedicineColor(val4),
					FirstItem = val4
				};
			}));
			foreach (GroupedMedicine item3 in _displayedMedicine)
			{
				Plugin.log.LogInfo((object)$"[MedicineWheel] 分组: {item3.FullName} x{item3.Count}");
			}
		}
		catch (Exception ex5)
		{
			Plugin.log.LogError((object)("[MedicineWheel] 扫描药品异常: " + ex5.Message));
		}
	}

	private static bool IsUnderSafe(Item item, Item root)
	{
		if (root == null || item == null)
		{
			return false;
		}
		Item val = item;
		int num = 0;
		while (val != null && num++ < 32)
		{
			if (val == root)
			{
				return true;
			}
			Item val2 = null;
			try
			{
				ItemAddress parent = val.Parent;
				object obj;
				if (parent == null)
				{
					obj = null;
				}
				else
				{
					IContainer container = parent.Container;
					obj = ((container != null) ? container.ParentItem : null);
				}
				val2 = (Item)obj;
			}
			catch (Exception)
			{
				break;
			}
			val = val2;
		}
		return false;
	}

	private void UseMedicine(GroupedMedicine group)
	{
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)_player == (Object)null || group?.FirstItem == null)
		{
			return;
		}
		try
		{
			Item firstItem = group.FirstItem;
			Meds val = (Meds)(object)((firstItem is Meds) ? firstItem : null);
			if (val != null)
			{
				if (val is MedKit)
				{
					if (!_player.HealthController.CanApplyItem((Item)(object)val, (EBodyPart)7))
					{
						Plugin.log.LogInfo((object)("[MedicineWheel] 当前无需治疗，跳过: " + group.FullName));
						return;
					}
					OneAndList<EBodyPart> val2 = _player.HealthController.BodyPartsPriority((Item)(object)val, true);
					_player.Proceed(val, val2, (Callback<IMedsController>)null, 0, true);
				}
				else if (val is Drugs || val is Stimulator)
				{
					_player.Proceed(val, new OneAndList<EBodyPart>((EBodyPart)7), (Callback<IMedsController>)null, 0, true);
				}
				else
				{
					if (!_player.HealthController.CanApplyItem((Item)(object)val, (EBodyPart)7))
					{
						Plugin.log.LogInfo((object)("[MedicineWheel] 当前无需治疗，跳过: " + group.FullName));
						return;
					}
					_player.Proceed(val, new OneAndList<EBodyPart>((EBodyPart)7), (Callback<IMedsController>)null, 0, true);
				}
			}
			else
			{
				FoodDrink val3 = (FoodDrink)(object)((firstItem is FoodDrink) ? firstItem : null);
				if (val3 != null)
				{
					_player.Proceed(val3, 1f, (Callback<IMedsController>)null, 0, true);
				}
				else
				{
					_player.Proceed(firstItem, (Callback<IQuickUseItem>)null, true);
				}
			}
			Plugin.log.LogInfo((object)("[MedicineWheel] 正在使用: " + group.FullName + " x1"));
		}
		catch (Exception ex)
		{
			Plugin.log.LogError((object)("[MedicineWheel] 使用失败: " + ex.Message));
		}
	}

	private async void LoadItemIconAsync(Item item, string templateId, Image iconImage, TextMeshProUGUI fallbackText)
	{
		if (item == null || (Object)(object)iconImage == (Object)null)
		{
			return;
		}
		Sprite val = await ItemIconCache.GetOrLoadAsync(item, templateId);
		if ((Object)(object)val == (Object)null)
		{
			Plugin.log.LogInfo((object)("[MedicineWheel] 无内建图标，使用文字显示: " + LocalizationExtensions.Localized(item.ShortName, (string)null)));
		}
		else if ((Object)(object)iconImage != (Object)null)
		{
			iconImage.sprite = val;
			((Behaviour)iconImage).enabled = true;
			if ((Object)(object)fallbackText != (Object)null)
			{
				((Behaviour)fallbackText).enabled = false;
			}
		}
	}

	private static Color GetMedicineColor(Item item)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		if (item is MedKit)
		{
			return new Color(0.35f, 0.75f, 0.4f);
		}
		if (item is Drugs || item is Stimulator)
		{
			return new Color(0.8f, 0.5f, 0.85f);
		}
		if (item is FoodDrink)
		{
			return new Color(0.9f, 0.65f, 0.3f);
		}
		if (item is Meds)
		{
			return new Color(0.45f, 0.65f, 0.9f);
		}
		return new Color(0.6f, 0.6f, 0.6f);
	}
}
