using System;
using System.Threading;
using System.Threading.Tasks;
using NekoNet.Server.Core;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;

namespace NekoParty.Server.GroupEX.Startup;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoRaidCrossEventPollUpdate(NekoState state, NekoCrossRaidService cross) : IOnUpdate
{
	public Task<bool> OnUpdateAsync(long secondsSinceLastRun, CancellationToken cancellationToken)
	{
		if (!state.Config.Enabled || !state.Config.EnableCrossRaid || secondsSinceLastRun < Math.Max(3, state.Config.RaidEventPollSeconds))
		{
			return Task.FromResult(result: false);
		}
		cross.PollEvents();
		return Task.FromResult(result: true);
	}
}
