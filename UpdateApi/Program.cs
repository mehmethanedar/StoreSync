using Microsoft.AspNetCore.Diagnostics;
using StoreSync.UpdateApi.Models;
using StoreSync.UpdateApi.Services;

namespace StoreSync.UpdateApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Controller ve Swagger kayıtları.
            builder.Services.AddControllers();
            builder.Services.AddSwaggerGen();

            // İki servis aynı aktarım durumunu kullansın diye birer kez oluşturulur.
            builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection("Demo"));
            builder.Services.AddHttpClient("SourceServer");
            builder.Services.AddSingleton<DemoFolderService>();
            builder.Services.AddSingleton<TransferService>();
            builder.Services.AddHostedService<TransferWorker>();

            WebApplication app = builder.Build();

            DemoFolderService folderService = app.Services.GetRequiredService<DemoFolderService>();
            folderService.Initialize();

            app.UseExceptionHandler(ConfigureErrorHandler);
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.MapControllers();
            app.Run();
        }

        private static void ConfigureErrorHandler(IApplicationBuilder errorApp)
        {
            errorApp.Run(WriteErrorAsync);
        }

        // Serviste oluşan hatayı web ekranının okuyabileceği bir yanıt haline getirir.
        private static async Task WriteErrorAsync(HttpContext context)
        {
            IExceptionHandlerFeature? errorInfo = context.Features.Get<IExceptionHandlerFeature>();
            Exception? error = null;
            if (errorInfo != null)
            {
                error = errorInfo.Error;
            }

            context.Response.StatusCode = 500;
            if (error is ArgumentException)
            {
                context.Response.StatusCode = 400;
            }
            else if (error is InvalidOperationException)
            {
                context.Response.StatusCode = 409;
            }

            string message = "İşlem tamamlanamadı.";
            if (error != null)
            {
                message = error.Message;
            }

            await context.Response.WriteAsJsonAsync(new { message = message });
        }
    }
}
