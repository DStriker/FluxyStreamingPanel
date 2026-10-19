using Fluxy.API.Controllers.Support;
using Fluxy.API.Extensions;
using Fluxy.API.Options;
using Fluxy.Application;
using Fluxy.Application.Services.Security;
using Fluxy.DataAccess;
using Microsoft.AspNetCore.HttpLogging;

namespace Fluxy.API
{

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // The .env file (next to compose.yaml) holds the real secrets and is gitignored, so it is read
            // from disk here instead of being duplicated in appsettings.json. Compose reads the same
            // file for the passwords of postgres, pgadmin and redis; this call composes
            // ConnectionStrings:Postgres and ConnectionStrings:Redis out of its POSTGRES_* and
            // REDIS_* keys, and feeds SMTP_* and RECAPTCHA_SECRET_KEY to the options they configure.
            // No credential appears in appsettings.json at all - not even as an empty placeholder,
            // because a key present there outranks this file and would silently win over it.
            // The search walks up from the content root, because the content root is the Fluxy.API
            // project folder while the file lives one directory above it. The file is inserted as the
            // FIRST source rather than appended, so appsettings.json, environment variables and
            // user-secrets keep a higher priority - in .NET configuration the last provider that
            // knows a key wins, so appending it would have let the file override all of them.
            var envFilePath = builder.Configuration.AddDotEnvFile(
                contentRootPath: builder.Environment.ContentRootPath);

            // Add services to the container.

            builder.Services.AddDataAccess(builder.Configuration);
            builder.Services.AddRedis(builder.Configuration);
            builder.Services.AddGeoIp(builder.Configuration);
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddFluxyAntiforgery();
            builder.Services.AddFrontendCors(builder.Configuration);

            // The JWT scheme and the three role policies. Read before the host is built, so a
            // missing or too-short signing key aborts here rather than at the first request that
            // carries a token - see AddFluxyAuthentication for what it validates.
            builder.Services.AddFluxyAuthentication(builder.Configuration);

            builder.Services.AddFluxyRateLimits(builder.Configuration);

            builder.Services.AddControllers();
            builder.Services.AddFluxyApiBehavior();
            builder.Services.AddFluxyExceptionHandling();

            // The two guards the guest-facing auth controllers compose. They are scoped
            // because the limiter follows the options monitor and the captcha guard is used
            // once per request; neither holds state between requests.
            builder.Services.AddScoped<AttemptLimiter>();
            builder.Services.AddScoped<CaptchaGuard>();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi(); 
            
            builder.Services.AddHttpLogging(opts =>
                opts.LoggingFields = HttpLoggingFields.RequestProperties);
            builder.Logging.AddFilter(
                "Microsoft.AspNetCore.HttpLogging", LogLevel.Information);

            // Auth

            var app = builder.Build();

            if (envFilePath is null)
            {
                // Not fatal on its own - the same values may come from user-secrets or environment
                // variables - but AddDataAccess throws a descriptive error at the first real use, and
                // without the file the mail settings have nowhere to come from at all.
                app.Logger.LogWarning(
                    "No .env file was found above the content root, so the secrets it holds were not " +
                    "loaded. Connection strings, SMTP settings and the reCAPTCHA key have to come from " +
                    "user-secrets or environment variables.");
            }

            if (!CorsExtensions.IsEnabled(builder.Configuration))
            {
                app.Logger.LogWarning(
                    $"The browser policy is switched off ('{CorsExtensions.EnabledKey}' is false). " +
                    "Browsers may reach this API from its own origin only - a page served from " +
                    "another origin will be refused, and no origin list can bring it back.");
            }
            else if (!CorsExtensions.HasAllowedOrigins(builder.Configuration))
            {
                app.Logger.LogWarning(
                    $"No origin is listed in '{CorsExtensions.OriginsSectionName}', so no browser " +
                    "may call this API. The frontend needs its origin added there to work.");
            }

            if (string.IsNullOrWhiteSpace(builder.Configuration[$"{RecaptchaOptions.SectionName}:SecretKey"]))
            {
                // Deliberately loud. An installation without the secret does not fail, it silently
                // stops checking anything, and that is exactly the kind of gap nobody notices until
                // it has been exploited.
                app.Logger.LogWarning(
                    $"'{RecaptchaOptions.SectionName}:SecretKey' is not set, so the reCAPTCHA check " +
                    "is bypassed entirely and every registration is accepted on its word. Put " +
                    "RECAPTCHA_SECRET_KEY in .env before exposing this installation.");
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi(); 
                app.UseDeveloperExceptionPage();
                app.UseHttpLogging();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/openapi/v1.json", "API v1");
                });
            }
            else
            {
                // Outside Development the developer page is replaced by the API envelope: the
                // stack trace belongs on a workstation, never in a response. The handler is
                // registered in every environment because whether it is used is the pipeline's
                // decision, not the service collection's.
                app.UseFluxyExceptionHandling();
            }

            // 404/405/415 are decided before any action runs, so they need the envelope here
            // rather than in a controller. This sits above routing so it sees the status those
            // middleware leave behind, and its filter leaves every body already written alone.
            app.UseFluxyStatusCodePages();

            // Routing comes first so that CORS and the authorization middleware can see which
            // endpoint is about to handle the request.
            app.UseRouting();

            // CORS has to answer a preflight itself. A preflight that gets redirected first is
            // answered with a 307 that carries no Access-Control-Allow-Origin, and the browser
            // reports that as `PreflightMissingAllowOriginHeader` - a CORS failure caused by
            // middleware ordering, not by the origin policy. `UseCors` therefore runs before
            // `UseHttpsRedirection`, which is the reverse of the order the templates suggest.
            app.UseFrontendCors();

            // TLS is terminated by whatever fronts this process, so the redirect is skipped for
            // requests that arrived through the Vite dev proxy. That proxy sends
            // `X-Forwarded-Proto: http`, and a proxied response must not redirect: the browser
            // would follow the Location to a different scheme, the call becomes cross-origin, and
            // the antiforgery cookies come back `secure` over a plain http connection and are
            // discarded - which reads as `csrf_invalid` with no cause in the response.
            //
            // Scoped to Development on purpose. Outside it the header is attacker-controlled, and
            // honouring it would let anyone skip TLS on this process. A production deployment
            // terminates TLS at its own reverse proxy, which is not this middleware's business.
            app.UseWhen(
                context => !app.Environment.IsDevelopment()
                    || !context.Request.Headers.ContainsKey(CorsExtensions.ProxyProtocolHeader),
                branch => branch.UseHttpsRedirection());

            // Authentication runs before authorization, and both come after the middleware that can change
            // the identity of the request. `UseAuthentication` is what turns a token into a
            // principal at all: without it the principal stays anonymous, `[Authorize]` has
            // nothing to check, and every protected endpoint answers 401 for every caller
            // including the ones holding a perfectly good token.
            //
            // Nothing about CORS or the HTTPS redirect has to come first, because neither reads
            // the principal - but the redirect does have to stay above this, since a browser that
            // follows a 307 to the other port would arrive here with the token absent.
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            // Applies the pending migrations and creates the first administrator, which prints
            // its generated password once. It runs before app.Run() so that the schema exists
            // by the time the first request arrives.
            await app.Services.SeedDatabaseAsync();

            app.Run();
        }
    }
}