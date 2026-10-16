using System.Reflection;
using FikaServer.Services;
using Microsoft.Extensions.DependencyInjection;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;

namespace NekoParty.Server.GroupEX.Patches;

public class CrossRaidAddPlayerPatch : AbstractPatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(MatchService).GetMethod("AddPlayerToMatch");
	}

	[PatchPrefix]
	public static void Prefix(MongoId matchId, MongoId playerId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		NekoCrossRaidService service = ServiceLocator.ServiceProvider.GetService<NekoCrossRaidService>();
		if (service != null && service.IsShadowMatch(matchId))
		{
			service.CleanStaleShadowPlayer(matchId, playerId);
		}
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
			service.TryForwardShadowJoin(matchId, playerId, isSpectator: false);
		}
	}

	public CrossRaidAddPlayerPatch()
		: base((string)null)
	{
	}
}
