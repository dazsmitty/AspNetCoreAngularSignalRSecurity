using ApiServer.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ApiServer.Providers;
using ApiServer.SignalRHubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Primitives;
using ApiServer.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Serilog;
using Microsoft.OpenApi.Models;
using ApiServer.Model;
using Microsoft.IdentityModel.Logging;

namespace ApiServer;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        IdentityModelEventSource.ShowPII = true;

        services.Configure<ClientAppSettings>("NEWS", Configuration.GetSection("ClientAppSettingsNewsApp"));
        services.Configure<ClientAppSettings>("DM", Configuration.GetSection("ClientAppSettingsDirectMessage"));

        services.AddTransient<DataEventRecordRepository>();
        services.AddSingleton<NewsStore>();
        services.AddSingleton<UserInfoInMemory>();

        var defaultConnection = Configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<NewsContext>(options => 
            options.UseSqlite(defaultConnection), 
            ServiceLifetime.Singleton
        );

        var sqliteConnectionString = Configuration.GetConnectionString("SqliteConnectionString");
        services.AddDbContext<DataEventRecordContext>(options =>
            options.UseSqlite(sqliteConnectionString)
        );

        services.AddCors(options =>
        {
            options.AddPolicy("AllowMyOrigins",
                builder =>
                {
                    builder
                        .AllowCredentials()
                        .AllowAnyHeader()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyMethod()
                        .WithOrigins(
                            "https://localhost:44311", 
                            "https://localhost:44390", 
                            "https://localhost:44395", 
                            "https://localhost:5001");
                });
        });

        var guestPolicy = new AuthorizationPolicyBuilder()
            .RequireClaim("scope", "dataEventRecords")
            .Build();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = "https://localhost:5001/";
                options.Audience = "dataEventRecordsApi";
                options.IncludeErrorDetails = true;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = "email",
                    RoleClaimType = "role",
                    ValidAudiences = new List<string> { "dataEventRecordsApi" },
                    ValidIssuers = new List<string> { "https://localhost:5001/" }
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if ( ( context.Request.Path.Value!.StartsWith("/signalrhome")
                            || context.Request.Path.Value!.StartsWith("/looney")
                            || context.Request.Path.Value!.StartsWith("/usersdm") 
                           )
                            && context.Request.Query.TryGetValue("token", out StringValues token)
                        )
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var te = context.Exception;
                        return Task.CompletedTask;
                    }
                };
        });

        services.AddAuthorization();
        services.AddSignalR();
        services.AddControllers();

        services.AddSwaggerGen(c =>
        {
            // add JWT Authentication
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "JWT Authentication",
                Description = "Enter JWT Bearer token **_only_**",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer", // must be lower case
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Id = JwtBearerDefaults.AuthenticationScheme,
                    Type = ReferenceType.SecurityScheme
                }
            };
            c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {securityScheme, Array.Empty<string>()}
            });

            c.SwaggerDoc("v1", new OpenApiInfo { Title = "APIs", Version = "v1" });
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseDeveloperExceptionPage();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "APIs");
            c.RoutePrefix = string.Empty;
        });

        app.UseCors("AllowMyOrigins");

        // https://nblumhardt.com/2019/10/serilog-in-aspnetcore-3/
        // https://nblumhardt.com/2019/10/serilog-mvc-logging/
        app.UseSerilogRequestLogging();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHub<UsersDmHub>("/usersdm");
            endpoints.MapHub<SignalRHomeHub>("/signalrhome");
            endpoints.MapHub<NewsHub>("/looney");

            endpoints.MapControllers();
        });
    }
}
