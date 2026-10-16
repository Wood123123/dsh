using System;
using System.Threading;
using System.Threading.Tasks;
using SPTarkov.DI.Annotations;

namespace SPTarkov.Server.Core.DI;

[Injectable(/*Could not decode attribute arguments.*/)]
public class ServiceLocator : IOnLoad
{
	public static IServiceProvider ServiceProvider { get; private set; }

	public ServiceLocator(IServiceProvider serviceProvider)
	{
		ServiceProvider = serviceProvider;
	}

	public Task OnLoadAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}
