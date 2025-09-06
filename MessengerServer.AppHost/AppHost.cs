using DotNetEnv;
using MessengerServer.AppHost;
using MessengerServer.AppHost.APIResponse;
using MessengerServer.AppHost.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;

internal class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        string postgreConnString = builder.Configuration.GetConnectionString("PostgreConnection") 
            ?? throw new InvalidOperationException("Connection string 'PostgreConnection' not found.");

        _ = builder.Services.AddDbContext<PostgresDbContext>((x) => _ = x.UseNpgsql(builder.Configuration.GetConnectionString("PostgreConnection"), y => y.EnableRetryOnFailure()), ServiceLifetime.Transient);
        _ = builder.Services.AddSingleton(x => postgreConnString);


        _ = builder.Services.AddControllers(options => _ = options.Filters.Add<GlobalActionFilter>()).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.WriteIndented = true;
        });

        _ = builder.Services.AddSingleton<AuthControllerFields>();

        WebApplication app = builder.Build();

        _ = app.Use(async (context, next) =>
        {
            await next();

            if (context.Response.StatusCode is (int)HttpStatusCode.MethodNotAllowed)
            {
                HttpStatusCode httpStatusCode = (HttpStatusCode)context.Response.StatusCode;
                Logger.LogError($"{httpStatusCode}({(int)httpStatusCode}): {context.Request.Path}", CallerInfos.Create());

                APIResponse<object> response = new()
                {
                    IsSuccess = false,
                    Data = null,
                    APIError = new()
                    {
                        StatusCode = httpStatusCode,
                        Message = "You triggert an error that was not related to the operation",
                    },
                };

                string json = JsonSerializer.Serialize(response, Server.JsonSerializerOptions);
                await context.Response.WriteAsync(json);
            }
        });

        _ = app.UseWebSockets();
        _ = app.MapControllers();

        _ = Env.Load();
        await Warmup.WarmupCryptographyAsync();
        await Warmup.WarmupDbPoolAsync(postgreConnString);

        app.Run("http://localhost:80");
    }
}