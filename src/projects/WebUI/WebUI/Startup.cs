using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebUI.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authentication.WeChat;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using WebUI.Models;
using WebUI.Utils;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using WebUI.Azure;

namespace WebUI
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
            services.AddSession();

            var dbFile = Configuration["Data:FileName"];
            var dbPath = Configuration["Data:Path"];
            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }
            var databaseFile = Path.Join(dbPath, dbFile);
            var connectionStringBuilder = new SqliteConnectionStringBuilder { DataSource = databaseFile };
            var connectionString = connectionStringBuilder.ToString();
            var connection = new SqliteConnection(connectionString);
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connection, b =>
                    b.MigrationsAssembly("WebUI")));
            var optionsBuilderUsers = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilderUsers.UseSqlite(connection);
            var appDatabase = new ApplicationDbContext(optionsBuilderUsers.Options);


            // appDatabase.Database.EnsureCreated();
            appDatabase.Database.Migrate();

            

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => false;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            
            /*services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.RequireHeaderSymmetry = false;
            }); */


            services.AddIdentity<IdentityUser, IdentityRole>()
                // Authorization
                // .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvc(config =>
            {
                // using Microsoft.AspNetCore.Mvc.Authorization;
                // using Microsoft.AspNetCore.Authorization;
                var policy = new AuthorizationPolicyBuilder()
                                 .RequireAuthenticatedUser()
                                 .Build();
                config.Filters.Add(new AuthorizeFilter(policy));
            })
                .AddRazorPagesOptions(options =>
                {
                    options.Conventions.AuthorizePage("/api");
                    options.Conventions.AuthorizeFolder("/api");
                    options.Conventions.AllowAnonymousToPage("/Private/PublicPage");
                    options.Conventions.AllowAnonymousToFolder("/Private/PublicPages");
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddAuthorization(options =>
            {
                options.AddPolicy("OnlyAdminAccess", policy => policy.RequireRole("Admin"));
            });

            services.ConfigureApplicationCookie(options =>
            {
                // Cookie settings
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(600);

                options.LoginPath = "/Identity/Account/Login";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                options.SlidingExpiration = true;
            });

            var auth = services.AddAuthentication();
            
            auth.AddWeChat(wechatOptions =>
                {
                    wechatOptions.AppId = Configuration["Authentication:WeChat:AppId"];
                    wechatOptions.AppSecret = Configuration["Authentication:WeChat:AppSecret"];
                }
            );

            auth.AddMicrosoftAccount( microsoftOption =>
            {   
                microsoftOption.ClientId = Configuration["Authentication:Microsoft:ClientId"];
                microsoftOption.ClientSecret = Configuration["Authentication:Microsoft:ClientSecret"];

            }
            );

            auth.AddGoogle(googleOptions => {
                googleOptions.ClientId = Configuration["Authentication:Google:ClientId"];
                googleOptions.ClientSecret = Configuration["Authentication:Google:ClientSecret"];
            });

            services.AddDistributedMemoryCache();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddFile("/var/log/webui/remotesensing-{Date}.log");
            var loggerStorage = loggerFactory.CreateLogger<CloudProvider>();
            // Setting up Cloud Configuration, all other setup should be arranged afterwards. 
            LocalSetting.Setup(loggerStorage);


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            // https://stackoverflow.com/questions/43860128/asp-net-core-google-authentication/43878365
            var forwardedHeadersOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                RequireHeaderSymmetry = false
            };
            forwardedHeadersOptions.KnownNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();

            app.UseForwardedHeaders(forwardedHeadersOptions);

            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseAuthentication();
            app.UseSession(); 
            // app.UseInMemorySession(configure: s => s.IdleTimeout = TimeSpan.FromMinutes(30));
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            CreateRoles(serviceProvider).Wait();
        }
    

        private async Task CreateRoles(IServiceProvider serviceProvider)
        {
            //initializing custom roles 
            var RoleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            
            IdentityResult roleResult;

            var configAuthorization = Config.App.GetJToken(Constants.JsontagAuthorization) as JObject;
            if (!Object.ReferenceEquals(configAuthorization, null))
            {
                foreach (var pair in configAuthorization)
                {
                    var roleName = pair.Key; 
                    var roleExist = await RoleManager.RoleExistsAsync(roleName);
                    if (!roleExist)
                    {
                        //create the roles and seed them to the database: Question 1
                        roleResult = await RoleManager.CreateAsync(new IdentityRole(roleName));
                        Console.WriteLine($"Create role \"{roleName}\"");
                    }
                }
            }
        }
    }

}
