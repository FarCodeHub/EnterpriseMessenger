using Messenger.Infrastructure;
using Messenger.Api.Hubs;
 
using System.Text;
using Messenger.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Messenger.Api.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
   options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
   {
       Name = "Authorization",
       Type = SecuritySchemeType.Http,
       Scheme = "Bearer",
       BearerFormat = "JWT",
       In = ParameterLocation.Header,
       Description = "Enter JWT token like this: Bearer {your token}"
   });

   options.AddSecurityRequirement(new OpenApiSecurityRequirement
   {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
   });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalTest", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials();
    });
});



 var redisConnectionString = builder.Configuration["Redis:ConnectionString"];

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services
        .AddSignalR()
        .AddStackExchangeRedis(redisConnectionString);
}
else
{
    builder.Services.AddSignalR();
}

 

builder.Services.AddScoped<JwtTokenService>();

var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];

if (string.IsNullOrWhiteSpace(jwtSecretKey))
    throw new InvalidOperationException("JWT secret key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecretKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHostedService<NotificationDispatchWorker>();


var app = builder.Build();

app.UseCors("AllowLocalTest");

app.UseAuthentication();
app.UseAuthorization();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// app.UseHttpsRedirection();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.Run();