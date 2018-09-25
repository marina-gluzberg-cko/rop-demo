using System;
using System.Net.Http;
using RichardSzalay.MockHttp;

namespace Rop.Demo.Helpers
{
    public class HttpClientBuilder
    {
        private MockHttpMessageHandler _mockedHandler;
        
        public HttpClientBuilder()
        {
            _mockedHandler = new MockHttpMessageHandler();
        }

        public HttpClientBuilder ConfigureHandler(Action<MockHttpMessageHandler> configure)
        {
            configure(_mockedHandler);
            return this;
        }

        public HttpClient Build()
        {
            return new HttpClient(_mockedHandler);
        }
    }
}