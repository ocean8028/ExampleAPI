using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNet.OData.Formatter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExampleAPI
{
    public class Startup
    {
        IConfigurationSection swaggerSettings;
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            //Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = configuration;
            //this.env = env;
        }

        //public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOData();
            services.AddControllers(mvcOptions =>
            {
                mvcOptions.EnableEndpointRouting = false;
                foreach (var formatter in mvcOptions.OutputFormatters
                       .OfType<ODataOutputFormatter>()
                       .Where(it => !it.SupportedMediaTypes.Any()))
                {
                    formatter.SupportedMediaTypes.Add(
                        new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/prs.mock-odata"));
                }

                foreach (var formatter in mvcOptions.InputFormatters
                    .OfType<ODataInputFormatter>()
                    .Where(it => !it.SupportedMediaTypes.Any()))
                {
                    formatter.SupportedMediaTypes.Add(
                        new Microsoft.Net.Http.Headers.MediaTypeHeaderValue("application/prs.mock-odata"));
                }
            }).AddNewtonsoftJson();

            services.AddSwaggerGen(swagger =>
            {
                swagger.SwaggerDoc(this.GetSwaggerSetting<string>("Document:Info:Version"), new OpenApiInfo
                {
                    Title = $"{GetSwaggerSetting<string>("Document:Info:Title")}",
                    Description = GetSwaggerSetting<string>("Document:Info:Description"),
                    Version = GetSwaggerSetting<string>("Document:Info:Version")
                });

                // [SwaggerRequestExample] & [SwaggerResponseExample]
                //swagger.ExampleFilters();
                //Tell Swagger to use XML comments from different assemblies
                List<string> xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml", SearchOption.TopDirectoryOnly).ToList();
                xmlFiles.ForEach(xmlFile => swagger.IncludeXmlComments(xmlFile));

                swagger.ResolveConflictingActions(apiDescriptions =>
                {
                    return apiDescriptions.First();
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            //app.UseHttpsRedirection();

            app.UseRouting();

            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.EnableDependencyInjection();
                endpoints.Select().Filter().OrderBy().Count().MaxTop(10);
            });

            app.UseSwagger(options =>
            {
                options.PreSerializeFilters.Add((swagger, httpReq) =>
                {
                    swagger.Servers = new List<OpenApiServer> { new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" } };
                });
            });
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint(this.GetSwaggerSetting<string>("UI:EndPoint:Url"), this.GetSwaggerSetting<string>("UI:EndPoint:Name"));
                c.RoutePrefix = String.Empty;
            });

            string logFilePath = Configuration.GetValue<string>("LogFile");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Hour)
                .CreateLogger();

            Log.Information("ExampleAPI service starting...");
        }

        private T GetSwaggerSetting<T>(string key)
        {
            if (swaggerSettings == null)
                swaggerSettings = this.Configuration.GetSection("Swagger");
            return swaggerSettings.GetValue<T>(key);
        }
    }
}
