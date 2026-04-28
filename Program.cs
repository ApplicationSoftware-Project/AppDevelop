using App.Features.AI;
using App.Features.Bootstrap;
using App.Features.Gateway;
using App.Features.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppBootstrap(builder.Configuration);

var app = builder.Build();

// 2. 미들웨어 설정 (Pipeline)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthEndpoints();
app.MapAiEndpoints();
app.MapGatewayEndpoints();
app.MapAnalysisEndpoints();

app.Run();
