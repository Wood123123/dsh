using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace FromHell.TacticalSprint;

[BepInPlugin("com.fromhell.tacticalsprint", "FROM HELL - Tactical Sprint", "0.1.1")]
public sealed class Plugin : BaseUnityPlugin
{
	public const string Id = "com.fromhell.tacticalsprint";

	private static Plugin? Self;

	private ConfigEntry<bool> Enabled;

	private ConfigEntry<float> Window;

	private ConfigEntry<float> Speed;

	private ConfigEntry<float> Drain;

	private Harmony? harmony;

	private Player? player;

	private object? hands;

	private object? animator;

	private static readonly MethodInfo GetAnimator = AccessTools.PropertyGetter(typeof(Player), "BodyAnimatorCommon");

	private MethodInfo? setFloat;

	private readonly object[] animArgs = new object[2];

	private float nativeSize;

	private bool wrote;

	private bool ready;

	private Action? removeDrain;

	private readonly SprintGate gate = new SprintGate();

	private ConfigEntry<KeyCode> sprintKey;

	private ConfigEntry<bool> holdMode;

	private ConfigEntry<bool> diagnostics;

	private int inputFrame = -1;

	private float pendingUntil;

	private bool issuing;

	private void Awake()
	{
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Expected O, but got Unknown
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		//IL_0139: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Expected O, but got Unknown
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d0: Expected O, but got Unknown
		//IL_01fc: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Expected O, but got Unknown
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Expected O, but got Unknown
		//IL_0274: Unknown result type (might be due to invalid IL or missing references)
		//IL_0280: Expected O, but got Unknown
		Self = this;
		Enabled = ((BaseUnityPlugin)this).Config.Bind<bool>("General", "Enabled", true, "Enable tactical sprint. Sprint key and toggle/hold mode are controlled by this plugin while holding a firearm.");
		sprintKey = ((BaseUnityPlugin)this).Config.Bind<KeyCode>("Input", "SprintKey", (KeyCode)304, "Physical sprint key used by this plugin.");
		holdMode = ((BaseUnityPlugin)this).Config.Bind<bool>("Input", "HoldToSprint", false, "false: tap toggles sprint. true: hold second tap to keep tactical sprint; release stops.");
		diagnostics = ((BaseUnityPlugin)this).Config.Bind<bool>("Debug", "InputDiagnostics", true, "Log sprint key edges and state changes, not each frame.");
		Window = ((BaseUnityPlugin)this).Config.Bind<float>("Input", "DoubleTapSeconds", 0.4f, new ConfigDescription("Maximum interval between sprint presses.", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.15f, 0.6f), Array.Empty<object>()));
		Speed = ((BaseUnityPlugin)this).Config.Bind<float>("Movement", "SpeedMultiplier", 1.25f, new ConfigDescription("Tactical sprint target speed multiplier.", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 2f), Array.Empty<object>()));
		Drain = ((BaseUnityPlugin)this).Config.Bind<float>("Movement", "StaminaMultiplier", 2f, new ConfigDescription("Tactical sprint leg-stamina drain relative to vanilla sprint. Does not scale other drains.", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 5f), Array.Empty<object>()));
		if (typeof(Player).Module.ModuleVersionId != new Guid("ad47400a-25db-462c-bf0a-531df654c3c3"))
		{
			((BaseUnityPlugin)this).Logger.LogError((object)"Unsupported client build. No patches installed.");
			return;
		}
		if (AppDomain.CurrentDomain.GetAssemblies().Any((Assembly a) => a.GetName().Name == "西欧弟式持枪"))
		{
			((BaseUnityPlugin)this).Logger.LogError((object)"Remove 西欧弟式持枪.dll and restart: it globally forces the raised animation. Tactical Sprint disabled.");
			return;
		}
		try
		{
			harmony = new Harmony("com.fromhell.tacticalsprint");
			harmony.Patch((MethodBase)AccessTools.Method(typeof(Player), "ToggleSprint", (Type[])null, (Type[])null), new HarmonyMethod(typeof(Plugin), "OnToggle", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)AccessTools.Method(typeof(Class1728), "TranslateCommand", (Type[])null, (Type[])null), new HarmonyMethod(typeof(Plugin), "OnCommand", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			harmony.Patch((MethodBase)AccessTools.Method(typeof(MovementContext), "SprintAcceleration", (Type[])null, (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(Plugin), "SpeedPatch", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null);
			ready = true;
			((BaseUnityPlugin)this).Logger.LogInfo((object)"Tactical Sprint 0.1.1 loaded. Physical-key double tap; plugin HoldToSprint controls mode. InputDiagnostics enabled by default.");
			((BaseUnityPlugin)this).Logger.LogInfo((object)"制作由[来自地狱]死神少爷是只兔");
		}
		catch (Exception ex)
		{
			Harmony? obj = harmony;
			if (obj != null)
			{
				obj.UnpatchSelf();
			}
			((BaseUnityPlugin)this).Logger.LogError((object)ex);
		}
	}

	private bool Eligible(Player p)
	{
		if (ready && ((Behaviour)this).isActiveAndEnabled && Enabled.Value && p.IsYourPlayer && p.HandsController is FirearmController && !p.IsInventoryOpened)
		{
			return p.HealthController.IsAlive;
		}
		return false;
	}

	private bool Tactical(Player p)
	{
		if (player == p && gate.Active && Eligible(p))
		{
			return p.Physical.Sprinting;
		}
		return false;
	}

	private void Bind(Player p)
	{
		if (player != p)
		{
			Clear();
			player = p;
			hands = p.HandsController;
		}
	}

	private void Trace(string message)
	{
		if (diagnostics.Value)
		{
			((BaseUnityPlugin)this).Logger.LogInfo((object)("Input: " + message));
		}
	}

	private void Issue(Player p, bool value)
	{
		issuing = true;
		try
		{
			p.EnableSprint(value);
		}
		finally
		{
			issuing = false;
		}
	}

	private void ReadInput(Player p)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		if (inputFrame == Time.frameCount)
		{
			return;
		}
		inputFrame = Time.frameCount;
		if (Input.GetKeyDown(sprintKey.Value))
		{
			bool num = gate.Press(Time.unscaledTime, Window.Value, p.Physical.Sprinting);
			bool value = num || holdMode.Value || !p.Physical.Sprinting;
			if (num)
			{
				pendingUntil = Time.unscaledTime + 0.45f;
			}
			else
			{
				pendingUntil = 0f;
			}
			Issue(p, value);
			Trace("down; tactical=" + gate.Active + " requestSprint=" + value + " sprinting=" + p.Physical.Sprinting + " hold=" + holdMode.Value + " playerType=" + ((object)p).GetType().FullName);
		}
		if (holdMode.Value && Input.GetKeyUp(sprintKey.Value))
		{
			gate.Stop();
			pendingUntil = 0f;
			Issue(p, value: false);
			Trace("release -> stop (hold mode)");
		}
	}

	private void Update()
	{
		if (ready)
		{
			GameWorld instance = Singleton<GameWorld>.Instance;
			Player val = (((Object)(object)instance != (Object)null) ? instance.MainPlayer : null);
			if (!((Object)(object)val == (Object)null) && Eligible(val))
			{
				Bind(val);
				ReadInput(val);
			}
		}
	}

	private static bool OnToggle(Player __instance)
	{
		Plugin self = Self;
		if ((Object)(object)self == (Object)null || self.issuing || !self.Eligible(__instance))
		{
			return true;
		}
		self.Bind(__instance);
		self.ReadInput(__instance);
		self.Trace("suppressed vanilla ToggleSprint");
		return false;
	}

	private unsafe static bool OnCommand(Class1728 __instance, ECommand command, ref ETranslateResult __result)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Invalid comparison between Unknown and I4
		Plugin self = Self;
		Player player_ = __instance.Player_0;
		if ((Object)(object)self == (Object)null || (Object)(object)player_ == (Object)null || !self.Eligible(player_))
		{
			return true;
		}
		if ((int)command != 23 && (int)command != 24)
		{
			return true;
		}
		self.Bind(player_);
		self.ReadInput(player_);
		__result = (ETranslateResult)0;
		self.Trace("suppressed command=" + ((object)(*(ECommand*)(&command))/*cast due to constrained. prefix*/).ToString());
		return false;
	}

	private void LateUpdate()
	{
		//IL_0141: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		if (!ready)
		{
			return;
		}
		GameWorld instance = Singleton<GameWorld>.Instance;
		Player val = (((Object)(object)instance != (Object)null) ? instance.MainPlayer : null);
		if ((Object)(object)val == (Object)null)
		{
			Clear();
			return;
		}
		Bind(val);
		if (!Eligible(val))
		{
			gate.Reset();
			SyncDrain();
			Restore();
			return;
		}
		if (hands != val.HandsController)
		{
			gate.Reset();
			Restore();
			hands = val.HandsController;
		}
		if (!val.Physical.Sprinting && Time.unscaledTime >= pendingUntil)
		{
			if (gate.Active)
			{
				Trace("tactical ended: not sprinting");
			}
			gate.Stop();
		}
		SyncDrain();
		if (!val.Physical.Sprinting)
		{
			Restore();
			return;
		}
		object obj = GetAnimator.Invoke(val, null);
		if (obj != null)
		{
			if (animator != obj)
			{
				Restore();
				animator = obj;
				setFloat = AccessTools.Method(obj.GetType(), "SetFloat", new Type[2]
				{
					typeof(int),
					typeof(float)
				}, (Type[])null);
			}
			nativeSize = ((Item)((FirearmController)val.HandsController).Item).CalculateCellSize().X;
			WriteSize(gate.Active ? 2f : Math.Max(3f, nativeSize));
			wrote = true;
		}
	}

	private void SyncDrain()
	{
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		if ((Object)(object)player == (Object)null || !Tactical(player))
		{
			removeDrain?.Invoke();
			removeDrain = null;
		}
		else
		{
			if (removeDrain != null)
			{
				return;
			}
			Player p = player;
			BasePhysicalClass physical = p.Physical;
			PlayerPhysicalClass val = (PlayerPhysicalClass)(object)((physical is PlayerPhysicalClass) ? physical : null);
			if (val != null && val.Consumptions.TryGetValue((EConsumptionType)1, out var baseline))
			{
				GClass773 val2 = new GClass773((EConsumptionType)1)
				{
					Delta = new GClass848<float>((Func<float>)(() => (!Tactical(p)) ? 0f : (Math.Max(0f, baseline.Delta.Value) * (Safe(Drain.Value, 1f, 5f) - 1f)))),
					AllowsRestoration = false,
					Downtime = baseline.Downtime
				};
				removeDrain = ((BasePhysicalClass)val).Stamina.AddConsumption(val2);
			}
		}
	}

	private void WriteSize(float value)
	{
		animArgs[0] = PlayerAnimator.WEAPON_SIZE_MODIFIER_PARAM_HASH;
		animArgs[1] = value;
		setFloat?.Invoke(animator, animArgs);
	}

	private void Restore()
	{
		if (wrote && animator != null && (Object)(object)player != (Object)null && hands == player.HandsController)
		{
			WriteSize(nativeSize);
		}
		wrote = false;
		animator = null;
	}

	private void Clear()
	{
		pendingUntil = 0f;
		inputFrame = -1;
		gate.Reset();
		removeDrain?.Invoke();
		removeDrain = null;
		Restore();
		hands = null;
		player = null;
	}

	private void OnDisable()
	{
		Clear();
	}

	private void OnDestroy()
	{
		ready = false;
		Clear();
		Harmony? obj = harmony;
		if (obj != null)
		{
			obj.UnpatchSelf();
		}
		if (Self == this)
		{
			Self = null;
		}
	}

	private static float Safe(float f, float min, float max)
	{
		if (!float.IsNaN(f) && !float.IsInfinity(f))
		{
			return Math.Max(min, Math.Min(max, f));
		}
		return min;
	}

	private static float Target(float original, Player p)
	{
		if (!((Object)(object)Self != (Object)null) || !Self.Tactical(p))
		{
			return original;
		}
		return original * Safe(Self.Speed.Value, 1f, 2f);
	}

	private static IEnumerable<CodeInstruction> SpeedPatch(IEnumerable<CodeInstruction> instructions)
	{
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Expected O, but got Unknown
		//IL_00ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Expected O, but got Unknown
		List<CodeInstruction> code = instructions.ToList();
		MethodInfo getter = AccessTools.PropertyGetter(typeof(MovementContext), "StateSprintSpeedLimit");
		int[] array = (from i in Enumerable.Range(0, Math.Max(0, code.Count - 2))
			where CodeInstructionExtensions.Calls(code[i], getter) && code[i + 1].opcode == OpCodes.Mul && code[i + 2].opcode == OpCodes.Stloc_1
			select i).ToArray();
		if (array.Length != 1)
		{
			throw new InvalidOperationException("Sprint target expression changed; refusing patch.");
		}
		code.InsertRange(array[0] + 2, (IEnumerable<CodeInstruction>)(object)new CodeInstruction[3]
		{
			new CodeInstruction(OpCodes.Ldarg_0, (object)null),
			new CodeInstruction(OpCodes.Ldfld, (object)AccessTools.Field(typeof(MovementContext), "_player")),
			new CodeInstruction(OpCodes.Call, (object)AccessTools.Method(typeof(Plugin), "Target", (Type[])null, (Type[])null))
		});
		return code;
	}
}
