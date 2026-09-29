

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace Refluxy.API
{
    public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public BasicAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                Logger.LogError("Authorization header missing");
                return AuthenticateResult.Fail("Missing authorization");
            }

            var header = AuthenticationHeaderValue.Parse(Request.Headers.Authorization!);
            var credentialsRaw = Convert.FromBase64String(header.Parameter!);
            var credentials = Encoding.UTF8.GetString(credentialsRaw).Split(':', 2);
            var username = credentials[0];
            var password = credentials[1];

            if (username != "admin" || password != "admin")
            {
                Logger.LogError($"Credentials: {username}:{password}");
                return AuthenticateResult.Fail("Invalid username or password");
            }
            Logger.LogInformation($"Credentials: {username}:{password}");

            Claim[] claims = [ new Claim(ClaimTypes.NameIdentifier, "admin"), new Claim(ClaimTypes.Name, "admin") ];
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers.WWWAuthenticate = "Basic realm=\"Some realm\"";
            await base.HandleChallengeAsync(properties);
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi(); 
            
            builder.Services.AddHttpLogging(opts =>
                opts.LoggingFields = HttpLoggingFields.RequestProperties);
            builder.Logging.AddFilter(
                "Microsoft.AspNetCore.HttpLogging", LogLevel.Information);

            // Auth
            builder.Services.AddAuthentication("basic")
                .AddScheme<AuthenticationSchemeOptions, BasicAuthHandler>("basic", null);

            var app = builder.Build();

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

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
