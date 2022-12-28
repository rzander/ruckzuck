using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace RZ.Server
{
    public class Startup
    {
        public Startup(IHostEnvironment env, IConfiguration configuration)
        {

            Configuration = configuration;
            Env = env;

            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json",
                 optional: false,
                 reloadOnChange: true)
                .AddEnvironmentVariables();

            if (env.IsDevelopment())
            {
                builder.AddUserSecrets<Startup>();
            }

            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; }
        public IHostEnvironment Env { get; }
        public IDistributedCache Cache { get; }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //try
            //{
            //    Controllers.RZController.sbconnection = Configuration["sbConnection"];
            //}
            //catch { }

            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddAzureAd(options => Configuration.Bind("AzureAd", options))
            .AddCookie();

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddHealthChecks();
            services.AddSignalR();
            services.AddAuthenticationCore();
            services.AddDistributedMemoryCache();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });


            if (Env.IsDevelopment())
            {
                //Disable AUthentication in Develpment mode
                services.AddSingleton<IAuthorizationHandler, AllowAnonymous>();
            }

            //services.AddMvc();
            //services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddControllersWithViews();
            services.AddApplicationInsightsTelemetry();

            string seqUri = Environment.GetEnvironmentVariable("SeqUri") ?? "";
            string seqApiKey = Environment.GetEnvironmentVariable("SeqAPI") ?? "";

            try
            {
                if (!string.IsNullOrEmpty(seqUri))
                {
                    Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Function", LogEventLevel.Warning)
            .MinimumLevel.Override("Host", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Seq(seqUri, apiKey: seqApiKey)
            .Enrich.WithProperty("host", Environment.MachineName)
            .CreateLogger();
                }
                else
                {
                    Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Function", LogEventLevel.Warning)
            .MinimumLevel.Override("Host", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .Enrich.WithProperty("host", Environment.MachineName)
            .CreateLogger();
                }
            }
            catch
            {
            }

            SecretClientOptions options = new SecretClientOptions()
            {
                Retry =
                        {
                            Delay= TimeSpan.FromSeconds(2),
                            MaxDelay = TimeSpan.FromSeconds(16),
                            MaxRetries = 5,
                            Mode = RetryMode.Exponential
                        }
            };
            string vaultBaseUrl = Environment.GetEnvironmentVariable("VaultUri");
#if !DEBUG
            var _keyVaultClient = new SecretClient(new Uri(vaultBaseUrl), new DefaultAzureCredential(), options);
#endif
#if DEBUG
            var _keyVaultClient = new SecretClient(new Uri(vaultBaseUrl), new DefaultAzureCredential(true), options);
#endif

            try
            {
                //Get Settings from KeyVault
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Cat")))
                    Environment.SetEnvironmentVariable("SAS:Cat", _keyVaultClient.GetSecret("cat").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Cont")))
                    Environment.SetEnvironmentVariable("SAS:Cont", _keyVaultClient.GetSecret("cont").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Icon")))
                    Environment.SetEnvironmentVariable("SAS:Icon", _keyVaultClient.GetSecret("icon").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Repo")))
                    Environment.SetEnvironmentVariable("SAS:Repo", _keyVaultClient.GetSecret("repo").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Wait")))
                    Environment.SetEnvironmentVariable("SAS:Wait", _keyVaultClient.GetSecret("wait").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Look")))
                    Environment.SetEnvironmentVariable("SAS:Look", _keyVaultClient.GetSecret("look").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Map")))
                    Environment.SetEnvironmentVariable("SAS:Map", _keyVaultClient.GetSecret("map").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Feedback")))
                    Environment.SetEnvironmentVariable("SAS:Feedback", _keyVaultClient.GetSecret("feedback").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Dlq")))
                    Environment.SetEnvironmentVariable("SAS:Dlq", _keyVaultClient.GetSecret("dlq").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Suq")))
                    Environment.SetEnvironmentVariable("SAS:Suq", _keyVaultClient.GetSecret("suq").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Faq")))
                    Environment.SetEnvironmentVariable("SAS:Faq", _keyVaultClient.GetSecret("faq").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Swq")))
                    Environment.SetEnvironmentVariable("SAS:Swq", _keyVaultClient.GetSecret("swq").Value.Value.ToString());
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAS:Ip")))
                    Environment.SetEnvironmentVariable("SAS:Ip", _keyVaultClient.GetSecret("ip").Value.Value.ToString());
                //if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("sbConnection")))
                //    Environment.SetEnvironmentVariable("sbConnection", _keyVaultClient.GetSecret("sbConnection").Value.Value.ToString());

                Log.Verbose("Secrets loaded without error...");
            }
            catch (Exception ex)
            {
                Log.ForContext("URL", vaultBaseUrl).Error("Error loading Secrets {ex}", ex.Message);
                ex.Message.ToString();
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostEnvironment env, IHostApplicationLifetime applicationLifetime)
        {
            applicationLifetime.ApplicationStopping.Register(OnShutdown);
            applicationLifetime.ApplicationStarted.Register(OnStartup);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();

            }

            app.UseHttpsRedirection();
            //app.UseStaticFiles();
            app.UseStaticFiles(new StaticFileOptions()
            {
                OnPrepareResponse = s =>
                {
                    if (s.Context.Request.Path.StartsWithSegments(new PathString("/plugins")) &&
                       !s.Context.User.Identity.IsAuthenticated)
                    {
                        s.Context.Response.StatusCode = 401;
                        s.Context.Response.Body = Stream.Null;
                        s.Context.Response.ContentLength = 0;
                    }

                    if (s.Context.Request.Path.StartsWithSegments(new PathString("/content")) &&
                        !s.Context.User.Identity.IsAuthenticated)
                    {
                        s.Context.Response.StatusCode = 401;
                        s.Context.Response.Body = Stream.Null;
                        s.Context.Response.ContentLength = 0;
                    }

                    if (s.Context.Request.Path.StartsWithSegments(new PathString("/repository")) &&
                        !s.Context.User.Identity.IsAuthenticated)
                    {
                        s.Context.Response.StatusCode = 401;
                        s.Context.Response.Body = Stream.Null;
                        s.Context.Response.ContentLength = 0;
                    }
                }
            });
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            
            app.UseCookiePolicy();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapHub<Default>("/msg");
            });

            //app.UseSignalR(routes =>
            //{
            //    routes.MapHub<Default>("/msg");
            //});

            //app.UseMvc(routes =>
            //{
            //    routes.MapRoute(
            //        name: "default",
            //        template: "{controller=Home}/{action=Index}/{id?}");
            //});
        }

        private void OnShutdown()
        {
        }

        private void OnStartup()
        {
            Log.Verbose("loading RZ.Software-Providers from {path}", Path.Combine(Path.Combine(Env.ContentRootPath, "wwwroot"), "plugins"));
            //Console.WriteLine("loading RZ.Software-Providers:");
            Plugins.loadPlugins(Path.Combine(Path.Combine(Env.ContentRootPath, "wwwroot"), "plugins"));

            Log.Verbose("loading SW-Catalog...");
            //Base.GetCatalog("", true);
            Console.WriteLine(" done.");
        }

        /// <summary>
        /// This authorisation handler will bypass all requirements
        /// </summary>
        public class AllowAnonymous : IAuthorizationHandler
        {
            public Task HandleAsync(AuthorizationHandlerContext context)
            {
                foreach (IAuthorizationRequirement requirement in context.PendingRequirements.ToList())
                    context.Succeed(requirement); //Simply pass all requirements

                return Task.CompletedTask;
            }
        }
    }
}
