using ApiServer.Model;
using ApiServer.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;
using IdentityServer4.AccessTokenValidation;
using ApiServer.Policies;
using ApiServer.Providers;
using ApiServer.SignalRHubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Primitives;
using System.Threading.Tasks;
using ApiServer.Data;
using Serilog;

namespace ApiServer
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; set; }
        
        private IHostingEnvironment _env { get; set; }

        public Startup(IHostingEnvironment env)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.WithProperty("App", "ApiServer")
                .Enrich.FromLogContext()
                .WriteTo.Seq("http://localhost:5341")
                .WriteTo.RollingFile("../../Logs/ApiServer")
                .CreateLogger();

            _env = env;
            var builder = new ConfigurationBuilder()
                 .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json");
            Configuration = builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var sqliteConnectionString = Configuration.GetConnectionString("SqliteConnectionString");
            var defaultConnection = Configuration.GetConnectionString("DefaultConnection");

            var cert = new X509Certificate2(Path.Combine(_env.ContentRootPath, "damienbodserver.pfx"), "");

            services.AddDbContext<DataEventRecordContext>(options =>
                options.UseSqlite(sqliteConnectionString)
            );

            // used for the new items which belong to the signalr hub
            services.AddDbContext<NewsContext>(options =>
                options.UseSqlite(
                    defaultConnection
                ), ServiceLifetime.Singleton
            );

            //Add Cors support to the service
            services.AddCors();

            var policy = new Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicy();

            policy.Headers.Add("*");
            policy.Methods.Add("*");
            policy.Origins.Add("*");
            policy.SupportsCredentials = true;

            services.AddCors(x => x.AddPolicy("corsGlobalPolicy", policy));

            var guestPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("scope", "dataEventRecords")
                .Build();

            services.AddAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme)
              .AddIdentityServerAuthentication(options =>
              {
                  options.Authority = "https://localhost:44318/";
                  options.ApiName = "dataEventRecords";
                  options.ApiSecret = "dataEventRecordsSecret";
                  options.Events = new JwtBearerEvents
                  {
                      OnMessageReceived = context =>
                      {
                          StringValues token;
                          if (context.Request.Path.Value.StartsWith("/loo") && context.Request.Query.TryGetValue("token", out token))
                          {
                              context.Token = token;
                          }

                          return Task.CompletedTask;
                      }
                  };
              });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("dataEventRecordsAdmin", policyAdmin =>
                {
                    policyAdmin.RequireClaim("role", "dataEventRecords.admin");
                });
                options.AddPolicy("dataEventRecordsUser", policyUser =>
                {
                    policyUser.RequireClaim("role", "dataEventRecords.user");
                });
                options.AddPolicy("dataEventRecords", policyUser =>
                {
                    policyUser.RequireClaim("scope", "dataEventRecords");
                });
                options.AddPolicy("correctUser", policyCorrectUser =>
                {
                    policyCorrectUser.Requirements.Add(new CorrectUserRequirement());
                });
            });

            services.AddSingleton<IAuthorizationHandler, CorrectUserHandler>();

            services.AddSingleton<NewsStore>();
            services.AddSignalR();

            services.AddMvc(options =>
            {
               options.Filters.Add(new AuthorizeFilter(guestPolicy));
            }).AddJsonOptions(options =>
            {
                options.SerializerSettings.ContractResolver = new DefaultContractResolver();
            });

            services.AddScoped<IDataEventRecordRepository, DataEventRecordRepository>();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();
            loggerFactory.AddDebug();

            loggerFactory.AddSerilog();

            app.UseExceptionHandler("/Home/Error");
            app.UseCors("corsGlobalPolicy");
            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseSignalR(routes =>
            {
                routes.MapHub<LoopyHub>("loopy");
                routes.MapHub<NewsHub>("looney");
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
