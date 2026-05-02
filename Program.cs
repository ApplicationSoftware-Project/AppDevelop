using App.Features.AI;
using App.Features.AI.Data;
using App.Features.Analysis;
using App.Features.Auth;
using App.Features.Bootstrap;
using App.Features.Gateway;
using App.Features.Health;
using App.Features.Receipt;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppBootstrap(builder.Configuration);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapAiEndpoints();
AnalysisEndpoints.MapAnalysisEndpoints(app);
app.MapReceiptEndpoints();
app.MapGatewayEndpoints();

app.Run();