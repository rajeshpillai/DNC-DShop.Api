﻿using System;
using System.Reflection;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using DShop.Api.ServiceForwarders;
using DShop.Common.Authentication;
using DShop.Common.Mvc;
using DShop.Common.RabbitMq;
using DShop.Common.RestEase;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestEase;

namespace DShop.Api
{
    public class Startup
    {
        private static readonly string[] Headers = new []{"x-operation", "x-resource"};
        public IConfiguration Configuration { get; }
        public IContainer Container { get; private set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().AddDefaultJsonOptions();
            services.AddJwt();
            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", cors => 
                        cors.AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials()
                            .WithExposedHeaders(Headers));
            });
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyTypes(Assembly.GetEntryAssembly())
                    .AsImplementedInterfaces();
            builder.Populate(services);
            builder.AddRabbitMq();

            builder.RegisterInstance(RestClient.For<IOperationsStorage>("http://localhost:5008"));

            builder.RegisterInstance(new RestClient("http://localhost:5008")
            {
                RequestQueryParamSerializer = new QueryParamSerializer()
            }
            .For<ICustomersStorage>());        

            builder.RegisterInstance(new RestClient("http://localhost:5008/")
            {
                RequestQueryParamSerializer = new QueryParamSerializer()
            }
            .For<IOrdersStorage>());

            Container = builder.Build();

            return new AutofacServiceProvider(Container);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
            IApplicationLifetime applicationLifetime)
        {
            if (env.IsDevelopment() || env.EnvironmentName == "local")
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseAuthentication();
            app.UseCors("CorsPolicy");
            app.UseMvc();
            app.UseRabbitMq();
            applicationLifetime.ApplicationStopped.Register(() => Container.Dispose());
        }
    }
}