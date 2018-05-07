using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CodingMilitia.GrpcExtensions.Hosting.Internal;
using Grpc.Core;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AddGrpcServerServiceCollectionExtensions
    {
        public static IServiceCollection AddGrpcServer(this IServiceCollection serviceCollection, Server server)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            if (server == null)
            {
                throw new ArgumentNullException(nameof(server));
            }

            serviceCollection.AddSingleton(server);
            serviceCollection.AddSingleton<IHostedService, GrpcBackgroundService>();
            return serviceCollection;
        }

        public static IServiceCollection AddGrpcServer(this IServiceCollection serviceCollection, Func<IServiceProvider, Server> serverFactory)
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            if (serverFactory == null)
            {
                throw new ArgumentNullException(nameof(serverFactory));
            }

            serviceCollection.AddSingleton(serverFactory);
            serviceCollection.AddSingleton<IHostedService, GrpcBackgroundService>();
            return serviceCollection;
        }

        public static IServiceCollection AddGrpcServer<TService>(
            this IServiceCollection serviceCollection,
            IEnumerable<ServerPort> ports,
            IEnumerable<ChannelOption> channelOptions = null
        )
            where TService : class
        {
            if (serviceCollection == null)
            {
                throw new ArgumentNullException(nameof(serviceCollection));
            }

            if (ports == null)
            {
                throw new ArgumentNullException(nameof(ports));
            }

            if (serviceCollection.Any(s => s.ServiceType.Equals(typeof(TService))))
            {
                throw new InvalidOperationException($"{typeof(TService).Name} is already registered in the container.");
            }

            var serviceBinder = GetServiceBinder<TService>();

            serviceCollection.AddSingleton<TService>();
            serviceCollection.AddSingleton(appServices =>
            {
                var server = channelOptions != null ? new Server(channelOptions) : new Server();
                server.AddPorts(ports);
                server.AddServices(serviceBinder(appServices.GetRequiredService<TService>()));
                return new TypedServerContainer<TService>(server);
            });

            serviceCollection.AddSingleton<IHostedService, TypedGrpcBackgroundService<TService>>();
            return serviceCollection;
        }

        private static void AddPorts(this Server server, IEnumerable<ServerPort> ports)
        {
            foreach (var port in ports)
            {
                server.Ports.Add(port);
            }
        }

        private static void AddServices(this Server server, params ServerServiceDefinition[] services)
        {
            server.AddServices((IEnumerable<ServerServiceDefinition>)services);
        }

        private static void AddServices(this Server server, IEnumerable<ServerServiceDefinition> services)
        {
            foreach (var service in services)
            {
                server.Services.Add(service);
            }
        }

        private static Func<TService, ServerServiceDefinition> GetServiceBinder<TService>()
        {
            var serviceType = typeof(TService);
            var baseServiceType = serviceType.BaseType;
            var serviceDefinitionType = typeof(ServerServiceDefinition);

            var serviceContainerType = baseServiceType.DeclaringType;
            var methods = serviceContainerType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            var binder =
                (from m in methods
                 let parameters = m.GetParameters()
                 where m.Name.Equals("BindService")
                     && parameters.Length == 1
                     && parameters.First().ParameterType.Equals(baseServiceType)
                     && m.ReturnType.Equals(serviceDefinitionType)
                 select m)
            .SingleOrDefault();

            if (binder == null)
            {
                throw new InvalidOperationException($"Could not find service binder for provided service {serviceType.Name}");
            }

            var serviceParameter = Expression.Parameter(serviceType);
            var invocation = Expression.Call(null, binder, new[] { serviceParameter });
            var func = Expression.Lambda<Func<TService, ServerServiceDefinition>>(invocation, false, new[] { serviceParameter }).Compile();
            return func;
        }
    }
}