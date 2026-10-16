using System.Reflection;
using System.Threading.Tasks;
using FikaServer.Controllers;
using FikaServer.Models.Fika;
using FikaServer.Models.Fika.Routes.Update;
using FikaServer.Services;
using Microsoft.Extensions.DependencyInjection;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;

namespace NekoParty.Server.GroupEX.Patches;

public class CrossRaidSetStatusPatch : AbstractPatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(UpdateController).GetMethod("HandleSetStatus");
	}

	[PatchPostfix]
	public static void Postfix(Task __result, FikaUpdateSetStatusRequestData request)
	{
		HandlePostfixAsync(__result, request);
	}

	private static async Task HandlePostfixAsync(Task resultTask, FikaUpdateSetStatusRequestData request)
	{
		await resultTask;
		NekoCrossRaidService service = ServiceLocator.ServiceProvider.GetService<NekoCrossRaidService>();
		MatchService service2 = ServiceLocator.ServiceProvider.GetService<MatchService>();
		if (service != null && service2 != null && !service.IsShadowMatch(new MongoId(request.ServerId)))
		{
			FikaMatch match = service2.GetMatch((MongoId?)new MongoId(request.ServerId));
			if (match != (FikaMatch)null)
			{
				service.TryAnnounceHostMatch(new MongoId(request.ServerId), match);
			}
		}
	}

	public CrossRaidSetStatusPatch()
		: base((string)null)
	{
	}
}
