using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using XM.ID.Invitations.API.Middleware;
using XM.ID.Invitations.Net;
using XM.ID.Net;

namespace Invitations
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
            // Load explicity appsettings.json to avoid issue on ubuntu servers
            var Configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", false, true)
                .Build();

            // CORS defined policy to allow all
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
            });

            services.AddControllers();

            // MongoDB Initialization
            services.AddSingleton(new ViaMongoDB(Configuration));
            services.AddSingleton(new WXMService(Configuration["WXM_BASE_URL"]));
            services.AddSingleton<ConfigService>();
            services.AddSingleton<AuthTokenValidation>();
            services.AddSingleton<PayloadValidation>();
            services.AddScoped<EventLogList>();
            services.AddMvc()
            .ConfigureApiBehaviorOptions(opt
                       =>
                   {
                       opt.InvalidModelStateResponseFactory =
                           (context => new BadRequestObjectResult("Bad Request"));
                   });

            if (Configuration.GetValue<bool>("TurnOnBGScheduler"))
            {
                // Adding cron for background tasks which calls WXM bulk token API
                services.AddCronJob<DispatchTask>(c =>
                {
                    // Allowed option for Cronjob are 1 min, 2 mins, 5 mins and 10 mins.
                    // Defaulting to 1 min
                    c.TimeZoneInfo = TimeZoneInfo.Local;
                    c.CronExpression = (Configuration["ClearQueueFrequencyInMins"]) switch
                    {
                        "1" => @"* * * * *",
                        "2" => @"*/2 * * * *",
                        "5" => @"*/5 * * * *",
                        "10" => @"*/10 * * * *",
                        _ => @"* * * * *",
                    };
                });
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Configuring for reverse proxy server to forward X-Forwarded-For and X-Forwarded-Proto headers
            // to the ASP.NET Core app.
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseCors("CorsPolicy");

            //For the wwwroot folder
            app.UseStaticFiles();

            //Not needed currently since running behind kestrel as http endpoint only
            //app.UseHttpsRedirection();
            app.UseScriptingMiddleware();
            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            SetConfiguration();
        }

        private void SetConfiguration()
        {
            SharedSettings.BASE_URL = Configuration["WXM_BASE_URL"];
            SharedSettings.AuthTokenCacheExpiryInSeconds = Configuration.GetValue<double>("AuthTokenExpiryInSeconds");
            SharedSettings.CacheExpiryInSeconds = Configuration.GetValue<double>("CacheExpiryInSeconds");

            // Custom extensible class will be added here in the global dictionary
            // SharedSettings.AvailableSamplers.Add("exampleSmample", new ExampleSamplingLogic());
            // SharedSettings.AvailableQueues.Add("exampleQueue", new ExampleQueueImplementation());
            // SharedSettings.AvailableUnsubscribeCheckers.Add("exampleUnsubscribe", new ExampleUnSubscribeCheck());
        }
    }
}
