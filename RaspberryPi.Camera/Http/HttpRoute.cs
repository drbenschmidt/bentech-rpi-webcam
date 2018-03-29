using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaspberryPi.Camera.Http
{
    public class HttpRoute
    {
        public string Route { get; internal set; }
        private Action<HttpContext> Handler;

        public HttpRoute(string route, Action<HttpContext> handler)
        {
            this.Handler = handler;
            this.Route = route;
        }

        public bool IsMatch(HttpRequest request)
        {
            return (request.Path == this.Route);
        }

        public async Task TryExecuteAsync(HttpContext context)
        {
            try
            {
                await Task.Factory.StartNew(() =>
                {
                    this.Handler.Invoke(context);
                }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // TODO: Add generic handlers and responses.
                // TODO: Add ability to log in HttpContext.
            }
        }
    }
}
