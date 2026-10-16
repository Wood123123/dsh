using System.Reflection;
using System.Threading.Tasks;
using FikaServer.Services;
using Microsoft.Extensions.DependencyInjection;
using NekoParty.Server.GroupEX.Services;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;

namespace NekoParty.Server.GroupEX.Patches;

public class MatchDeletePatch : AbstractPatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(MatchService).GetMethod("DeleteMatch");
	}

	[PatchPostfix]
	public static void Postfix(ValueTask __result, MongoId matchId)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		HandlePostfixAsync(__result, matchId);
	}

	private unsafe static async Task HandlePostfixAsync(ValueTask resultTask, MongoId matchId)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		await resultTask;
		ServiceLocator.ServiceProvider.GetService<NekoGroupService>()?.MarkPartyOpen(((object)(*(MongoId*)(&matchId))/*cast due to constrained. prefix*/).ToString());
	}

	public MatchDeletePatch()
		: base((string)null)
	{
	}
}
