using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Stl.DependencyInjection;
using Stl.Fusion.Authentication;
using Stl.Generators;

namespace Stl.Fusion.Server.Authentication
{
    public class AuthContextMiddleware : IMiddleware
    {
        public class Options : IOptions
        {
            public Generator<string> IdGenerator { get; set; } = RandomStringGenerator.Default;
            public CookieBuilder Cookie { get; set; } = new CookieBuilder() {
                Name = "AuthContext",
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expiration = TimeSpan.FromDays(28),
            };
        }

        protected AuthContextProvider AuthContextProvider { get; }
        protected Generator<string> IdGenerator { get; }
        protected CookieBuilder Cookie { get; }

        public AuthContextMiddleware(AuthContextProvider authContextProvider) : this(null, authContextProvider) { }
        public AuthContextMiddleware(Options? options, AuthContextProvider authContextProvider)
        {
            options ??= new Options();
            IdGenerator = options.IdGenerator;
            Cookie = options.Cookie;
            AuthContextProvider = authContextProvider;
        }

        public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
        {
            var cookies = httpContext.Request.Cookies;
            var cookieName = Cookie.Name;
            var hasCookie = cookies.TryGetValue(cookieName, out var contextId);
            if (!hasCookie || string.IsNullOrEmpty(contextId)) {
                contextId = IdGenerator.Next();
                var responseCookies = httpContext.Response.Cookies;
                if (hasCookie)
                    responseCookies.Delete(cookieName);
                responseCookies.Append(cookieName, contextId, Cookie.Build(httpContext));
            }
            var authContext = new AuthContext(contextId);
            AuthContextProvider.SetContext(authContext);
            using (authContext.Activate())
                await next(httpContext);
        }
    }
}
