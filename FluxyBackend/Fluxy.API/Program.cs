using Fluxy.API.Configuration;
using Fluxy.API.Contracts;
using Fluxy.Application;
using Fluxy.Application.Services.Authentication;
using Fluxy.Application.Services.Security;
using Fluxy.DataAccess;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
            builder.Services.AddApplicationServices(builder.Configuration);
            builder.Services.AddFluxyAntiforgery();
            builder.Services.AddFrontendCors(builder.Configuration);

            // The JWT scheme and the three role policies. Read before the host is built, so a
            // missing or too-short signing key aborts here rather than at the first request that
            // carries a token - see AddFluxyAuthentication for what it validates.
            builder.Services.AddFluxyAuthentication(builder.Configuration);

            builder.Services.AddOptions<RateLimitOptions>()
                .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
                .Validate(
                    options => options.RegisterLimit > 0 && options.RegisterWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive RegisterLimit and a " +
                    "RegisterWindow longer than zero.")
                .Validate(
                    options => options.ConfirmLimit > 0 && options.ConfirmWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive ConfirmLimit and a " +
                    "ConfirmWindow longer than zero.")
                .Validate(
                    options => options.LoginLimit > 0 && options.LoginWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive LoginLimit and a " +
                    "LoginWindow longer than zero.")
                .Validate(
                    options => options.RefreshLimit > 0 && options.RefreshWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive RefreshLimit and a " +
                    "RefreshWindow longer than zero.")
                .Validate(
                    options => options.PreferenceLimit > 0 && options.PreferenceWindow > TimeSpan.Zero,
                    $"'{RateLimitOptions.SectionName}' needs a positive PreferenceLimit and a " +
                    "PreferenceWindow longer than zero.")
                // Checked while the host starts rather than on the first request, so a limit of
                // zero is a startup error naming the setting rather than an API that refuses
                // every registration and never says why.
                .ValidateOnStart();

            builder.Services.AddOptions<AuthenticationOptions>()
                .Bind(builder.Configuration.GetSection(AuthenticationOptions.SectionName))
                .Validate(
                    options => options.AccessLifetime > TimeSpan.Zero,
                    $"'{AuthenticationOptions.SectionName}' needs an AccessLifetime longer than " +
                    "zero, otherwise no request could ever be authorized.")
                .Validate(
                    options => options.RefreshLifetime >= options.AccessLifetime,
                    $"'{AuthenticationOptions.SectionName}' needs a RefreshLifetime at least as " +
                    "long as the AccessLifetime, otherwise a session would expire while its " +
                    "access token was still valid and no token could ever be exchanged.")
                // Deliberately not validated here. The signing key is checked once, eagerly, by
                // AddFluxyAuthentication, because it has to be turned into bytes before the
                // scheme can exist at all - it cannot be a lazy validation on an options object
                // nothing resolves until the first authorized request.
                .ValidateOnStart();

            builder.Services.AddControllers();

            // There is no AddApiBehaviorOptions in this framework version; the options object is
            // configured through the ordinary options system, which MVC picks up.
            builder.Services.Configure<ApiBehaviorOptions>(ConfigureApiBehavior);

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

        /// <summary>
        /// Replaces the shape a rejected body is answered with.
        /// </summary>
        /// <remarks>
        /// The default is a <c>ValidationProblemDetails</c>, which carries the offending fields but
        /// no <c>message</c>. The frontend reads <c>data.message</c> and nothing else, so the
        /// default answer reaches it as an empty string and the visitor sees "something went
        /// wrong" instead of what was wrong with the form. This produces the same body as every
        /// other failure of this API, with the fields added on top.
        /// </remarks>
        private static void ConfigureApiBehavior(ApiBehaviorOptions options)
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                // Grouped rather than turned into a dictionary directly, because a value that
                // failed both while being read and while being validated lands under two keys
                // that normalize to the same name - and a duplicate key would throw out of a
                // factory that is supposed to be describing a problem, not causing one.
                var errors = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .GroupBy(entry => FieldErrorKeys.FromModelStateKey(entry.Key))
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .SelectMany(entry => entry.Value!.Errors)
                            .Select(Describe)
                            .ToArray());

                return new BadRequestObjectResult(new MessageResponse
                {
                    Code = "validation_failed",
                    Message = "Some of the values you entered are not valid.",
                    Errors = errors.Count == 0 ? null : errors
                });
            };
        }

        /// <summary>
        /// Text for one rejected value. A binding failure without a message of its own - a value
        /// that could not be read as the type it was declared as - gets a generic one rather than
        /// an empty string, so no entry in the map is ever blank.
        /// </summary>
        private static string Describe(ModelError error)
            => string.IsNullOrWhiteSpace(error.ErrorMessage)
                ? "The value is not valid."
                : error.ErrorMessage;
    }
}