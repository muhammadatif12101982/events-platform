using Duende.IdentityServer.Models;

namespace Identity.Server;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new IdentityResource[]
        { 
            new IdentityResources.OpenId()
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        [
            new ApiScope("orders.read", "Read Orders API"),
            new ApiScope("orders.write", "Write Orders API")
        ];

    public static IEnumerable<ApiResource> ApiResources =>
        [
            new ApiResource("orders-api", "Orders API")
            {
                Scopes = {"orders.read", "orders.write"}
            }
        ];

    public static IEnumerable<Client> Clients =>
        [   // Gateway client — machine-to-machine using client credentials flow
            new Client
            {
                ClientId = "gateway-client",
                ClientName = "API Gateway",
                ClientSecrets = { new Secret("gateway-secret".Sha256()) },
                
                // Client credentials = no user involved, service-to-service auth
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = {"orders.read", "orders.write"}
            },
            // Read-only client — can only read, not write
            new Client
            {
                ClientId      = "readonly-client",
                ClientName    = "Read Only Client",
                ClientSecrets = { new Secret("readonly-secret".Sha256()) },
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes     = { "orders.read" }   // no orders.write
            }
        ];
}