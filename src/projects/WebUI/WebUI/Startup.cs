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


            appDatabase.Database.EnsureCreated();
            // appDatabase.Database.Migrate();

            

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            services.AddDefaultIdentity<IdentityUser>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

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
                googleOptions.ClientSecret = Configuration["Authentication:Google:ClientId"];
            });

            services.AddDistributedMemoryCache();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
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
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
