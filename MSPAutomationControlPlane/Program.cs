using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MSPAutomationControlPlane.Repositories;
using MSPAutomationControlPlane.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddSingleton<IModuleRepository, InMemoryModuleRepository>();
        services.AddSingleton<IJobRepository, InMemoryJobRepository>();
        services.AddSingleton<IClientConnectionRepository, InMemoryClientConnectionRepository>();
        services.AddSingleton<IOperatorContext, StubOperatorContext>();
        services.AddSingleton<ClientConnectionService>();
        services.AddSingleton<ModuleRegistryService>();
        services.AddSingleton<JobService>();
    })
    .Build();

host.Run();
