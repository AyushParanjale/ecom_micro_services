# Ecommerce Microservices

## Running in containers

Each service has `src/<Project>/Dockerfile` plus a `Dockerfile.dockerignore` next to it. Build from this folder,
because every image needs `ECom.Shared` and `Directory.Build.*`:

```powershell
docker build -f src/ECom.Order.Api/Dockerfile -t ecom-order-api .
```

`docker-compose.yml` runs the whole stack from `src/*/.env.production` (see below):

```powershell
docker compose up -d --build     # gateway :8000, auth :8001, product :8002, cart :8003, order :8004
docker compose logs -f order-api
docker compose down
```

- Compose overrides `ServiceUrls:*` with the in-network hosts (`http://product-api:8080`, ...) and mounts
  `src/ECom.Gateway/ocelot.Docker.json` over the image's `ocelot.Production.json`.
- When adding a gateway route, add it to `ocelot.json`, `ocelot.Production.json` and `ocelot.Docker.json`.
- Azure SQL must allow this machine's public IP in its server firewall, otherwise the APIs restart in a loop with error 40615.

`azure-pipelines.yml` builds and unit-tests the solution, then builds and pushes one image per service to the
`ecomcr_connection` registry (`ecom-auth-api`, `ecom-product-api`, `ecom-cart-api`, `ecom-order-api`, `ecom-gateway`,
tagged with the build ID and `latest`). Pull request builds skip the push.

The deployed container app names and URLs are listed in [AZURE_ENDPOINTS.md](AZURE_ENDPOINTS.md).

## Container environment scripts

`scripts/Export-ContainerEnvironment.ps1` builds one Docker `--env-file` per service, written to
`src/<Project>/.env.local` or `src/<Project>/.env.production`, from its `appsettings.json`,
`appsettings.Development.json` and .NET user secrets. No secret values are printed.

```powershell
pwsh -File scripts/Export-ContainerEnvironment.ps1                                  # src/*/.env.local for all services
pwsh -File scripts/Export-ContainerEnvironment.ps1 -Target Production               # src/*/.env.production
pwsh -File scripts/Export-ContainerEnvironment.ps1 -Service Cart,Gateway -Force     # subset, overwriting existing files
```

- Services: `ECom.Auth.Api`, `ECom.Product.Api`, `ECom.Cart.Api`, `ECom.Order.Api`, `ECom.Gateway`.
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` and `Jwt:EncryptionKey` must be identical across all services; the script fails otherwise.
- `Production` replaces the SQL connection strings, `ServiceUrls:*` (hosts from `ocelot.Production.json`) and the gateway's `Cors:AllowedOrigins` with placeholders to fill in. Remove `AdminSeed__*` from the auth-api file after bootstrap.
- `Local` keeps `ServiceUrls` on `localhost`; change them to reachable hosts when running in containers.
- Product catalogue seed: runs at product-api startup in `Development`, or anywhere when `Seed__Products=true` (set for product-api in `docker-compose.yml`). It is idempotent — it only inserts seed products whose name is missing and fills in `ImageUrl` where it is empty; admin-created or edited products are never overwritten.

`scripts/GenerateEnvFillCli.ps1` turns those files into `az containerapp update` commands and copies them to the clipboard:

```powershell
pwsh -File scripts/GenerateEnvFillCli.ps1 -ResourceGroup my-rg                       # all services, src/*/.env.production -> ecom-<service>
pwsh -File scripts/GenerateEnvFillCli.ps1 -ResourceGroup my-rg -Service Order -ContainerAppPrefix prod-ecom-
pwsh -File scripts/GenerateEnvFillCli.ps1 -ResourceGroup my-rg -ContainerAppName my-app -EnvFile src/ECom.Cart.Api/.env.production
```

The generated `.env.*` files are git-ignored; never commit them or bake them into an image.
