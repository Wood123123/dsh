using System.Reflection;
using FikaServer.Models.Fika;
using FikaServer.Services;
using Microsoft.Extensions.DependencyInjection;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;

namespace NekoParty.Server.GroupEX.Patches;

public class CrossRaidSetHostPatch : AbstractPatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(MatchService).GetMethod("SetMatchHost");
	}

	[PatchPostfix]
	public static void Postfix(MongoId matchId)
	{
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		NekoCrossRaidService service = ServiceLocator.ServiceProvider.GetService<NekoCrossRaidService>();
		MatchService service2 = ServiceLocator.ServiceProvider.GetService<MatchService>();
		if (service != null && service2 != null && !service.IsShadowMatch(matchId))
		{
			FikaMatch match = service2.GetMatch((MongoId?)matchId);
			if (match != (FikaMatch)null)
			{
				service.TryAnnounceHostMatch(matchId, match);
			}
		}
	}

	public CrossRaidSetHostPatch()
		: base((string)null)
	{
	}
}
