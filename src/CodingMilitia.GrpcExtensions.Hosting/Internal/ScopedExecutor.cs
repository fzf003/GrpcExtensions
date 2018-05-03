using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CodingMilitia.GrpcExtensions.Hosting.Internal
{
    internal class ScopedExecutor : IScopedExecutor
    {
        private readonly IServiceProvider _appServices;

        public ScopedExecutor(IServiceProvider appServices)
        {
            _appServices = appServices ?? throw new ArgumentNullException(paramName: nameof(appServices));
        }

        public void Execute<TService>(Action<TService> handler)
        {
            using (var scope = _appServices.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<TService>();
                handler(service);
            }
        }

        public TResult Execute<TService, TResult>(Func<TService, TResult> handler)
        {
            using (var scope = _appServices.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<TService>();
                return handler(service);
            }
        }

        public async Task ExecuteAsync<TService>(Func<TService, Task> handler)
        {
            using (var scope = _appServices.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<TService>();
                await handler(service).ConfigureAwait(false);
            }
        }

        public async Task<TResult> ExecuteAsync<TService, TResult>(Func<TService, Task<TResult>> handler)
        {
            using (var scope = _appServices.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<TService>();
                return await handler(service).ConfigureAwait(false);
            }
        }
    }
}