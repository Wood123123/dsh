using System;
using System.Collections.Generic;
using System.Linq;
using Comfort.Common;
using CookingGrenades.Config;
using EFT;
using EFT.InventoryLogic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CookingGrenades;

public class GrenadeWheel : WheelBase<GrenadeWheel>
{
	private class GroupedGrenade
	{
		public string TemplateId;

		public string Name;

		public string FullName;

		public int Count;

		public Color Color;

		public ThrowWeap FirstItem;
	}

	public static string PreferredTemplateId;

	private readonly List<GroupedGrenade> _displayedGrenades = new List<GroupedGrenade>();

	protected override string WheelLogName => "GrenadeWheel";

	protected override bool EnabledByConfig => ConfigManager.EnableGrenadeWheel.Value;

	protected override KeyCode HoldKey
	{
		get
		{
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			return ConfigManager.GrenadeWheelKey.Value;
		}
	}

	protected override int DisplayCount => _displayedGrenades.Count;

	protected override bool IsOtherWheelOpen()
	{
		if ((Object)(object)WheelBase<MedicineWheel>.Instance != (Object)null)
		{
			return WheelBase<MedicineWheel>.Instance.IsOpen;
		}
		return false;
	}

	protected override string GetSlotName(int index)
	{
		return _displayedGrenades[index].Name;
	}

	protected override string GetSlotFullName(int index)
	{
		return _displayedGrenades[index].FullName;
	}

	protected override int GetSlotCount(int index)
	{
		return _displayedGrenades[index].Count;
	}

	protected override void LoadSlotIcon(int index, Image iconImage, TextMeshProUGUI fallbackText)
	{
		GroupedGrenade groupedGrenade = _displayedGrenades[index];
		LoadBuiltInIconAsync(groupedGrenade.TemplateId, groupedGrenade.FirstItem, iconImage, fallbackText);
	}

	protected override void OnSelect(int index)
	{
		EquipGrenade(_displayedGrenades[index]);
	}

	protected override int GetInitialSelectedIndex()
	{
		try
		{
			Player player = _player;
			object obj;
			if (player == null)
			{
				obj = null;
			}
			else
			{
				InventoryController inventoryController = player.InventoryController;
				obj = ((inventoryController == null) ? null : inventoryController.Inventory?.Equipment);
			}
			InventoryEquipment val = (InventoryEquipment)obj;
			if (val == null)
			{
				return -1;
			}
			ThrowWeap topPriorityGrenade = val.TopPriorityGrenade;
			if (topPriorityGrenade == null)
			{
				return -1;
			}
			string stringTemplateId = ((Item)topPriorityGrenade).StringTemplateId;
			for (int i = 0; i < _displayedGrenades.Count; i++)
			{
				GroupedGrenade groupedGrenade = _displayedGrenades[i];
				if (groupedGrenade.FirstItem == topPriorityGrenade)
				{
					return i;
				}
				if (!string.IsNullOrEmpty(stringTemplateId) && groupedGrenade.TemplateId == stringTemplateId)
				{
					return i;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.log.LogWarning((object)("[GrenadeWheel] 读取偏好手雷失败: " + ex.Message));
		}
		return -1;
	}

	protected override void ScanItems()
	{
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		Plugin.log.LogInfo((object)"[GrenadeWheel] 使用 GetThrowablePriorityGrenadesList 扫描手雷...");
		List<ThrowWeap> list = new List<ThrowWeap>();
		try
		{
			List<ThrowWeap> throwablePriorityGrenadesList = InventoryExtension.GetThrowablePriorityGrenadesList(_player.InventoryController);
			if (throwablePriorityGrenadesList != null)
			{
				list.AddRange(throwablePriorityGrenadesList);
			}
		}
		catch (Exception ex)
		{
			Plugin.log.LogWarning((object)("[GrenadeWheel] GetThrowablePriorityGrenadesList 异常: " + ex.Message + "，使用后备扫描模式"));
			try
			{
				InventoryEquipment val = _player.Inventory?.Equipment;
				if (val != null)
				{
					foreach (EquipmentSlot value in Enum.GetValues(typeof(EquipmentSlot)))
					{
						Slot slot = val.GetSlot(value);
						Item obj = ((slot != null) ? slot.ContainedItem : null);
						ThrowWeap val3 = (ThrowWeap)(object)((obj is ThrowWeap) ? obj : null);
						if (val3 != null)
						{
							list.Add(val3);
							continue;
						}
						Item obj2 = ((slot != null) ? slot.ContainedItem : null);
						IContainer val4 = (IContainer)(object)((obj2 is IContainer) ? obj2 : null);
						if (val4 == null)
						{
							continue;
						}
						foreach (Item item in val4.Items)
						{
							ThrowWeap val5 = (ThrowWeap)(object)((item is ThrowWeap) ? item : null);
							if (val5 != null)
							{
								list.Add(val5);
							}
						}
					}
				}
			}
			catch (Exception ex2)
			{
				Plugin.log.LogError((object)("[GrenadeWheel] 后备扫描也异常: " + ex2.Message));
			}
		}
		_displayedGrenades.Clear();
		if (list.Count > 0)
		{
			_displayedGrenades.AddRange((from tw in list
				group tw by ((Item)tw).StringTemplateId ?? "unknown").Select(delegate(IGrouping<string, ThrowWeap> g)
			{
				//IL_0065: Unknown result type (might be due to invalid IL or missing references)
				//IL_006a: Unknown result type (might be due to invalid IL or missing references)
				ThrowWeap val6 = g.First();
				return new GroupedGrenade
				{
					TemplateId = g.Key,
					Name = (string.IsNullOrEmpty(((Item)val6).ShortName) ? LocalizationExtensions.Localized(((Item)val6).Name, (string)null) : LocalizationExtensions.Localized(((Item)val6).ShortName, (string)null)),
					FullName = LocalizationExtensions.Localized(((Item)val6).Name, (string)null),
					Count = g.Count(),
					Color = GetGrenadeColor(val6),
					FirstItem = val6
				};
			}));
			foreach (GroupedGrenade displayedGrenade in _displayedGrenades)
			{
				Plugin.log.LogInfo((object)$"[GrenadeWheel] 分组: {displayedGrenade.FullName} x{displayedGrenade.Count}");
			}
		}
		if (_displayedGrenades.Count == 0)
		{
			Plugin.log.LogInfo((object)"[GrenadeWheel] 背包中没有手雷，但轮盘仍会显示");
		}
	}

	private void EquipGrenade(GroupedGrenade group)
	{
		if ((Object)(object)_player == (Object)null || group?.FirstItem == null)
		{
			return;
		}
		try
		{
			PreferredTemplateId = group.TemplateId;
			InventoryController inventoryController = _player.InventoryController;
			InventoryEquipment val = ((inventoryController == null) ? null : inventoryController.Inventory?.Equipment);
			if (val != null)
			{
				val.TopPriorityGrenade = group.FirstItem;
			}
			Plugin.log.LogInfo((object)("[GrenadeWheel] 已设为偏好手雷: " + group.Name));
			bool flag = _player.HandsController is GrenadeHandsController;
			if ((!flag) ? ConfigManager.EquipImmediatelyOnSelect.Value : ConfigManager.SwitchImmediatelyWhenHolding.Value)
			{
				Plugin.log.LogInfo((object)("[GrenadeWheel] 立即" + (flag ? "切换" : "装备") + ": " + group.Name));
				_player.Proceed(group.FirstItem, (Callback<IGrenadeController>)null, true);
			}
			else
			{
				Plugin.log.LogInfo((object)("[GrenadeWheel] 仅设为偏好，松开 G 后再按 G 键" + (flag ? "切换到该手雷" : "掏出")));
			}
		}
		catch (Exception ex)
		{
			Plugin.log.LogError((object)("[GrenadeWheel] 装备失败: " + ex.Message));
		}
	}

	private async void LoadBuiltInIconAsync(string templateId, ThrowWeap item, Image iconImage, TextMeshProUGUI fallbackText)
	{
		if (string.IsNullOrEmpty(templateId) || item == null || (Object)(object)iconImage == (Object)null)
		{
			return;
		}
		Sprite val = await ItemIconCache.GetOrLoadAsync((Item)(object)item, templateId);
		if ((Object)(object)val == (Object)null)
		{
			Plugin.log.LogInfo((object)("[GrenadeWheel] 无内建图标，使用文字显示: " + LocalizationExtensions.Localized(((Item)item).ShortName, (string)null)));
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

	private static Color GetGrenadeColor(ThrowWeap grenade)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_018f: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b1: Unknown result type (might be due to invalid IL or missing references)
		string stringTemplateId = ((Item)grenade).StringTemplateId;
		if (stringTemplateId == null)
		{
			return new Color(0.6f, 0.6f, 0.6f);
		}
		if (stringTemplateId.Contains("5710c24a"))
		{
			return new Color(0.85f, 0.7f, 0.15f);
		}
		if (stringTemplateId.Contains("5448be9a"))
		{
			return new Color(0.5f, 0.65f, 0.3f);
		}
		if (stringTemplateId.Contains("58d3db53"))
		{
			return new Color(0.55f, 0.7f, 0.25f);
		}
		if (stringTemplateId.Contains("5e32f56f"))
		{
			return new Color(0.6f, 0.45f, 0.15f);
		}
		if (stringTemplateId.Contains("5e340dcd"))
		{
			return new Color(0.65f, 0.4f, 0.1f);
		}
		if (stringTemplateId.Contains("617fd91e"))
		{
			return new Color(0.5f, 0.6f, 0.4f);
		}
		if (stringTemplateId.Contains("618a431d"))
		{
			return new Color(0.7f, 0.45f, 0.2f);
		}
		if (stringTemplateId.Contains("617aa4dd"))
		{
			return new Color(0.35f, 0.7f, 0.35f);
		}
		if (stringTemplateId.Contains("619256e5"))
		{
			return new Color(0.9f, 0.85f, 0.6f);
		}
		if (stringTemplateId.Contains("5a2a57cf"))
		{
			return new Color(0.4f, 0.65f, 0.5f);
		}
		if (stringTemplateId.Contains("66dae7cb"))
		{
			return new Color(0.7f, 0.5f, 0.4f);
		}
		if (stringTemplateId.Contains("5a0c2771"))
		{
			return new Color(0.95f, 0.9f, 0.45f);
		}
		return new Color(0.6f, 0.6f, 0.6f);
	}
}
