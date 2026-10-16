using System;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace TacticalSprint.Fika;

public class ObservedPlayerTacticalSprintController : MonoBehaviour
{
	private Player player;

	private bool active;

	private object animator;

	private MethodInfo setFloat;

	private MethodInfo getFloat;

	private bool hasSaved;

	private float savedSize;

	private void Awake()
	{
		player = ((Component)this).GetComponent<Player>();
	}

	public void SetActive(bool value)
	{
		if (active == value)
		{
			return;
		}
		active = value;
		ResolveAnimator();
		if (animator != null)
		{
			if (value)
			{
				SaveCurrent();
				Plugin.Log.LogInfo((object)("[TacticalSprint.Fika] 进入战术冲刺，保存原值 " + (hasSaved ? savedSize.ToString("F2") : "取不到(动画器未就绪)，退出时用武器尺寸兜底")));
			}
			else
			{
				float size = (hasSaved ? savedSize : GetNativeSize());
				Write(size);
				Plugin.Log.LogInfo((object)("[TacticalSprint.Fika] 退出战术冲刺，还原 " + size.ToString("F2") + (hasSaved ? "（存的原值）" : "（武器尺寸兜底）")));
			}
		}
	}

	private void LateUpdate()
	{
		if (!active)
		{
			return;
		}
		try
		{
			ResolveAnimator();
			Write(2f);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning((object)("[TacticalSprint.Fika] 写动画参数失败: " + ex.Message));
		}
	}

	private void SaveCurrent()
	{
		hasSaved = false;
		if (getFloat == null)
		{
			return;
		}
		try
		{
			if (getFloat.Invoke(animator, new object[1] { PlayerAnimator.WEAPON_SIZE_MODIFIER_PARAM_HASH }) is float num && num >= 1f)
			{
				savedSize = num;
				hasSaved = true;
			}
		}
		catch
		{
		}
	}

	private void Write(float size)
	{
		if (animator != null && !(setFloat == null))
		{
			object[] parameters = new object[2]
			{
				PlayerAnimator.WEAPON_SIZE_MODIFIER_PARAM_HASH,
				size
			};
			setFloat.Invoke(animator, parameters);
		}
	}

	private void ResolveAnimator()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		try
		{
			if (animator != null || (Object)player == (Object)null)
			{
				return;
			}
			object handsController = player.HandsController;
			if (handsController != null)
			{
				FieldInfo fieldInfo = AccessTools.Field(handsController.GetType(), "_playerAnimator");
				if (fieldInfo != null)
				{
					object value = fieldInfo.GetValue(handsController);
					if (value != null && SetAnimator(value))
					{
						return;
					}
				}
			}
			MethodInfo methodInfo = AccessTools.PropertyGetter(typeof(Player), "BodyAnimatorCommon");
			if (methodInfo != null)
			{
				object obj = methodInfo.Invoke(player, null);
				if (obj != null)
				{
					SetAnimator(obj);
				}
			}
		}
		catch
		{
		}
	}

	private bool SetAnimator(object a)
	{
		MethodInfo methodInfo = AccessTools.Method(a.GetType(), "SetFloat", new Type[2]
		{
			typeof(int),
			typeof(float)
		}, (Type[])null);
		if (methodInfo == null)
		{
			return false;
		}
		animator = a;
		setFloat = methodInfo;
		getFloat = AccessTools.Method(a.GetType(), "GetFloat", new Type[1] { typeof(int) }, (Type[])null);
		return true;
	}

	private float GetNativeSize()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if ((Object)player == (Object)null)
			{
				return 3f;
			}
			object handsController = player.HandsController;
			if (handsController == null)
			{
				return 3f;
			}
			object obj = null;
			string[] array = new string[3] { "ItemInHands", "Item", "Weapon" };
			foreach (string text in array)
			{
				PropertyInfo propertyInfo = AccessTools.Property(handsController.GetType(), text);
				if (propertyInfo != null)
				{
					obj = propertyInfo.GetValue(handsController, null);
					if (obj != null)
					{
						break;
					}
				}
			}
			Weapon val = (Weapon)((obj is Weapon) ? obj : null);
			if (val != null)
			{
				return Mathf.Max(3f, (float)((Item)val).CalculateCellSize().X);
			}
		}
		catch
		{
		}
		return 3f;
	}
}
