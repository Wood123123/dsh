using EFT;
using EFT.Interactive;
using UnityEngine;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Components;

public class ZoneFlareTrigger : TriggerWithId
{
	public int Experience;

	public void Awake()
	{
		((Component)this).gameObject.layer = LayerMask.NameToLayer("Triggers");
	}

	public override void TriggerEnter(Player player)
	{
		((TriggerWithId)this).TriggerEnter(player);
		LogHelper.LogDebug("WTT-ClientCommonLib: Entered Flare CustomQuestZone.");
	}
}
