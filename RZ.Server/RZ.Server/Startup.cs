using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            try
            {
                Controllers.RZController.sbconnection = Configuration["sbConnection"];
                Controllers.RZv1Controller.sbconnection = Configuration["sbConnection"];
            }
            catch { }

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
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddControllersWithViews();
            services.AddApplicationInsightsTelemetry();
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
            Console.WriteLine("loading RZ.Software-Providers:");
            Plugins.loadPlugins(Path.Combine(Path.Combine(Env.ContentRootPath, "wwwroot"), "plugins"));

            Console.Write("loading SW-Catalog...");
            Base.GetCatalog("", true);
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
