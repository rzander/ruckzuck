using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RZ.Server
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IConfiguration configuration)
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
        public IHostingEnvironment Env { get; }
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

            services.AddSignalR();

            services.AddDistributedMemoryCache();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            if (Env.IsDevelopment())
            {
                services.AddMvc(opts =>
                {
                    opts.Filters.Add(new AllowAnonymousFilter());
                });
            }
            else
            {
                //services.AddMvc();
                services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime applicationLifetime)
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
            app.UseAuthentication();
            app.UseCookiePolicy();

            app.UseSignalR(routes =>
            {
                routes.MapHub<Default>("/msg");
            });



            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private void OnShutdown()
        {
        }

        private void OnStartup()
        {
            Console.WriteLine("loading RZ.Software-Providers:");
            Plugins.loadPlugins(Path.Combine(Env.WebRootPath, "plugins"));

            Console.Write("loading SW-Catalog...");
            Base.GetCatalog("", true);
            Console.WriteLine(" done.");
        }
    }
}
