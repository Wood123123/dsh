using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.LanguageExtensions;
using Diz.Utils;
using EFT;
using EFT.Communications;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using JsonType;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Components;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Patches;

internal class GetActionsPatch : ModulePatch
{
	private static ConditionProgressChecker _activeSalvageChecker;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.FirstMethod(typeof(InteractionContextHelper), (Func<MethodInfo, bool>)((MethodInfo x) => x.Name == "GetAvailableActions" && x.GetParameters()[0].Name == "owner"));
	}

	[PatchPrefix]
	public static bool PatchPrefix(object[] __args, ref AvailableInteractionState __result)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Expected O, but got Unknown
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Expected O, but got Unknown
		GamePlayerOwner owner = (GamePlayerOwner)__args[0];
		object obj = __args[1];
		SalvageItemTrigger salvageTrigger = obj as SalvageItemTrigger;
		if (salvageTrigger == null)
		{
			return true;
		}
		Player player = ((PlayerOwner)owner).Player;
		InventoryController inventoryController = player.InventoryController;
		if (inventoryController == null)
		{
			LogHelper.LogError("InventoryController is null");
			return true;
		}
		QuestController questController = player.QuestController;
		QuestBook val = ((questController != null) ? questController.Quests : null);
		if (val == null)
		{
			__result = null;
			return true;
		}
		_activeSalvageChecker = null;
		ConditionProgressChecker val2 = null;
		foreach (ConditionProgressChecker item in val.GetConditionHandlersByZone<ConditionZone>(((TriggerWithId)salvageTrigger).Id))
		{
			if (item.Condition is ConditionSalvage && item.CurrentValue < (double)item.Condition.value)
			{
				val2 = item;
				break;
			}
		}
		if (val2 == null)
		{
			__result = null;
			return false;
		}
		_activeSalvageChecker = val2;
		List<InteractionAction> actions = new List<InteractionAction>
		{
			new InteractionAction
			{
				Name = LocalizationExtensions.Localized("SALVAGE_ZONE", (string)null),
				Action = delegate
				{
					StartSalvage(owner, salvageTrigger, inventoryController);
				}
			}
		};
		__result = new AvailableInteractionState
		{
			Actions = actions
		};
		return false;
	}

	private static void StartSalvage(GamePlayerOwner owner, SalvageItemTrigger trigger, InventoryController inventoryController)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		Player player = ((PlayerOwner)owner).Player;
		Item[] source = player.Inventory.GetPlayerItems((EPlayerItems)6).ToArray();
		MongoID requiredTpl = MongoID.op_Implicit(trigger.RequiredItemTpl);
		Item requiredItem = source.FirstOrDefault(delegate(Item i)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0007: Unknown result type (might be due to invalid IL or missing references)
			return i.TemplateId == requiredTpl;
		});
		if (requiredItem == null)
		{
			string arg = LocalizationExtensions.LocalizedName(requiredTpl);
			NotificationManager.DisplaySingletonWarningNotification(string.Format(LocalizationExtensions.Localized("YOU_NEED_ITEM_TO_SALVAGE", (string)null), arg), (ENotificationDurationType)0);
			return;
		}
		if (!(player.CurrentState is IdlePlayerState))
		{
			NotificationManager.DisplaySingletonWarningNotification(LocalizationExtensions.Localized("CANT_SALVAGE_WHILE_MOVING", (string)null), (ENotificationDurationType)0);
			return;
		}
		owner.ShowObjectivesPanel("SALVAGING_OBJECTIVE", trigger.SalvageTime);
		MovementState currentManagedState = player.CurrentManagedState;
		bool flag = true;
		bool flag2 = false;
		float salvageTime = trigger.SalvageTime;
		currentManagedState.Plant(flag, flag2, salvageTime, (Action<bool>)OnPlantComplete);
		async void OnPlantComplete(bool success)
		{
			player.PlantItem(MongoID.op_Implicit(requiredItem.TemplateId), ((TriggerWithId)trigger).Id, success);
			owner.CloseObjectivesPanel();
			if (!success)
			{
				owner.ClearInteractionState();
			}
			else
			{
				owner.ClearInteractionState();
				await ApplySalvageAsync(trigger, requiredItem, inventoryController, owner);
			}
		}
	}

	private static async Task ApplySalvageAsync(SalvageItemTrigger trigger, Item requiredItem, InventoryController inventoryController, GamePlayerOwner owner)
	{
		_activeSalvageChecker = null;
		Player player = ((PlayerOwner)owner).Player;
		if ((Object)(object)ItemUiContext.Instance == (Object)null)
		{
			LogHelper.LogError("ItemUiContext.Instance is null");
			return;
		}
		if (trigger.ConsumeRequiredItem)
		{
			try
			{
				OperationResult<RemoveResult> val = ItemManipulator.Remove(requiredItem, (ItemController)(object)inventoryController, true);
				if (val.Failed)
				{
					LogHelper.LogError($"Failed to remove required item: {val.Error}");
					return;
				}
				if (!(await ((ItemController)inventoryController).TryRunNetworkTransaction(OperationResult<RemoveResult>.op_Implicit(val), (Callback)null)).Succeed)
				{
					LogHelper.LogError("Network transaction for removing required item failed");
					return;
				}
			}
			catch (Exception arg)
			{
				LogHelper.LogError($"Exception removing required item: {arg}");
				return;
			}
		}
		MongoID id = default(MongoID);
		MongoID value = default(MongoID);
		foreach (SalvageItemTrigger.SalvageReward reward in trigger.Rewards)
		{
			for (int i = 0; i < reward.Count; i++)
			{
				try
				{
					ItemFactory instance = Singleton<ItemFactory>.Instance;
					if (instance == null)
					{
						LogHelper.LogError("ItemFactoryClass.Instance is null");
						return;
					}
					Stash val2 = instance.CreateFakeStash((MongoID?)null);
					Grid val3 = new Grid("salvage fake stash", 5, 5, false, Array.Empty<ItemFilter>(), (CompoundItem)(object)val2);
					((CompoundItem)val2).Grids[0] = val3;
					((Item)val2).CurrentAddress = ((ItemController)inventoryController).CreateItemAddress();
					((MongoID)(ref id))._002Ector(MongoID.op_Implicit(((IDatabaseIdGenerator)inventoryController).NextId));
					((MongoID)(ref value))._002Ector(((Item)val2).Id);
					FlatItem val4 = new FlatItem
					{
						_id = id,
						_tpl = new MongoID(reward.ItemTpl),
						parentId = value,
						slotId = val3.ID,
						location = null,
						upd = null
					};
					FlatItem[] array = (FlatItem[])(object)new FlatItem[1] { val4 };
					Singleton<ItemFactory>.Instance.FlatItemsToTree(array, false, new Dictionary<string, Item> { 
					{
						((Item)val2).Id,
						(Item)(object)val2
					} });
					Item[] array2 = val3.Items.ToArray();
					foreach (Item val5 in array2)
					{
						val5.SpawnedInSession = true;
						IEnumerable<CompoundItem> enumerable;
						if (!reward.ToQuestInventory)
						{
							enumerable = LinqExtensions.ToEnumerable<InventoryEquipment>(inventoryController.Inventory.Equipment).OfType<CompoundItem>();
						}
						else
						{
							IEnumerable<CompoundItem> enumerable2 = (IEnumerable<CompoundItem>)((IEnumerable<Stash>)(object)new Stash[1] { inventoryController.Inventory.QuestRaidItems }).Where((Stash c) => c != null);
							enumerable = enumerable2;
						}
						IEnumerable<CompoundItem> enumerable3 = enumerable;
						if (!enumerable3.Any())
						{
							LogHelper.LogError("No in-raid targets for salvage reward " + reward.ItemTpl);
							continue;
						}
						OperationResult<IItemOperationResult> val6 = ItemManipulator.QuickFindAppropriatePlace(val5, (ItemController)(object)inventoryController, enumerable3, (EMoveItemOrder)51, true);
						if (val6.Failed)
						{
							LogHelper.LogError($"QuickFind failed for salvage reward {reward.ItemTpl}: {val6.Error}");
						}
						else if (!(await ((ItemController)inventoryController).TryRunNetworkTransaction(OperationResult<IItemOperationResult>.op_Implicit(val6), (Callback)null)).Succeed)
						{
							LogHelper.LogError("Network transaction failed for salvage reward " + reward.ItemTpl);
						}
					}
				}
				catch (Exception arg2)
				{
					LogHelper.LogError($"Error preparing salvage reward {reward.ItemTpl}: {arg2}");
				}
			}
		}
		try
		{
			player.RemoveTriggerZone((TriggerWithId)(object)trigger);
			SalvageZoneTracker.Clear(player, trigger);
		}
		catch (Exception arg3)
		{
			LogHelper.LogError($"Error removing salvage trigger: {arg3}");
		}
		finally
		{
			_activeSalvageChecker = null;
		}
	}
}
