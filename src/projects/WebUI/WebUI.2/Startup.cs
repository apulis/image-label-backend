using ContactManager.Authorization;
using ContactManager.Data;
using Microsoft.AspNetCore.Authentication.WeChat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace ContactManager
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        #region snippet_defaultPolicy
        #region snippet
        #region snippet2
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

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
                    b.MigrationsAssembly("ContactManager")));
            // var optionsBuilderUsers = new DbContextOptionsBuilder<ApplicationDbContext>();
            // optionsBuilderUsers.UseSqlite(connection);
            // var appDatabase = new ApplicationDbContext(optionsBuilderUsers.Options);


            // appDatabase.Database.EnsureCreated();

            /*
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection")));*/
            services.AddDefaultIdentity<IdentityUser>().AddRoles<IdentityRole>()
                 .AddEntityFrameworkStores<ApplicationDbContext>();
            #endregion

            services.AddMvc(config =>
            {
                // using Microsoft.AspNetCore.Mvc.Authorization;
                // using Microsoft.AspNetCore.Authorization;
                var policy = new AuthorizationPolicyBuilder()
                                 .RequireAuthenticatedUser()
                                 .Build();
                config.Filters.Add(new AuthorizeFilter(policy));
            })                
               .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            #endregion

            var auth = services.AddAuthentication();

            auth.AddWeChat(wechatOptions =>
            {
                wechatOptions.AppId = Configuration["Authentication:WeChat:AppId"];
                wechatOptions.AppSecret = Configuration["Authentication:WeChat:AppSecret"];
            }
            );

            auth.AddMicrosoftAccount(microsoftOption =>
            {
                microsoftOption.ClientId = Configuration["Authentication:Microsoft:ClientId"];
                microsoftOption.ClientSecret = Configuration["Authentication:Microsoft:ClientSecret"];

            }
            );

            auth.AddGoogle(googleOptions => {
                googleOptions.ClientId = Configuration["Authentication:Google:ClientId"];
                googleOptions.ClientSecret = Configuration["Authentication:Google:ClientSecret"];
            });

            // Authorization handlers.
            services.AddScoped<IAuthorizationHandler,
                                  ContactIsOwnerAuthorizationHandler>();

            services.AddSingleton<IAuthorizationHandler,
                                  ContactAdministratorsAuthorizationHandler>();

            services.AddSingleton<IAuthorizationHandler,
                                  ContactManagerAuthorizationHandler>();
        }
        #endregion

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            // app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseAuthentication();

            app.UseMvc();
        }
    }
}
