using System.Diagnostics;
using ecommerce_api.Data;
using ecommerce_api.Logging;
using ecommerce_api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.AddProvider(new DailyFileLoggerProvider());
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureServices((context, services) =>
                {
                    services.AddControllers();
                    services.AddSwaggerGen(c =>
                    {
                        c.SwaggerDoc("v1", new OpenApiInfo { Title = "E-commerce API", Version = "v1" });
                    });
                    services.AddSingleton<CustomerService>();
                    services.AddSingleton<OrderService>();
                    services.AddSingleton<OrderDetailService>();
                    services.AddSingleton<ProductService>();
                });
                webBuilder.Configure((context, app) =>
                {
                    var env = app.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
                    if (env.IsDevelopment())
                    {
                        app.UseDeveloperExceptionPage();
                    }
                    app.Use(async (context, next) =>
                    {
                        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                        var stopwatch = Stopwatch.StartNew();
                        try
                        {
                            await next();
                            logger.LogInformation(
                                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMilliseconds} ms",
                                context.Request.Method,
                                context.Request.Path,
                                context.Response.StatusCode,
                                stopwatch.ElapsedMilliseconds);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(
                                exception,
                                "HTTP {Method} {Path} failed after {ElapsedMilliseconds} ms",
                                context.Request.Method,
                                context.Request.Path,
                                stopwatch.ElapsedMilliseconds);
                            throw;
                        }
                    });
                    app.UseRouting();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapGet("/", context =>
                        {
                            context.Response.Redirect("/swagger");
                            return Task.CompletedTask;
                        });
                    });
                    var customerService = app.ApplicationServices.GetRequiredService<CustomerService>();
                    var orderService = app.ApplicationServices.GetRequiredService<OrderService>();
                    var orderDetailService = app.ApplicationServices.GetRequiredService<OrderDetailService>();
                    var productService = app.ApplicationServices.GetRequiredService<ProductService>();
                    MockDataInitializer.Initialize(customerService, orderService, orderDetailService, productService);
                    app.UseSwagger();
                    app.UseSwaggerUI(c =>
                    {
                        c.SwaggerEndpoint("/swagger/v1/swagger.json", "E-commerce API V1");
                    });
                });
            });
}