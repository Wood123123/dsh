using System.Reflection;
using FikaServer.Services;
using Microsoft.Extensions.DependencyInjection;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;

namespace NekoParty.Server.GroupEX.Patches;

public class CrossRaidRemovePlayerPatch : AbstractPatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(MatchService).GetMethod("RemovePlayerFromMatch");
	}

	[PatchPostfix]
	public static void Postfix(MongoId matchId, MongoId playerId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		NekoCrossRaidService service = ServiceLocator.ServiceProvider.GetService<NekoCrossRaidService>();
		if (service != null && service.IsShadowMatch(matchId))
		{
			service.TryForwardShadowLeave(matchId, playerId);
		}
	}

	public CrossRaidRemovePlayerPatch()
		: base((string)null)
	{
	}
}
