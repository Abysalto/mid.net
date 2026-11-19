using System.Text.Json.Serialization;
using AbySalto.Mid.Application.Common.Interfaces;
using AbySalto.Mid.Auth;
using AbySalto.Mid.Errors;
using AbySalto.Mid.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace AbySalto.Mid
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPresentation(this IServiceCollection services)
        {
            services.AddControllers()
                .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            services.AddOpenApi();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "AbySalto", Version = "v1" });

                var bearer = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                };
                c.AddSecurityDefinition("Bearer", bearer);
                c.AddSecurityRequirement(new OpenApiSecurityRequirement { [bearer] = [] });
            });

            services.AddProblemDetails();
            services.AddExceptionHandler<ApiExceptionHandler>();

            services.AddJwtAuthentication();
            services.AddAuthRateLimiting();

            return services;
        }

        private static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
                {
                    bearer.MapInboundClaims = false;
                    bearer.TokenValidationParameters = JwtValidation.CreateParameters(jwt.Value);

                    bearer.Events = new JwtBearerEvents
                    {
                        OnChallenge = async context =>
                        {
                            context.HandleResponse();
                            var (code, detail) = context.AuthenticateFailure switch
                            {
                                SecurityTokenExpiredException => ("auth.token_expired", "The access token has expired. Log in again."),
                                not null => ("auth.token_invalid", "The access token is invalid. Log in again to get a new one."),
                                null => ("auth.token_missing", "An access token is required. Send it as 'Authorization: Bearer <token>'."),
                            };
                            Logger(context.HttpContext).LogInformation(
                                context.AuthenticateFailure,
                                "{Method} {Path} -> 401: {Detail}",
                                context.Request.Method, context.Request.Path, detail);
                            await WriteAuthProblemAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", detail, code);
                        },
                        OnForbidden = context =>
                        {
                            Logger(context.HttpContext).LogInformation(
                                "{Method} {Path} -> 403 for user {UserId}",
                                context.Request.Method, context.Request.Path, context.Principal?.FindFirst("sub")?.Value);
                            return WriteAuthProblemAsync(
                                context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden", "You don't have access to this resource.",
                                "auth.forbidden");
                        },
                    };
                });

            services.AddAuthorization();

            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUser, CurrentUser>();

            return services;
        }

        private static ILogger Logger(HttpContext http) =>
            http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AbySalto.Mid.Auth.JwtBearer");

        private static async Task WriteAuthProblemAsync(HttpContext http, int status, string title, string detail, string code)
        {
            http.Response.StatusCode = status;
            await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = new ProblemDetails
                {
                    Status = status, Title = title, Detail = detail,
                    Extensions = { ["code"] = code },
                },
            });
        }
    }
}
