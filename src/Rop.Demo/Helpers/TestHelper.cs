using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Rop.Demo.Helpers
{
    public static class TestHelper
    {
        public static TestServer CreateInternalTestServer(Action<IServiceCollection> configureServices = null, Dictionary<string, string> config = null)
        {
            var httpClient = new HttpClientBuilder()
                    .Build();

            var builder = TestWebHostBuilder.BuildTestWebHost(httpClient);

            if (config != null)
            {
                var cb = new ConfigurationBuilder()
                    .AddInMemoryCollection(config);

                builder.UseConfiguration(cb.Build());
            }

            if (configureServices != null)
            {
                builder.ConfigureServices(configureServices);
            }

            builder.UseStartup<WebApi.Startup>();

            var testServer = new TestServer(builder);
            return testServer;
        }
    }
}