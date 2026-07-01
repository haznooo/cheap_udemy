
using Api;
using Api.Authorization;
using Business.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
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

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Friendly ProblemDetails body on rejection; deliberately doesn't expose the exact limits.
                options.OnRejected = async (context, cancellationToken) =>
                {
                    await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Too many requests. Please try again later."
                    }, options: null, contentType: "application/problem+json", cancellationToken);
                };

                // Global limiter: applies to every request (20/min per IP).
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ip,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                });
            });

            builder.Services.AddControllers();

            // RFC 7807 ProblemDetails as the single error contract: powers the parameterless
            // UseExceptionHandler/UseStatusCodePages below so even middleware-generated errors
            // (401/403 from JWT auth, unhandled 500s) carry the same JSON body the controllers emit.
            builder.Services.AddProblemDetails();

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
                // Authenticated-by-default: every endpoint requires a logged-in user unless it
                // explicitly opts out with [AllowAnonymous]. This prevents a forgotten [Authorize]
                // from silently exposing a whole controller (the root cause of the Enrollment/
                // Lessons/Media holes).
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

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

            // Unhandled exceptions → 500 ProblemDetails (no stack trace leaked);
            // empty-body 4xx/5xx (e.g. 401/403 from the JWT middleware) → ProblemDetails.
            app.UseExceptionHandler();
            app.UseStatusCodePages();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                // in the future Only trust the request if it comes from a specific proxy IP (e.g., your Nginx server)
            
            });

            if (app.Environment.IsDevelopment())
            {
           
                app.MapOpenApi().AllowAnonymous();
                app.MapScalarApiReference(options =>
                {
                    options.WithTitle("My API Documentation").WithTheme(ScalarTheme.Mars);
                    options.AddPreferredSecuritySchemes(JwtBearerDefaults.AuthenticationScheme);
                }).AllowAnonymous();

            }

            app.UseHttpsRedirection();
            app.UseCors("CheapUdemyApiCorsPolicy");

            app.UseRateLimiter();      // before auth: block abusive requests early

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
