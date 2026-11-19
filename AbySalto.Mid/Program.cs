using AbySalto.Mid.Application;
using AbySalto.Mid.Infrastructure;
using AbySalto.Mid.Infrastructure.Persistence;

namespace AbySalto.Mid
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services
                .AddPresentation()
                .AddApplication(builder.Configuration)
                .AddInfrastructure(builder.Configuration);

            var app = builder.Build();

            if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
                await app.Services.InitializeDatabaseAsync();
            

            app.UseExceptionHandler();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AbySalto");
                    options.RoutePrefix = string.Empty;
                });
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            await app.RunAsync();
        }
    }
}
