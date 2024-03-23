﻿using LiveStreamingServerNet.Operator.Services;
using LiveStreamingServerNet.Operator.Services.Contracts;

namespace LiveStreamingServerNet.Operator.Installers
{
    public static class ControllerServicesInstaller
    {
        public static IServiceCollection AddControllerServices(this IServiceCollection services)
        {
            services.AddSingleton<IClusterStateRetriver, ClusterStateRetriver>()
                    .AddSingleton<IDesiredStateCalculator, DesiredStateCalculator>()
                    .AddSingleton<IDesiredStateApplier, DesiredStateApplier>()
                    .AddSingleton<IPodCleaner, PodCleaner>()
                    .AddSingleton<IClusterScaler, ClusterScaler>()
                    .AddTransient<ITargetReplicasStabilizer, TargetReplicasStabilizer>();

            return services;
        }
    }
}