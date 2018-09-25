using System;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Rop.Demo.Helpers
{
    public static class TestWebHostBuilder
    {
        public static IWebHostBuilder BuildTestWebHost(HttpClient httpClient)
        {
            var builder = new WebHostBuilder()
                .UseEnvironment("Testing")
                .ConfigureServices(x => 
                {
                    x.AddSingleton(httpClient);
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddFilter("", LogLevel.Warning);
                    logging.AddConsole();
                });

            return builder;
        }
    }
}