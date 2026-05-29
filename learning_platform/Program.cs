
using Api;
using Api.Authorization;
using Business.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Scalar.AspNetCore;
using Supabase;
using System.Net;
using System.Text;
namespace CheapUdemy
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);


            builder.Services.AddControllers();

            builder.Services.AddOpenApi();


            //my DI
            builder.Services.AddApiDI(builder.Configuration);
            //adding cors policy for development
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CheapUdemyApiCorsPolicy", policy =>
                {
                    policy
                        .WithOrigins(
                            "https://localhost:7038",
                            "http://localhost:5297"
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            var SecretKey = builder.Configuration["JWT_SECRET_KEY"];

            if (string.IsNullOrWhiteSpace(SecretKey))
            {
                throw new Exception("JWT secret key is not configured in environment variables");
            }


            // JWT Authentication Configuration
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {

                    options.TokenValidationParameters = new TokenValidationParameters
                    {

                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey  = true,
                        ValidIssuer = "CheapUdemyApi",
                        ValidAudience = "CheapUdemyApiUsers",
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(SecretKey))
                    };
                });
         
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("UserOwnerOrAdmin", policy =>
                    policy.Requirements.Add(new UserOwnerOrAdminRequirement()));
            });

            builder.Services.AddSingleton<IAuthorizationHandler,UserOwnerOrAdminHandler>();

            // 1. Bind the appsettings.json section to your SupabaseSettings class
            var supabaseUrl = builder.Configuration["Supabase:Url"];
            var supabaseKey = builder.Configuration["StupidKey"];
            var options = new SupabaseOptions { AutoConnectRealtime = false };
            var supabaseClient = new Supabase.Client(supabaseUrl, supabaseKey, options);
            builder.Services.AddScoped<LessonService>();
            builder.Services.AddSingleton(supabaseClient);
            builder.Services.AddScoped<IMediaService, SupabaseMediaService>();
         
            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                // in the future Only trust the request if it comes from a specific proxy IP (e.g., your Nginx server)
            
            });

            if (app.Environment.IsDevelopment())
            {
           
                app.MapOpenApi();
                app.MapScalarApiReference(options =>
                {
                    options.WithTitle("My API Documentation") .WithTheme(ScalarTheme.Mars);
                    options.AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme);
                });

            }

            app.UseHttpsRedirection();
            app.UseCors("CheapUdemyApiCorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
