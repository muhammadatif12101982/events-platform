using Serilog;

namespace Identity.Server;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        // uncomment if you want to add a UI
        //builder.Services.AddRazorPages();

        var isBuilder = builder.Services.AddIdentityServer(options =>
        {
            // https://docs.duendesoftware.com/identityserver/v6/fundamentals/resources/api_scopes#authorization-based-on-scopes
            options.EmitStaticAudienceClaim = true;
                
            // Disable automatic key management — use developer signing key instead
            options.KeyManagement.Enabled = false;
        })
        .AddInMemoryIdentityResources(Config.IdentityResources)
        .AddInMemoryApiScopes(Config.ApiScopes)
        .AddInMemoryApiResources(Config.ApiResources)
        .AddInMemoryClients(Config.Clients);

        // Use a developer signing credential — perfect for development/learning
        // In production this would be a real certificate from Key Vault (Week 7)
        if (builder.Environment.IsDevelopment())
        {
            //isBuilder.AddDeveloperSigningCredential();

            // To this (persistKey: false = keep in memory only, no file writes):
            isBuilder.AddDeveloperSigningCredential(persistKey: false);
        }

        return builder.Build();
    }
    
    public static WebApplication ConfigurePipeline(this WebApplication app)
    { 
        app.UseSerilogRequestLogging();
    
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // uncomment if you want to add a UI
        //app.UseStaticFiles();
        //app.UseRouting();
            
        app.UseIdentityServer();

        // uncomment if you want to add a UI
        //app.UseAuthorization();
        //app.MapRazorPages().RequireAuthorization();

        return app;
    }
}
