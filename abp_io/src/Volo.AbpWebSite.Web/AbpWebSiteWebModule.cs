﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Geetest.Core;
using Geetest.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Modularity;
using Volo.Abp.AspNetCore.Mvc.UI;
using Volo.Abp.AspNetCore.Mvc.UI.Bootstrap;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.Autofac;
using Volo.Abp.Configuration;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.Web;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;
using Volo.Abp.UI;
using Volo.Abp.UI.Navigation;
using Volo.Abp.VirtualFileSystem;
using Volo.AbpWebSite.Bundling;
using Volo.AbpWebSite.EntityFrameworkCore;
using Volo.Blogging;
using Volo.Docs;

namespace Volo.AbpWebSite
{
    [DependsOn(
        typeof(AbpWebSiteApplicationModule),
        typeof(AbpWebSiteEntityFrameworkCoreModule),
        typeof(AbpAutofacModule),
        typeof(AbpAspNetCoreMvcUiThemeSharedModule),
        typeof(DocsApplicationModule),
        typeof(DocsWebModule),
        typeof(AbpAccountWebModule),
        typeof(AbpIdentityApplicationModule),
        typeof(AbpIdentityWebModule),
        typeof(BloggingApplicationModule),
        typeof(BloggingWebModule)
        )]
    public class AbpWebSiteWebModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.PreConfigure<AbpAspNetCoreConfigurationOptions>(options =>
            {
                options.UserSecretsAssembly = typeof(AbpWebSiteWebModule).Assembly;
            });
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();
            var configuration = context.Services.GetConfiguration();

            ConfigureDatabaseServices(context.Services, configuration);
            ConfigureVirtualFileSystem(context.Services, hostingEnvironment);
            ConfigureBundles(context.Services);
            ConfigureTheme(context.Services);

            context.Services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            context.Services.AddTransient<IGeetestManager, GeetestManager>();
            context.Services.AddTransient<IClientInfoProvider, ClientInfoProvider>();
            context.Services.AddSingleton<IGeetestConfiguration, GeetestConfiguration>();

            context.Services.AddSingleton<IGeetestConfiguration>(provider =>
                new GeetestConfiguration(provider.GetRequiredService<IClientInfoProvider>())
                {
                    Id = configuration["Captcha:Geetest:Id"],
                    Key = configuration["Captcha:Geetest:Key"]
                });
        }

        private static void ConfigureBundles(IServiceCollection services)
        {
            services.Configure<BundlingOptions>(options =>
            {
                options
                    .StyleBundles
                    .Add(AbpIoBundles.Styles.Global, bundle =>
                    {
                        bundle.
                            AddBaseBundles(StandardBundles.Styles.Global)
                            .AddFiles(
                                "/scss/vs.css",
                                "/js/prism/prism.css"
                                );
                    });

                options
                    .ScriptBundles
                    .Add(AbpIoBundles.Scripts.Global, bundle =>
                    {
                        bundle.AddBaseBundles(StandardBundles.Scripts.Global);
                    });
            });
        }

        private static void ConfigureDatabaseServices(IServiceCollection services, IConfigurationRoot configuration)
        {
            services.Configure<DbConnectionOptions>(options =>
            {
                options.ConnectionStrings.Default = configuration.GetConnectionString("Default");
            });

            services.Configure<AbpDbContextOptions>(options =>
            {
                options.Configure(context =>
                {
                    if (context.ExistingConnection != null)
                    {
                        context.DbContextOptions.UseMySql(context.ExistingConnection,
                            mysqlOptions => { mysqlOptions.ServerVersion(new Version(8, 0, 12), ServerType.MySql); });
                    }
                    else
                    {
                        context.DbContextOptions.UseMySql(context.ConnectionString,
                            mysqlOptions => { mysqlOptions.ServerVersion(new Version(8, 0, 12), ServerType.MySql); });
                    }
                });
            });
        }

        private static void ConfigureVirtualFileSystem(IServiceCollection services, IHostingEnvironment hostingEnvironment)
        {
            if (hostingEnvironment.IsDevelopment())
            {
                services.Configure<VirtualFileSystemOptions>(options =>
                {
                    options.FileSets.ReplaceEmbeddedByPyhsical<AbpUiModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}framework{0}src{0}Volo.Abp.UI", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPyhsical<AbpAspNetCoreMvcUiModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}framework{0}src{0}Volo.Abp.AspNetCore.Mvc.UI", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPyhsical<AbpAspNetCoreMvcUiBootstrapModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}framework{0}src{0}Volo.Abp.AspNetCore.Mvc.UI.Bootstrap", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPyhsical<AbpAspNetCoreMvcUiThemeSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}framework{0}src{0}Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPyhsical<DocsDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}modules{0}docs{0}src{0}Volo.Docs.Domain", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPyhsical<DocsWebModule>(Path.Combine(hostingEnvironment.ContentRootPath,    string.Format("..{0}..{0}..{0}modules{0}docs{0}src{0}Volo.Docs.Web", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPyhsical<BloggingWebModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}modules{0}blogging{0}src{0}Volo.Blogging.Web", Path.DirectorySeparatorChar)));
                    options.FileSets.ReplaceEmbeddedByPyhsical<AbpAccountWebModule>(Path.Combine(hostingEnvironment.ContentRootPath, string.Format("..{0}..{0}..{0}modules{0}account{0}src{0}Volo.Abp.Account.Web", Path.DirectorySeparatorChar)));
                });
            }
        }

        private void ConfigureTheme(IServiceCollection services)
        {
            services.Configure<ThemingOptions>(options =>
            {
                options.Themes.Add<AbpIoTheme>();
                options.DefaultThemeName = AbpIoTheme.Name;
            });
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            app.ApplicationServices.GetService<AbpWebSiteDbContext>().Database.Migrate();

            app.UseRequestLocalization(options =>
            {
                options.DefaultRequestCulture = new RequestCulture("zh-Hans", "zh-Hans");
                options.AddSupportedCultures("zh-Hans");
                options.AddSupportedUICultures("zh-Hans");
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseErrorPage();
            }

            //Necessary for LetsEncrypt
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @".well-known")),
                RequestPath = new PathString("/.well-known"),
                ServeUnknownFileTypes = true // serve extensionless file
            });

            app.UseVirtualFiles();

            app.UseAuthentication();
            
            //TODO: Create an extension method!
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "defaultWithArea",
                    template: "{area}/{controller=Home}/{action=Index}/{id?}");

                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            AsyncHelper.RunSync(async () =>
            {
                await context.ServiceProvider
                    .GetRequiredService<IIdentityDataSeeder>()
                    .SeedAsync(
                        "1q2w3E*",
                        IdentityPermissions.GetAll()
                            .Union(BloggingPermissions.GetAll())
                    );
            });
        }
    }
}
