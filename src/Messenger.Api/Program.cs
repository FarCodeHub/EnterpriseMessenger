using Messenger.Infrastructure;
using Messenger.Api.Hubs;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
builder.Services.AddSignalR();

var app = builder.Build();

app.UseCors("AllowLocalTest");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

 
// app.UseHttpsRedirection();

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.Run();