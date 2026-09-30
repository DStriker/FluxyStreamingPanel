

using Fluxy.API.Configuration;
using Fluxy.Application;
using Fluxy.DataAccess;
using Microsoft.AspNetCore.HttpLogging;

namespace Fluxy.API
{

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // The compose .env file (next to compose.yaml) holds the real infrastructure secrets and is
            // gitignored, so it is read from disk here instead of being duplicated in appsettings.json.
            // It also provides ConnectionStrings:Postgres, composed from its POSTGRES_* secrets.
            // The search walks up from the content root, because the content root is the Fluxy.API
            // project folder while the file lives one directory above it. The file is added as the last
            // source, so appsettings.json, environment variables and user-secrets keep a higher priority.
            var envFilePath = builder.Configuration.AddDotEnvFile(
                contentRootPath: builder.Environment.ContentRootPath);

            // Add services to the container.

            builder.Services.AddDataAccess(builder.Configuration);
            builder.Services.AddApplicationServices();
            builder.Services.AddControllers();
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

            //app.UseAuthentication();
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
