using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebhookReceiver.Models;
using WebhookReceiver.Models.DTOs;
using WebhookReceiver.Repositories;
using WebhookReceiver.Services;

namespace WebhookReceiver
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(CreateMapper());
            services.AddControllers();
            services.AddTransient<IAlertRepository, AlertRepository>();
            services.AddTransient<ISubscriptionRepository, SubscriptionRepository>();
            services.AddTransient<ITwitchService, TwitchService>();
            services.AddTransient<ISubscriptionService, SubscriptionService>();
            services.AddTransient<IAlertService, AlertService>();
        }

        private static IMapper CreateMapper()
        {
            MapperConfiguration configuration = new(cfg =>
            {
                cfg.CreateMap<SubscriptionDto, Subscription>();
                cfg.CreateMap<TwitchStreamAlertDto, TwitchStreamAlert>();
            });
            return configuration.CreateMapper();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}