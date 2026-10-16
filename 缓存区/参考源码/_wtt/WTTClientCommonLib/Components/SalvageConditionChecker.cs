using System;
using EFT.Quests;

namespace WTTClientCommonLib.Components;

public class SalvageConditionChecker : ConditionProgressChecker
{
	private bool _done;

	private readonly double _target;

	public SalvageConditionChecker(ConditionSalvage condition)
		: base((Condition)(object)condition)
	{
		_target = ((Condition)condition).value;
		((ConditionProgressChecker)this).SetCurrentValueGetter((Func<ConditionProgressChecker, double>)delegate(ConditionProgressChecker cpc)
		{
			SalvageConditionChecker salvageConditionChecker = (SalvageConditionChecker)(object)cpc;
			return (!salvageConditionChecker._done) ? 0.0 : salvageConditionChecker._target;
		});
	}

	public override bool Test(object testValue)
	{
		bool num = ((ConditionProgressChecker)this).Test(testValue);
		if (num && !_done)
		{
			_done = true;
		}
		return num;
	}
}
