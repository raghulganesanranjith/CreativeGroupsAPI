using CreativeGroupsAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CreativeGroupsAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Set PostgreSQL DateTime handling globally
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);
           
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    // Configure JSON serialization for DateTime
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                });
            
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Add CORS
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // Add DbContext with PostgreSQL
            builder.Services.AddDbContext<PayrollDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();
            
            app.UseHttpsRedirection();

            // Use CORS before authorization
            app.UseCors();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}

