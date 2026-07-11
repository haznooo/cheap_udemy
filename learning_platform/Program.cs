
using Api;
using Api.Authorization;
using Business.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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

                // No GlobalLimiter: a GlobalLimiter always stacks (AND's) with any endpoint-specific
                // policy, and [DisableRateLimiting] turns out to disable it too when combined with
                // [EnableRateLimiting] on the same endpoint (verified empirically) — neither gives a
                // policy that's actually independent. Instead, every endpoint gets exactly ONE policy:
                // "standard" by default (applied via a class-level [EnableRateLimiting] on
                // ApiControllerBase, inherited by every controller), overridden per-method to "auth"
                // on login/signUp/refresh. Method-level metadata wins over class-level, so these two
                // budgets never stack.
                options.AddPolicy("standard", httpContext =>
                {
                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ip,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 67,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                });

                // Dedicated auth limiter: shared 10/min-per-IP budget across login/signUp/refresh
                // (one counter for all three, not one each). Attempt-based (counts successes too),
                // not failure-only — a deliberate, simpler tradeoff for this project.
                options.AddPolicy("auth", httpContext =>
                {
                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ip,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
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

            builder.Services.AddOpenApi(openApiOptions =>
            {
                // Declare the JWT bearer scheme in the generated document. Without this the
                // OpenAPI JSON has no securitySchemes at all, so Scalar/Swagger UI never show
                // the one-time "Authorize" input and AddPreferredSecuritySchemes below is a no-op.
                openApiOptions.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT"
                    };

                    // Document-level requirement: marks every operation as bearer-secured so the
                    // pasted token is attached to all "try it" requests (same UX as Swagger's
                    // Authorize button). Anonymous endpoints simply ignore the extra header.
                    document.Security ??= new List<OpenApiSecurityRequirement>();
                    document.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document)] = new List<string>()
                    });

                    return Task.CompletedTask;
                });
            });


            //this line will add all the dependencies from the Api project to the DI container
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
                // from silently exposing a whole controller 
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                options.AddPolicy("UserOwnerOrAdmin", policy =>
                    policy.Requirements.Add(new UserOwnerOrAdminRequirement()));
            });

            // if im not wrong but right now we are not using this handler
            builder.Services.AddSingleton<IAuthorizationHandler,UserOwnerOrAdminHandler>();

            // 1. supabase client configuration
            var supabaseUrl = builder.Configuration["Supabase:Url"];
            var supabaseKey = builder.Configuration["StupidKey"];

            if (string.IsNullOrWhiteSpace(supabaseUrl))
            {
                throw new Exception("Supabase URL is not configured in environment variables");
            }

            if (string.IsNullOrWhiteSpace(supabaseKey))
            {
                throw new Exception("Supabase service role key is not configured in environment variables");
            }

            var options = new SupabaseOptions { AutoConnectRealtime = false };
            var supabaseClient = new Supabase.Client(supabaseUrl, supabaseKey, options);
            builder.Services.AddSingleton(supabaseClient);
            builder.Services.AddScoped<IMediaService, SupabaseMediaService>();
         
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            // Unhandled exceptions → 500 ProblemDetails (no stack trace leaked);
            // empty-body 4xx/5xx (e.g. 401/403 from the JWT middleware) → ProblemDetails.
            app.UseExceptionHandler();
            app.UseStatusCodePages();

            // Forwarded headers middleware: if the app is behind a reverse proxy (e.g., Nginx),
    //we need to forward the original request's scheme and IP address so that the JWT middleware can validate the token correctly.
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                // in the future Only trust the request if it comes from a specific proxy IP (e.g., your Nginx server)
                // so no one can spoof the X-Forwarded-For header. For now, we trust all proxies (not ideal for production).

            });

     
            if (app.Environment.IsDevelopment())
            {
                // allow anonymous access to the OpenAPI docs in development, so we can test the endpoints without a JWT
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
