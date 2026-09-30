using Fluxy.API.Configuration;
using Fluxy.API.Contracts;
using Fluxy.Application;
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

            // The compose .env file (next to compose.yaml) holds the real infrastructure secrets and is
            // gitignored, so it is read from disk here instead of being duplicated in appsettings.json.
            // It also provides ConnectionStrings:Postgres and ConnectionStrings:Redis, composed from its
            // POSTGRES_* and REDIS_* secrets.
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
                // Checked while the host starts rather than on the first request, so a limit of
                // zero is a startup error naming the setting rather than an API that refuses
                // every registration and never says why.
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
                // variables - but AddDataAccess throws a descriptive error at the first real use.
                app.Logger.LogWarning(
                    "No .env file was found above the content root, so the compose secrets were not " +
                    "loaded. Connection strings have to come from user-secrets or environment variables.");
            }

            if (!CorsExtensions.HasAllowedOrigins(builder.Configuration))
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
                    "is bypassed entirely and every registration is accepted on its word. Set the key " +
                    "before exposing this installation.");
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
            app.UseHttpsRedirection();

            // Routing comes first so that CORS and the authorization middleware can see which
            // endpoint is about to handle the request.
            app.UseRouting();
            app.UseCors(CorsExtensions.PolicyName);

            //app.UseAuthentication();
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