using System;
using EFT.Quests;

namespace WTTClientCommonLib.Components;

public class LeaveItemAtLocationChecker : ConditionProgressChecker
{
	private bool _done;

	private readonly double _target;

	public LeaveItemAtLocationChecker(ConditionLeaveItemAtLocation condition)
		: base((Condition)(object)condition)
	{
		_target = ((Condition)condition).value;
		((ConditionProgressChecker)this).SetCurrentValueGetter((Func<ConditionProgressChecker, double>)delegate(ConditionProgressChecker cpc)
		{
			LeaveItemAtLocationChecker leaveItemAtLocationChecker = (LeaveItemAtLocationChecker)(object)cpc;
			return (!leaveItemAtLocationChecker._done) ? 0.0 : leaveItemAtLocationChecker._target;
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
