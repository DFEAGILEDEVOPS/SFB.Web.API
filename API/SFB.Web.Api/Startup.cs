using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SFB.Web.ApplicationCore.DataAccess;
using SFB.Web.ApplicationCore.Services.DataAccess;
using SFB.Web.Infrastructure.Helpers;
using SFB.Web.Infrastructure.Logging;
using SFB.Web.Infrastructure.Repositories;
using System;

namespace SFB.Web.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            string endPoint = Configuration.GetValue<string>("Secrets:endpoint");
            string authKey = Configuration.GetValue<string>("Secrets:authkey");
            string databaseId = Configuration.GetValue<string>("Secrets:database");
            string emCollectionId = Configuration.GetValue<string>("Secrets:emCollection");
            string enableAiTelemetry = Configuration.GetValue<string>("ApplicationInsights:enabled");

            var cosmosClient = new CosmosClientBuilder(endPoint, authKey)
                                .WithConnectionModeDirect()
                                .Build();

            var dataCollectionManager = new DataCollectionManager(cosmosClient, databaseId, new NetCoreCachedActiveCollectionsService());

            services.AddSingleton<ILogManager>(container => new NetCoreLogManager(container.GetRequiredService<IHttpContextAccessor>(), enableAiTelemetry));
            services.AddSingleton<IEfficiencyMetricDataService, EfficiencyMetricDataService>();
            services.AddSingleton<ISelfAssesmentDashboardDataService, SelfAssesmentDashboardDataService>();
            services.AddSingleton<IContextDataService, ContextDataService>();
            services.AddSingleton<IFinancialDataService, FinancialDataService>();
            services.AddSingleton<IFinancialDataRepository>(container => new CosmosDbFinancialDataRepository(dataCollectionManager, cosmosClient, databaseId, container.GetRequiredService<ILogManager>()));
            services.AddSingleton<IEdubaseRepository>(container => new CosmosDbEdubaseRepository(dataCollectionManager, cosmosClient, databaseId, container.GetRequiredService<ILogManager>()));
            services.AddSingleton<IEfficiencyMetricRepository>(container => new CosmosDBEfficiencyMetricRepository(cosmosClient, databaseId, emCollectionId, container.GetRequiredService<ILogManager>()));
            services.AddSingleton<ISelfAssesmentDashboardRepository>(container => new CosmosDBSelfAssesmentDashboardRepository(cosmosClient, databaseId, container.GetRequiredService<ILogManager>()));
            services.AddSingleton<IDataCollectionManager>(dataCollectionManager);

            services.AddApplicationInsightsTelemetry();
            
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder =>
                    {
                        builder.WithOrigins("http://localhost:4200", "http://localhost:4201");                       
                    });
            });

            services.AddControllers();

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            //services.AddAntiforgery(o => o.SuppressXFrameOptionsHeader = true);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.Use(async (context, next) =>
            {
                context.Response.Headers.Add("X-Xss-Protection", "1; mode=block");
                context.Response.Headers.Add("x-frame-options", "SAMEORIGIN");
                context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Remove("X-Powered-By");
                context.Response.Headers.Remove("Server");
                await next();
            });

            app.UseRouting();

            app.UseCors();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
