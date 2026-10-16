using System.Reflection;
using System.Threading.Tasks;
using FikaServer.Models.Fika;
using FikaServer.Models.Fika.Routes.Raid.Create;
using FikaServer.Services;
using Microsoft.Extensions.DependencyInjection;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;

namespace NekoParty.Server.GroupEX.Patches;

public class CrossRaidCreatePatch : AbstractPatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(MatchService).GetMethod("CreateMatch");
	}

	[PatchPostfix]
	public static void Postfix(Task<bool> __result, FikaRaidCreateRequestData data, MongoId sessionId)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		HandlePostfixAsync(__result, data, sessionId);
	}

	private static async Task HandlePostfixAsync(Task<bool> resultTask, FikaRaidCreateRequestData data, MongoId sessionId)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (!(await resultTask))
		{
			return;
		}
		NekoCrossRaidService service = ServiceLocator.ServiceProvider.GetService<NekoCrossRaidService>();
		MatchService service2 = ServiceLocator.ServiceProvider.GetService<MatchService>();
		if (service != null && service2 != null)
		{
			FikaMatch match = service2.GetMatch((MongoId?)data.ServerId);
			if (match != (FikaMatch)null)
			{
				service.TryAnnounceHostMatch(sessionId, match);
				service.TryUploadLocationAsync(sessionId, match.LocationData);
			}
		}
	}

	public CrossRaidCreatePatch()
		: base((string)null)
	{
	}
}
