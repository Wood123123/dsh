using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Diz.Binding;
using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using Newtonsoft.Json;
using StashTools.History;
using UnityEngine;

namespace StashTools;

internal static class HistoryCapture
{
	internal static bool Enabled = true;

	private static RaidRecord? _record;

	private static readonly Dictionary<string, HistoryItem> Known = new Dictionary<string, HistoryItem>();

	private static readonly Dictionary<string, string> Quests = new Dictionary<string, string>();

	private static readonly Dictionary<string, int> Achievements = new Dictionary<string, int>();

	private static readonly HashSet<string> Victims = new HashSet<string>();

	private static readonly List<HistoryItem> Baseline = new List<HistoryItem>();

	private static float _saveAt;

	private static bool _pending;

	private static bool _segmentOpen;

	internal static void AddPause(string profile, double seconds)
	{
		if (_record != null && !(_record.Profile != profile) && !_record.Final && double.IsFinite(seconds) && !(seconds <= 0.0))
		{
			_record.PausedSeconds += seconds;
		}
	}

	internal static void Guard(Action action)
	{
		if (!Enabled)
		{
			return;
		}
		try
		{
			action();
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("战绩采集失败，其他功能不受影响：" + ex));
		}
	}

	internal static void Begin(Player player, bool resume)
	{
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		if (!resume || _record == null)
		{
			_record = new RaidRecord
			{
				Id = Guid.NewGuid().ToString("N"),
				Profile = player.ProfileId,
				Name = player.Profile.Info.Nickname,
				Side = ((object)player.Side/*cast due to constrained. prefix*/).ToString(),
				StartedUtc = DateTime.UtcNow
			};
			Known.Clear();
			Victims.Clear();
			Baseline.Clear();
			Quests.Clear();
			Achievements.Clear();
			Guard(delegate
			{
				ReadTasks(player.Profile, compare: false);
			});
			Guard(delegate
			{
				ReadVictims(player, collect: false);
			});
			Guard(delegate
			{
				UpdateTeam();
			});
		}
		_record.Route.Add(player.Location);
		_record.Events.Add("进入 " + player.Location);
		ReadEquipment((Item)(object)player.Inventory.Equipment, "随身");
		if (!resume)
		{
			Baseline.AddRange(Known.Values.Where((HistoryItem i) => LoadoutValue.IncludeKnife || i.Slot != "Scabbard").Select(Clone));
		}
		_pending = false;
		_segmentOpen = true;
	}

	internal static void UpdateTeam()
	{
		if (_record == null || _record.Final)
		{
			return;
		}
		CoopRaidContext coopRaidContext = HistoryCoopBridge.Capture(_record.Profile);
		if (coopRaidContext == null)
		{
			return;
		}
		if (_record.Team != null)
		{
			if (_record.Team.Provider != coopRaidContext.Provider || _record.Team.ActionId != coopRaidContext.ActionId)
			{
				return;
			}
			foreach (TeamMemberIdentity old in _record.Team.Members)
			{
				if (!coopRaidContext.Members.Any((TeamMemberIdentity m) => m.Profile == old.Profile))
				{
					coopRaidContext.Members.Add(old);
				}
			}
		}
		_record.Team = TeamRules.Copy(coopRaidContext);
	}

	private static HistoryItem Clone(HistoryItem item)
	{
		return JsonConvert.DeserializeObject<HistoryItem>(JsonConvert.SerializeObject((object)item));
	}

	private static void ReadEquipment(Item root, string destination)
	{
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cc: Invalid comparison between Unknown and I4
		Stack<(Item, bool)> stack = new Stack<(Item, bool)>();
		stack.Push((root, false));
		HashSet<string> hashSet = new HashSet<string>();
		while (stack.Count > 0)
		{
			var (val, flag) = stack.Pop();
			if (!hashSet.Add(val.Id))
			{
				continue;
			}
			ContainerCollection val2 = val.TryCast<ContainerCollection>();
			if (val.Id != root.Id)
			{
				ItemAddress parent = val.Parent;
				GridItemAddress val3 = parent?.TryCast<GridItemAddress>();
				IntVec2 val4 = val.CalculateCellSize();
				object obj;
				if (parent == null)
				{
					obj = null;
				}
				else
				{
					IContainer container = parent.Container;
					if (container == null)
					{
						obj = null;
					}
					else
					{
						Slot? obj2 = container.TryCast<Slot>();
						obj = ((obj2 != null) ? obj2.ID : null);
					}
				}
				if (obj == null)
				{
					obj = ((parent != null) ? parent.ContainerName : null) ?? "";
				}
				string text = (string)obj;
				object obj3;
				if (parent == null)
				{
					obj3 = null;
				}
				else
				{
					IContainer container2 = parent.Container;
					if (container2 == null)
					{
						obj3 = null;
					}
					else
					{
						Item parentItem = container2.ParentItem;
						obj3 = ((parentItem != null) ? parentItem.Id : null);
					}
				}
				bool flag2 = (string?)obj3 == root.Id && (text == "SecuredContainer" || text == "Pockets" || text == "Dogtag");
				bool flag3 = parent?.Container != null && ArmorValuation.FixedSlot(parent.Container);
				HistoryItem obj4 = new HistoryItem
				{
					Id = val.Id,
					Template = val.StringTemplateId,
					PriceKey = PriceBook.Key(val),
					Name = LocalizationExtensions.LocalizedName(val)
				};
				object obj5;
				if (parent == null)
				{
					obj5 = null;
				}
				else
				{
					IContainer container3 = parent.Container;
					if (container3 == null)
					{
						obj5 = null;
					}
					else
					{
						Item parentItem2 = container3.ParentItem;
						obj5 = ((parentItem2 != null) ? parentItem2.Id : null);
					}
				}
				if (obj5 == null)
				{
					obj5 = "";
				}
				obj4.Parent = (string)obj5;
				obj4.Slot = text;
				obj4.Width = Math.Max(1, val4.X);
				obj4.Height = Math.Max(1, val4.Y);
				obj4.Count = Math.Max(0, val.StackObjectsCount);
				obj4.Units = ItemValuation.Units(val);
				int? obj6;
				if (val3 == null)
				{
					obj6 = null;
				}
				else
				{
					Grid grid = val3.Grid;
					obj6 = ((grid != null) ? new int?(grid.GridWidth) : ((int?)null));
				}
				int? num = obj6;
				obj4.GridWidth = num.GetValueOrDefault();
				int? obj7;
				if (val3 == null)
				{
					obj7 = null;
				}
				else
				{
					Grid grid2 = val3.Grid;
					obj7 = ((grid2 != null) ? new int?(grid2.GridHeight) : ((int?)null));
				}
				num = obj7;
				obj4.GridHeight = num.GetValueOrDefault();
				obj4.Storage = val3 != null;
				obj4.X = val3?.LocationInGrid.x ?? 0;
				obj4.Y = val3?.LocationInGrid.y ?? 0;
				obj4.Rotated = val3 != null && (int)val3.LocationInGrid.r > 0;
				obj4.Destination = destination;
				obj4.Valued = flag && !flag2 && !flag3;
				HistoryItem historyItem = obj4;
				if (historyItem.Valued)
				{
					RaidPrices.Register(val);
				}
				double? num2 = RaidPrices.Get(PriceBook.Key(val));
				historyItem.Price = num2.GetValueOrDefault();
				historyItem.Priced = num2.HasValue && num2 > 0.0;
				Known[val.Id] = historyItem;
			}
			if (val2 == null)
			{
				continue;
			}
			foreach (IContainer container4 in val2.Containers)
			{
				foreach (Item item in container4.Items)
				{
					stack.Push((item, !ArmorValuation.FixedSlot(container4)));
				}
			}
		}
	}

	internal static void Transfer(Item root)
	{
		ReadEquipment(root, "已送出（待确认）");
	}

	internal unsafe static void Finish(Player player, ExitStatus status)
	{
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Invalid comparison between Unknown and I4
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Invalid comparison between Unknown and I4
		if (_record != null && _segmentOpen)
		{
			Guard(UpdateTeam);
			Guard(delegate
			{
				ReadEquipment((Item)(object)player.Inventory.Equipment, "随身");
			});
			Guard(delegate
			{
				ReadVictims(player);
			});
			Guard(delegate
			{
				ReadTasks(player.Profile, compare: true);
			});
			_record.Status = ((object)(*(ExitStatus*)(&status))/*cast due to constrained. prefix*/).ToString();
			_record.EndedUtc = DateTime.UtcNow;
			_record.Final = (int)status != 5;
			_record.Events.Add(((int)status == 5) ? "转移地图，连续记录" : ("行动结束：" + HistoryUi.Status(((object)(*(ExitStatus*)(&status))/*cast due to constrained. prefix*/).ToString())));
			_pending = true;
			_saveAt = Time.unscaledTime + 8f;
			_segmentOpen = false;
		}
	}

	internal static void Interrupted(Player? player, string reason, bool readInventory)
	{
		if (_record == null || !_segmentOpen)
		{
			return;
		}
		if (((Object)(object)player != (Object)null) & readInventory)
		{
			Guard(UpdateTeam);
			Guard(delegate
			{
				ReadEquipment((Item)(object)player.Inventory.Equipment, "结果未确认");
			});
			Guard(delegate
			{
				ReadVictims(player);
			});
			Guard(delegate
			{
				ReadTasks(player.Profile, compare: true);
			});
		}
		foreach (HistoryItem value in Known.Values)
		{
			if (!value.Destination.StartsWith("已送出", StringComparison.Ordinal))
			{
				value.Destination = "结果未确认";
			}
		}
		_record.Status = "结果未确认";
		_record.EndedUtc = DateTime.UtcNow;
		_record.Final = true;
		_record.Events.Add("未收到正常结束事件：" + reason);
		_segmentOpen = false;
		_pending = true;
		_saveAt = Time.unscaledTime;
		Guard(Persist);
	}

	private static void ReadVictims(Player player, bool collect = true)
	{
		foreach (VictimStats item in (BindableList<VictimStats>)(object)player.Profile.EftStats.Victims)
		{
			string text = player.Location + "/" + item.ProfileId + "/" + item.Time.TotalSeconds;
			if (Victims.Add(text) && collect)
			{
				_record.Kills.Add(new HistoryKill
				{
					Key = text,
					Name = item.Name,
					Side = ((object)Unsafe.As<EPlayerSide, EPlayerSide>(ref item.Side)/*cast due to constrained. prefix*/).ToString(),
					Role = ((object)Unsafe.As<WildSpawnType, WildSpawnType>(ref item.Role)/*cast due to constrained. prefix*/).ToString(),
					Level = item.Level,
					Seconds = item.Time.TotalSeconds,
					Weapon = item.Weapon,
					Distance = item.Distance,
					Profile = item.ProfileId,
					IsAi = HistoryCoopBridge.VictimIdentity(item.ProfileId)
				});
			}
		}
	}

	internal static void Reconcile(Profile profile)
	{
		if (_record == null || profile.Id != _record.Profile || !_record.Final)
		{
			return;
		}
		foreach (HistoryItem item in Known.Values.Where((HistoryItem e) => !e.Destination.StartsWith("已送出")))
		{
			item.Destination = "遗失或已消耗";
		}
		ReadEquipment((Item)(object)profile.Inventory.Equipment, "随身带出");
		_record.Reconciled = true;
		Guard(delegate
		{
			ReadTasks(profile, compare: true);
		});
		_pending = true;
		_saveAt = Time.unscaledTime + 3f;
		Guard(delegate
		{
			Persist();
		});
	}

	private static void ReadTasks(Profile profile, bool compare)
	{
		//IL_01da: Unknown result type (might be due to invalid IL or missing references)
		//IL_01df: Unknown result type (might be due to invalid IL or missing references)
		foreach (QuestDataClass questsDatum in profile.QuestsData)
		{
			string text = ((object)Unsafe.As<EQuestStatus, EQuestStatus>(ref questsDatum.Status)/*cast due to constrained. prefix*/).ToString() + "/" + questsDatum.CompletedConditions.Count;
			if (compare && Quests.TryGetValue(questsDatum.Id, out string value) && text != value)
			{
				string text2 = ((object)Unsafe.As<EQuestStatus, EQuestStatus>(ref questsDatum.Status)/*cast due to constrained. prefix*/).ToString() switch
				{
					"Success" => "已完成", 
					"AvailableForFinish" => "可交付", 
					"Started" => "进行中", 
					"Fail" => "失败", 
					"AvailableForStart" => "可接受", 
					"Locked" => "未解锁", 
					_ => ((object)Unsafe.As<EQuestStatus, EQuestStatus>(ref questsDatum.Status)/*cast due to constrained. prefix*/).ToString(), 
				};
				List<string> tasks = _record.Tasks;
				string[] array = new string[5];
				QuestTemplate template = questsDatum.Template;
				array[0] = LocalizationExtensions.Localized(((template != null) ? template.Name : null) ?? questsDatum.Id, (string)null);
				array[1] = ": ";
				array[2] = text2;
				array[3] = "，完成条件 ";
				array[4] = questsDatum.CompletedConditions.Count.ToString();
				tasks.Add(string.Concat(array));
			}
			Quests[questsDatum.Id] = text;
		}
		foreach (KeyValuePair<MongoID, int> achievementsDatum in profile.AchievementsData)
		{
			string text3 = ((object)achievementsDatum.Key/*cast due to constrained. prefix*/).ToString();
			if (compare && achievementsDatum.Value > Achievements.GetValueOrDefault(text3))
			{
				_record.Achievements.Add(text3);
			}
			Achievements[text3] = achievementsDatum.Value;
		}
	}

	internal static void Tick()
	{
		if (!Enabled || !_pending || _record == null || Time.unscaledTime < _saveAt)
		{
			return;
		}
		Guard(delegate
		{
			if (RaidEarnings.Totals.Pending <= 0 || !(Time.unscaledTime < _saveAt + 20f))
			{
				Persist();
				_pending = false;
			}
		});
	}

	internal static void Persist()
	{
		if (_record == null || _record.EndedUtc == default(DateTime))
		{
			return;
		}
		RaidLedger.Totals totals = RaidEarnings.Totals;
		_record.Income = totals.Income;
		_record.Expense = totals.Expense;
		_record.MissingPrices = totals.Pending + totals.Unpriced;
		_record.Loadout = Baseline.Where((HistoryItem i) => i.Valued).Sum((HistoryItem i) => (RaidPrices.Get(i.PriceKey) ?? i.Price) * i.Units);
		foreach (HistoryItem value in Known.Values)
		{
			(bool, double) tuple = RaidEarnings.Ledger.A(value.Id);
			value.BroughtUnits = tuple.Item2;
			if (_record.Status != "结果未确认" && value.Valued && !tuple.Item1 && !value.Destination.StartsWith("已送出"))
			{
				value.Destination = "遗失或已消耗";
			}
			double? num = RaidPrices.Get(value.PriceKey);
			if (num.HasValue)
			{
				value.Price = num.Value;
				value.Priced = num > 0.0;
			}
		}
		_record.Items = Known.Values.Select(Clone).ToList();
		_record.Revision++;
		HistoryNetwork.Enqueue(_record);
		Plugin.Logger.LogInfo((object)("对局记录已写入本地待上传：" + _record.Id + "，状态=" + _record.Status + "，版本=" + _record.Revision));
	}
}
