using System;
using System.Threading;
using System.Threading.Tasks;
using NekoParty.Server.GroupEX.Patches;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;

namespace NekoParty.Server.GroupEX.Startup;

[Injectable(/*Could not decode attribute arguments.*/)]
public class NekoGroupExOnLoad(ISptLogger<NekoGroupExOnLoad> logger, NekoCrossPartyService cross, NekoCrossRaidService crossRaid) : IOnLoad
{
	public Task OnLoadAsync(CancellationToken cancellationToken)
	{
		((AbstractPatch)new RaidCreatePatch()).Enable();
		((AbstractPatch)new MatchDeletePatch()).Enable();
		((AbstractPatch)new SaveServerScavProfileFallbackPatch()).Enable();
		((AbstractPatch)new CrossRaidCreatePatch()).Enable();
		((AbstractPatch)new CrossRaidSetHostPatch()).Enable();
		((AbstractPatch)new CrossRaidSetStatusPatch()).Enable();
		((AbstractPatch)new CrossRaidEndPatch()).Enable();
		((AbstractPatch)new CrossRaidAddPlayerPatch()).Enable();
		((AbstractPatch)new CrossRaidRemovePlayerPatch()).Enable();
		cross.Start();
		crossRaid.Start();
		logger.Success("========================================", (Exception)null);
		logger.Success(" _   _      _         ____            _         ", (Exception)null);
		logger.Success("| \\ | | ___| | _____ |  _ \\ __ _ _ __| |_ _   _ ", (Exception)null);
		logger.Success("|  \\| |/ _ \\ |/ / _ \\| |_) / _` | '__| __| | | |", (Exception)null);
		logger.Success("| |\\  |  __/   < (_) |  __/ (_| | |  | |_| |_| |", (Exception)null);
		logger.Success("|_| \\_|\\___|_|\\_\\___/|_|   \\__,_|_|   \\__|\\__, |", (Exception)null);
		logger.Success("                                          |___/ ", (Exception)null);
		logger.Success("NekoParty.Server.GroupEX 组队模块加载成功", (Exception)null);
		logger.Success("========================================", (Exception)null);
		return Task.CompletedTask;
	}
}
