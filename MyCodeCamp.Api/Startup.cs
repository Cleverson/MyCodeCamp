using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyCodeCamp.Data.Data;
using MyCodeCamp.Data.Interfaces;
using MyCodeCamp.Data.Services;
using Newtonsoft.Json;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using MyCodeCamp.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using MyCodeCamp.Data.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks;
using MyCodeCamp.Data;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace MyCodeCamp.Api
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            _env = env;
            _config = builder.Build();
        }

        private IConfigurationRoot _config;
        private readonly IHostingEnvironment _env;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_config);

            //DI - DbContext
            services.AddDbContext<CampContext>(ServiceLifetime.Scoped);
            services.AddScoped<ICampRepository, CampRepository>();
            services.AddTransient<CampDbInitializer>();
            services.AddTransient<CampIdentityInitializer>();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddAutoMapper();

            services.AddIdentity<CampUser, IdentityRole>()
                .AddEntityFrameworkStores<CampContext>();

            services.Configure<IdentityOptions>(config =>
            {
                config.Cookies.ApplicationCookie.Events =
                    new CookieAuthenticationEvents()
                    {
                        OnRedirectToLogin = (ctx) =>
                        {
                            if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Response.StatusCode == 200)
                            {
                                ctx.Response.StatusCode = 401;
                            }
                            return Task.CompletedTask;
                        },
                        OnRedirectToAccessDenied = (ctx) =>
                        {
                            if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Response.StatusCode == 200)
                            {
                                ctx.Response.StatusCode = 403;
                            }
                            return Task.CompletedTask;
                        }
                    };
            });

            services.AddCors(cfg =>
            {
                cfg.AddPolicy("Sdias", bldr =>
                {
                    bldr.AllowAnyHeader()
                        .AllowAnyMethod()
                        .WithOrigins("http://sdias.com");

                });

                cfg.AddPolicy("AnyGET", bldr =>
                {
                    bldr.AllowAnyHeader()
                        .WithMethods("GET")
                        .AllowAnyOrigin();
                });
            });

            services.AddAuthorization(cfg =>
            {
                cfg.AddPolicy("SuperUsers", p => p.RequireClaim("SuperUser", "True"));
            });

            // Add framework services.
            services.AddMvc(opt =>
            {
                if (!_env.IsProduction())
                {
                    opt.SslPort = 44300;
                }
                opt.Filters.Add(new RequireHttpsAttribute());
            })
                .AddJsonOptions(opt =>
                {
                    opt.SerializerSettings.ReferenceLoopHandling =
                    ReferenceLoopHandling.Ignore;
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            CampDbInitializer seeder,
            CampIdentityInitializer identitySeeder)
        {
            loggerFactory.AddConsole(_config.GetSection("Logging"));
            loggerFactory.AddDebug();

            //app.UseCors(cfg => 
            //{
            //    cfg.AllowAnyHeader()
            //    .AllowAnyMethod()
            //    .WithOrigins("https://marcainforamtica.com.br");
            //});

            app.UseIdentity();

            app.UseJwtBearerAuthentication(new JwtBearerOptions()
            {
                AutomaticAuthenticate = true,
                AutomaticChallenge = true,
                TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters()
                {
                    ValidIssuer = _config["Tokens:Issuer"],
                    ValidAudience = _config["Tokens:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Tokens:Key"])),
                    ValidateLifetime = true
                }
            });

            app.UseMvc(config =>
            {
                //config.MapRoute("MainAPIRoute", "api/{controller}/{action}");
            });

            seeder.Seed().Wait();
            identitySeeder.Seed().Wait();
        }
    }
}
