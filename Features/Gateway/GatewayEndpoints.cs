namespace App.Features.Gateway;

public static class GatewayEndpoints
{
    public static void MapGatewayEndpoints(this WebApplication app)
    {
        app.MapReverseProxy();
    }
}
