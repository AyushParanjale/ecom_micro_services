# Azure Container Apps endpoints

Container Apps environment domain: `ashyground-73772ac9.southindia.azurecontainerapps.io`

| Service | Container app | Ingress | Endpoint |
|---|---|---|---|
| Gateway | `ecom-gateway` | External | https://ecom-gateway.ashyground-73772ac9.southindia.azurecontainerapps.io |
| Auth | `ecom-auth` | Internal | https://ecom-auth.internal.ashyground-73772ac9.southindia.azurecontainerapps.io |
| Product | `ecom-product` | Internal | https://ecom-product.internal.ashyground-73772ac9.southindia.azurecontainerapps.io |
| Cart | `ecom-cart` | Internal | https://ecom-cart.internal.ashyground-73772ac9.southindia.azurecontainerapps.io |
| Order | `ecom-order` | Internal | https://ecom-order.internal.ashyground-73772ac9.southindia.azurecontainerapps.io |
| Frontend | `ecom-frontend` | External | https://ecom-frontend.ashyground-73772ac9.southindia.azurecontainerapps.io |

Only the gateway and frontend are reachable from the internet. The frontend's `VITE_API_BASE_URL` must point at the
gateway, and the gateway's `Cors__AllowedOrigins__0` must be the frontend URL.

Where these URLs are used:

- `src/ECom.Gateway/ocelot.Production.json`: downstream hosts for every route, and `BaseUrl` (gateway).
- `ServiceUrls__*` in `src/ECom.Cart.Api/.env.production` (ProductApi) and `src/ECom.Order.Api/.env.production` (CartApi, ProductApi).
- `scripts/Export-ContainerEnvironment.ps1` (`$productionServiceUrls`) and the container app names in
  `scripts/Export-ContainerEnvironment.ps1` / `scripts/GenerateEnvFillCli.ps1`.
