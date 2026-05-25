using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MSPAutomationControlPlane.Queues;
using MSPAutomationControlPlane.Repositories;
using MSPAutomationControlPlane.Security;
using MSPAutomationControlPlane.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(worker =>
    {
        worker.UseMiddleware<EntraAuthorizationMiddleware>();
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton(ControlPlaneAuthOptions.FromEnvironment());
        services.AddSingleton<EntraTokenValidator>();
        services.AddSingleton(ModuleRegistryOptions.FromEnvironment());
        services.AddSingleton<ModuleManifestValidator>();

        var repositoryProvider = Environment.GetEnvironmentVariable("ControlPlane__RepositoryProvider");
        if (string.Equals(repositoryProvider, "TableStorage", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(TableStorageOptions.FromEnvironment());
            services.AddSingleton<IModuleRepository, TableStorageModuleRepository>();
            services.AddSingleton<IJobRepository, TableStorageJobRepository>();
            services.AddSingleton<IClientConnectionRepository, TableStorageClientConnectionRepository>();
            services.AddSingleton<INotificationSubscriptionRepository, TableStorageNotificationSubscriptionRepository>();
            services.AddSingleton<IAuditEventRepository, TableStorageAuditEventRepository>();
        }
        else
        {
            services.AddSingleton<IModuleRepository, InMemoryModuleRepository>();
            services.AddSingleton<IJobRepository, InMemoryJobRepository>();
            services.AddSingleton<IClientConnectionRepository, InMemoryClientConnectionRepository>();
            services.AddSingleton<INotificationSubscriptionRepository, InMemoryNotificationSubscriptionRepository>();
            services.AddSingleton<IAuditEventRepository, InMemoryAuditEventRepository>();
        }

        services.AddSingleton<IOperatorContext, StubOperatorContext>();

        var queueProvider = Environment.GetEnvironmentVariable("ControlPlane__QueueProvider");
        if (string.Equals(queueProvider, "ServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(ServiceBusQueueOptions.FromEnvironment());
            services.AddSingleton<IJobQueue, ServiceBusJobQueue>();
        }
        else
        {
            services.AddSingleton<IJobQueue, InMemoryJobQueue>();
        }

        services.AddSingleton<AuditService>();
        services.AddSingleton<ClientConnectionService>();
        services.AddSingleton<ModuleRegistryService>();
        services.AddSingleton<NotificationSubscriptionService>();
        services.AddSingleton<JobService>();
        services.AddSingleton<JobDispatcher>();
        services.AddSingleton<LocalJobDispatcher>();
    })
    .Build();

host.Run();
