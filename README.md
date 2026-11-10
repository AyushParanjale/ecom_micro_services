# Ecommerce Microservices

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

`scripts/GenerateEnvFillCli.ps1` turns those files into `az containerapp update` commands and copies them to the clipboard:

```powershell
pwsh -File scripts/GenerateEnvFillCli.ps1 -ResourceGroup my-rg                       # all services, src/*/.env.production -> ecom-<service>
pwsh -File scripts/GenerateEnvFillCli.ps1 -ResourceGroup my-rg -Service Order -ContainerAppPrefix prod-ecom-
pwsh -File scripts/GenerateEnvFillCli.ps1 -ResourceGroup my-rg -ContainerAppName my-app -EnvFile src/ECom.Cart.Api/.env.production
```

The generated `.env.*` files are git-ignored; never commit them or bake them into an image.
