using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using EFT;
using EFT.InventoryLogic;
using Newtonsoft.Json;
using SPT.Reflection.Patching;
using UnityEngine;

namespace RAID_REVIEW;

public class RAID_REVIEW_Player_OnItemAddedOrRemovedPatch : ModulePatch
{
	private static readonly ConcurrentDictionary<string, TrackingLootItem> _pendingRemoves = new ConcurrentDictionary<string, TrackingLootItem>();

	protected override MethodBase GetTargetMethod()
	{
		return typeof(Player).GetMethod("OnItemAddedOrRemoved", BindingFlags.Instance | BindingFlags.Public);
	}

	[PatchPostfix]
	private static void PatchPostFix(ref Player __instance, Item item, ItemAddress location, bool added)
	{
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		if (__instance.Location == "hideout")
		{
			return;
		}
		try
		{
			bool flag = location.Container.ID == "cartridges";
			if (!RAID_REVIEW.LootTracking.Value || flag)
			{
				return;
			}
			Vector3 position = __instance.Position;
			TrackingLootItem trackingLootItem = new TrackingLootItem
			{
				sessionId = RAID_REVIEW.sessionId,
				profileId = __instance.ProfileId,
				time = RAID_REVIEW.stopwatch.ElapsedMilliseconds,
				itemId = item.Id,
				templateId = ((object)item.TemplateId/*cast due to constrained. prefix*/).ToString(),
				itemName = LocalizationExtensions.LocalizedShortName(item),
				price = RAID_REVIEW.GetHandbookPrice(item),
				qty = item.StackObjectsCount,
				type = (item.QuestItem ? "QUEST_ITEM" : "LOOT"),
				added = added,
				x = position.x,
				y = position.y,
				z = position.z
			};
			TrackingLootItem value;
			if (!added)
			{
				_pendingRemoves[item.Id] = trackingLootItem;
				Task.Delay(50).ContinueWith(delegate
				{
					if (_pendingRemoves.TryRemove(item.Id, out var value2))
					{
						Telemetry.Send("LOOT", JsonConvert.SerializeObject((object)value2));
					}
				});
			}
			else if (!_pendingRemoves.TryRemove(item.Id, out value))
			{
				Telemetry.Send("LOOT", JsonConvert.SerializeObject((object)trackingLootItem));
			}
		}
		catch (Exception ex)
		{
			ModulePatch.Logger.LogError((object)(ex.Message ?? ""));
		}
	}
}
