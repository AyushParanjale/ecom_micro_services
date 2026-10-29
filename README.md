# ECom Microservices

The `ecommerce_backend` monolith split into independently deployable services behind an Ocelot API gateway. This is a **straight decomposition**: the public API, business rules and error responses are the same as the monolith's. Nothing new was added except what the split requires.

```
                    ┌──────────────────────────┐
  React SPA ──────► │ ECom.Gateway (Ocelot)    │  :5174  JWT validation, CORS
  (:5173)           └──┬──────┬──────┬──────┬──┘
                       │      │      │      │
               ┌───────▼┐ ┌───▼─────┐ ┌▼─────┐ ┌▼──────────────────────┐
               │ Auth   │ │ Product │ │ Cart │ │ Order (+Payments,     │
               │ :5101  │ │ :5102   │ │:5103 │ │  SignalR, outbox)     │
               └───┬────┘ └─▲──┬────┘ └▲─┬───┘ │ :5104                 │
                   │        │  │       │ │     └──┬─────────┬──────────┘
                   │        │  │  HTTP │ └─HTTP───┘(cart)   │ HTTP (products,
                   │        │  │ (prices/stock)             │  stock decrement/restore)
                   │        └──┼───────┼────────────────────┘
                   │           │       │
                   │      ┌────┴───────┴─────────── Azure Service Bus ─────────────┐
                   │      │ queue orderpaymentfailed → Product                     │◄─┐
                   │      │ queue ordercreated       → Cart                        │◄─┤ Order publishes
                   │      └────────────────────────────────────────────────────────┘  │ via its outbox
   ECommerce_Auth   ECommerce_Product   ECommerce_Cart   ECommerce_Order ──────────────┘
```

## Solution layout

```
ecom_micro_services/
├── ecom_micro_services.slnx
├── Directory.Build.props          net10.0, nullable, implicit usings
├── src/
│   ├── ECom.Shared/               shared core library (see below)
│   ├── ECom.Gateway/              Ocelot: ocelot.json (local), ocelot.Production.json
│   ├── ECom.Auth.Api/             Identity users, login/register, JWT issuing
│   ├── ECom.Product.Api/          catalog, caching, stock (+ idempotent decrement/restore)
│   ├── ECom.Cart.Api/             carts; reads live product data from Product
│   └── ECom.Order.Api/            checkout, orders, payments, SignalR hub, transactional outbox
└── tests/
    ├── ECom.Auth.UnitTests/
    ├── ECom.Product.UnitTests/
    ├── ECom.Cart.UnitTests/
    └── ECom.Order.UnitTests/
```

Each service is a single web project with the same folders: `Controllers/`, `Data/` (DbContext, configurations, migrations, design-time factory), `Entities/`, `DTOs/`, `Repositories/`, `Services/`, plus `Clients/` (typed HttpClients to other services) and `Messaging/` (integration events and handlers) where needed.

### ECom.Shared

| Area | Contents |
|---|---|
| `Exceptions/` | `DomainException`, `NotFoundException`, `ConflictException`, `InvalidCredentialsException`, `GatewayException`, `DownstreamServiceException` |
| `Web/` | `GlobalExceptionHandler`: the monolith's ProblemDetails mapping, plus 503 for unavailable downstream services |
| `Auth/` | `JwtOptions`, `AddEComJwtAuthentication()` (same validation everywhere, optional SignalR query-token path), `Roles`, `AdminOnly` policy, `User.GetUserId()` |
| `Http/` | `BackendApiAuthenticationHttpClientHandler` (forwards the caller's bearer token), `AddDownstreamClient<>()`, translation of downstream ProblemDetails back into the shared exceptions |
| `Hosting/` | `RunEComAsync` (Serilog bootstrap/fatal logging), `AddEComSerilog()`, `AddEComApiDefaults()` / `UseEComApiDefaults()` (controllers, Swagger, ProblemDetails, `/health`) |
| `Caching/` | `ICacheService` and `MemoryCacheService` |
| `Messaging/` | `IMessageBus` / `ServiceBusMessageBus` (one client, cached senders, MessageId/CorrelationId/Subject), `ServiceBusConsumer<T>` (`BackgroundService` on a queue or a topic subscription, a DI scope per message, explicit complete, exceptions → retry/DLQ, bad payloads → DLQ), `IIntegrationEventHandler<T>` |
| `Outbox/` | `OutboxMessage` and its EF configuration, `OutboxRepository<TContext>`, `OutboxProcessor` (per-type handlers, exponential backoff), `OutboxDispatchWorker` |

Each service keeps **its own copies** of the DTOs and event contracts it uses from other services (for example `Cart.Api/Clients/ProductDto.cs`). There is no shared contracts assembly, so the services don't have to be deployed together.

## Service catalog

| Service | Owns | Public routes (via gateway) | Internal routes (not exposed by the gateway) | Calls | Publishes | Consumes |
|---|---|---|---|---|---|---|
| Auth | `Users`, Identity tables | `POST api/auth/register`, `POST api/auth/login`, `GET api/auth/me` | — | — | — | — |
| Product | `Products`, `ProcessedStockOperations` | `GET api/products[?category=]`, `GET api/products/{id}`, `POST/PUT/DELETE` (Admin) | `POST internal/products/batch`, `POST internal/stock/decrement`, `POST internal/stock/restore` | — | — | `OrderPaymentFailed` → restore stock |
| Cart | `Carts`, `CartItems` | `GET api/cart`, `POST api/cart/items`, `PUT/DELETE api/cart/items/{id}` | `GET internal/cart` | Product | — | `OrderCreated` → remove checked-out lines |
| Order | `Orders`, `OrderItems`, `Payments`, `OutboxMessages` | `POST api/checkout`, `POST api/orders` (deprecated), `GET api/orders/my-orders`, `GET api/orders/{id}`, `GET api/orders/{id}/payment`, `POST api/payments`, `GET api/payments/{id}`, `GET api/admin/orders`, hub `/hubs/orders` | — | Cart, Product | `OrderCreated`, `OrderPaymentFailed` (through the outbox) | — |

Data ownership:
- There are no foreign keys or joins across services. `UserId` and `ProductId` are plain values.
- **Cart stores references**: it keeps only product IDs and reads live price and stock on every request.
- **Order stores snapshots**: product name and unit price are copied at checkout.

### How checkout works now

In the monolith, checkout was a single database transaction. It is now:

1. Order reads the caller's cart lines from Cart (`GET internal/cart`, forwarding the JWT).
2. Order reads the products from Product and runs the same checks with the same 409s: `Cart is empty`, `Product unavailable`, `Insufficient stock`.
3. Order asks Product to decrement stock. This is all-or-nothing in Product's own transaction and **idempotent per OrderId**.
4. Order saves the order plus two outbox rows (`OrderStatusChanged` for SignalR, `OrderCreated` for Service Bus) in **one** transaction.
   - If that save fails, Order calls `internal/stock/restore` to put the stock back. Restore is idempotent and leaves a marker so a late decrement for the same order is refused.
5. The outbox worker publishes `OrderCreated`, and Cart removes the checked-out lines.

When a payment is **declined**, the order and payment update and an `OrderPaymentFailed` outbox row are saved in the same transaction, and Product restores the stock when it receives the event. Payment and order state stay in one service, so their consistency is the same as in the monolith.

### Behaviour that differs from the monolith (caused by the split)
- The cart empties **shortly after** checkout, not in the same transaction.
- Stock comes back **shortly after** a declined payment.
- Deleting a product no longer returns 409 when carts or orders reference it, because those live in other services. Carts silently drop missing products (as before), and orders keep their snapshots.
- If a downstream service is unreachable, the response is `503 Service unavailable` (ProblemDetails).

## Running locally

Prerequisites: .NET SDK 10, SQL Server Express (`.\SQLEXPRESS`), `dotnet-ef`, and an Azure Service Bus namespace. The **Basic** tier is enough, because queues are used by default.

### 1. Secrets

`appsettings.json` only contains placeholders, and **secrets never go in committed files**. You can keep local values in either of two places:

- **`appsettings.Development.json`**: this file is git-ignored and is never included in `dotnet publish` output. Each project has a committed template, `appsettings.Development.example.json`. Copy it to `appsettings.Development.json` and fill in the values; for Service Bus, that's `ServiceBus:ConnectionString` in Cart, Product and Order.
  ```powershell
  Get-ChildItem src -Recurse -Filter appsettings.Development.example.json |
    ForEach-Object { Copy-Item $_.FullName ($_.FullName -replace '\.example\.json$', '.json') -ErrorAction SilentlyContinue }
  ```
- **user-secrets**: stored in your Windows profile, outside the repo, and loaded only in Development. If both are set, user-secrets **override** `appsettings.Development.json`.

The Jwt values must be **identical** in the gateway and all four services. To set them with user-secrets:

```powershell
$bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$key = [Convert]::ToBase64String($bytes)   # 64-character random signing key

$projects = "ECom.Gateway","ECom.Auth.Api","ECom.Product.Api","ECom.Cart.Api","ECom.Order.Api"
foreach ($p in $projects) {
  dotnet user-secrets set "Jwt:Issuer"     "ecom-auth-local" --project src/$p
  dotnet user-secrets set "Jwt:Audience"   "ecom-api-local"  --project src/$p
  dotnet user-secrets set "Jwt:SigningKey" $key              --project src/$p
}

# Optional admin seeded at Auth startup (Development only). The password needs upper, lower and a digit, 8+ chars.
dotnet user-secrets set "AdminSeed:Email"    "admin@ecom.local" --project src/ECom.Auth.Api
dotnet user-secrets set "AdminSeed:Password" "<Password1>"      --project src/ECom.Auth.Api

# Service Bus: Send rights for the publisher, Listen rights for the consumers
dotnet user-secrets set "ServiceBus:ConnectionString" "<send connection string>"   --project src/ECom.Order.Api
dotnet user-secrets set "ServiceBus:ConnectionString" "<listen connection string>" --project src/ECom.Cart.Api
dotnet user-secrets set "ServiceBus:ConnectionString" "<listen connection string>" --project src/ECom.Product.Api
```

Check what's set with `dotnet user-secrets list --project src/<Project>`.

`ServiceBus:ConnectionString` must be the **Primary Connection String** of a shared access policy, in the form `Endpoint=sb://<ns>.servicebus.windows.net/;SharedAccessKeyName=...;SharedAccessKey=...`. The namespace URL (`https://<ns>.servicebus.windows.net/`) is not a connection string; if you use it, the service stops at startup with an error explaining where to get the right value. If the setting is empty, the services still start: consumers stay disabled (a warning is logged), and Order's integration-event outbox rows remain pending and are retried with backoff.

### 2. Azure Service Bus entities

The code does not create queues or topics, so create them once yourself.

Each event currently has exactly one consumer, so the default setup uses **queues**. Queues are available on the Basic tier, which is billed per operation and costs almost nothing at dev volume.

| Event (published by Order) | Queue name | Consumer |
|---|---|---|
| `OrderCreated` | `ordercreated` | Cart removes the checked-out lines |
| `OrderPaymentFailed` | `orderpaymentfailed` | Product restores stock |

**Azure portal:**
1. Open the namespace and go to **Entities → Queues → + Queue**.
2. Create `ordercreated` with the default settings. Max delivery count 10 means a message moves to the dead-letter queue after 10 failed attempts.
3. Create `orderpaymentfailed` the same way.
4. Create access keys under **Settings → Shared access policies** (see the commands below).

**az CLI:**

```bash
RG=<resource-group>; NS=<namespace>
az servicebus namespace create -g $RG -n $NS --sku Basic

az servicebus queue create -g $RG --namespace-name $NS -n ordercreated
az servicebus queue create -g $RG --namespace-name $NS -n orderpaymentfailed

# Least-privilege keys
az servicebus namespace authorization-rule create -g $RG --namespace-name $NS -n order-send --rights Send
az servicebus namespace authorization-rule create -g $RG --namespace-name $NS -n consumers-listen --rights Listen
az servicebus namespace authorization-rule keys list -g $RG --namespace-name $NS -n order-send --query primaryConnectionString -o tsv
az servicebus namespace authorization-rule keys list -g $RG --namespace-name $NS -n consumers-listen --query primaryConnectionString -o tsv
```

Entity names are configured under `TopicAndQueueNames` in each service's `appsettings.json`.

#### Switching to topics later (Standard tier)

Topics are needed once an event has **more than one** consumer, for example a future notification service that also needs `OrderCreated`. No code change is required:

1. Upgrade the namespace to **Standard**.
2. Create topics named `ordercreated` and `orderpaymentfailed`, with duplicate detection enabled. A queue and a topic can't share a name, so delete the queues first or use different names in config.
3. Add the subscriptions `OrderCreatedCart` on `ordercreated` and `OrderPaymentFailedProduct` on `orderpaymentfailed`.
4. Set `ServiceBus:UseTopics` to `true` in **Cart** and **Product**. The subscription names come from `TopicAndQueueNames:*Subscription`.
5. Order doesn't need any change. Its sender writes to the configured entity name whether that's a queue or a topic.

On Basic there is no broker-side duplicate detection. That's acceptable here because both consumers are idempotent: Cart removes only matching lines, and Product records a restore once per order.

### 3. Databases

There is one database per service: `ECommerce_{Auth|Product|Cart|Order}_Dev` in Development. Migrations are not applied automatically at startup (same as the monolith):

```powershell
dotnet build ecom_micro_services.slnx
foreach ($s in "Auth","Product","Cart","Order") { dotnet ef database update --project src/ECom.$s.Api }
```

In Development, Auth seeds the admin user and Product seeds the catalog on startup.

### 4. Run

Start each service in its own terminal. Order doesn't matter, but the gateway is usually started last:

```powershell
dotnet run --project src/ECom.Auth.Api
dotnet run --project src/ECom.Product.Api
dotnet run --project src/ECom.Cart.Api
dotnet run --project src/ECom.Order.Api
dotnet run --project src/ECom.Gateway
```

The frontend keeps using `VITE_API_BASE_URL=http://localhost:5174`, which is now the gateway. Each service exposes Swagger in Development at `http://localhost:510x/swagger` and a health check at `/health`.

### 5. Tests

```powershell
dotnet test ecom_micro_services.slnx
```

## Deploying to Azure App Service

User-secrets exist only on your PC. In Azure, every value goes into each App Service's **Environment variables**, which override `appsettings.json`. App Service runs with `ASPNETCORE_ENVIRONMENT=Production` by default, so `appsettings.Development.json` and user-secrets are **not** loaded there.

Naming rule: a nested key such as `ServiceBus:ConnectionString` becomes `ServiceBus__ConnectionString` (two underscores). This works on both Windows and Linux plans; a colon does not work on Linux.

### 1. Create the resources (portal)

| Resource | Notes |
|---|---|
| **App Service plan** | One plan can host all five apps. Use B1 or higher so you can turn on **Always On**, which the outbox worker and Service Bus consumers need. On Free/Shared the apps unload when idle and background work stops. |
| **5 Web Apps** (runtime **.NET 10**) | For example `ecom-gateway`, `ecom-auth-api`, `ecom-product-api`, `ecom-cart-api`, `ecom-order-api`. On each: **Settings → Configuration → General settings**, set **Always On = On**, **Web sockets = On** (gateway and order), and **HTTPS Only = On**. |
| **Azure SQL** | One logical server with 4 databases: `ecom-auth`, `ecom-product`, `ecom-cart`, `ecom-order`. To save credits, check whether the *Azure SQL Database free offer* or the *serverless* tier with auto-pause is available in your subscription. Under **Networking**, enable **Allow Azure services and resources to access this server** and add your client IP so you can run migrations. |
| **Service Bus** | Your existing namespace, with the queues `ordercreated` and `orderpaymentfailed` and the policies `order-send` (Send) and `consumers-listen` (Listen). |

### 2. Database connection strings

On the SQL database, go to **Settings → Connection strings → ADO.NET (SQL authentication)**, copy the value and fill in the password. It looks like:

```
Server=tcp:<server>.database.windows.net,1433;Initial Catalog=ecom-product;User ID=<admin>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

`Trusted_Connection=True` (Windows auth, used locally) does **not** work against Azure SQL.

On **each API app** (not the gateway), add it under **Settings → Environment variables → Connection strings → + Add**:

| Name | Value | Type |
|---|---|---|
| `DefaultConnection` | that service's own database (Auth → `ecom-auth`, Product → `ecom-product`, and so on) | **SQLAzure** |

App Service exposes this to the app as `ConnectionStrings:DefaultConnection`. The Serilog SQL sink also writes warnings and errors to a `Logs` table in the same database.

### 3. App settings

Generate a **new** signing key for production; don't reuse the local one:

```powershell
$b = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Fill($b); [Convert]::ToBase64String($b)
```

Then add the following under **Settings → Environment variables → App settings → + Add** on each app, and click **Apply** at the bottom. The three Jwt values must be identical in all five apps.

| Setting | Gateway | Auth | Product | Cart | Order |
|---|:-:|:-:|:-:|:-:|:-:|
| `Jwt__Issuer` (e.g. `ecom-auth`) | ✔ | ✔ | ✔ | ✔ | ✔ |
| `Jwt__Audience` (e.g. `ecom-api`) | ✔ | ✔ | ✔ | ✔ | ✔ |
| `Jwt__SigningKey` (the new 64-character key) | ✔ | ✔ | ✔ | ✔ | ✔ |
| `Cors__AllowedOrigins__0` = your frontend URL, e.g. `https://<frontend>.azurestaticapps.net` | ✔ | | | | |
| `ServiceBus__ConnectionString` = `consumers-listen` Primary Connection String | | | ✔ | ✔ | |
| `ServiceBus__ConnectionString` = `order-send` Primary Connection String | | | | | ✔ |
| `ServiceUrls__ProductApi` = `https://ecom-product-api.azurewebsites.net` | | | | ✔ | ✔ |
| `ServiceUrls__CartApi` = `https://ecom-cart-api.azurewebsites.net` | | | | | ✔ |

If you add more allowed origins, use `Cors__AllowedOrigins__1`, `Cors__AllowedOrigins__2`, and so on. Optionally, set **Monitoring → Health check** to the path `/health` on every app.

**Optional, recommended: Azure Key Vault.**
1. On each app, go to **Identity** and turn **System assigned = On**.
2. In the Key Vault, assign that identity the **Key Vault Secrets User** role.
3. Store `JwtSigningKey`, `ServiceBusSend`, `ServiceBusListen` and the SQL connection strings as secrets.
4. Use references as the setting values instead of the secrets themselves, e.g. `@Microsoft.KeyVault(VaultName=<vault>;SecretName=JwtSigningKey)`.

The portal shows a green tick when a reference resolves, and rotating a secret then means changing it in one place.

### 4. Gateway routes

`ocelot.Production.json` is loaded in Azure. Replace its placeholder hosts before deploying:

| Placeholder | Replace with |
|---|---|
| `ecom-auth-api.example.internal` | `ecom-auth-api.azurewebsites.net` |
| `ecom-product-api.example.internal` | `ecom-product-api.azurewebsites.net` |
| `ecom-cart-api.example.internal` | `ecom-cart-api.azurewebsites.net` |
| `ecom-order-api.example.internal` | `ecom-order-api.azurewebsites.net` |
| `BaseUrl` `https://ecom-gateway.example.com` | `https://ecom-gateway.azurewebsites.net` |

Leave the ports at 443 and the schemes as `https` (and `wss` for the hub).

### 5. Create the schema (migrations)

Migrations are not applied automatically at startup. Run them from your PC once per database. Your client IP must be allowed in the SQL server firewall.

```powershell
dotnet ef database update --project src/ECom.Auth.Api    --connection "<ecom-auth connection string>"
dotnet ef database update --project src/ECom.Product.Api --connection "<ecom-product connection string>"
dotnet ef database update --project src/ECom.Cart.Api    --connection "<ecom-cart connection string>"
dotnet ef database update --project src/ECom.Order.Api   --connection "<ecom-order connection string>"
```

### 6. Seed the admin user and catalog (one-off)

The admin and product seeds run only in Development, and never in Azure. To seed the Azure databases once, run the two services locally against them:

```powershell
dotnet run --project src/ECom.Auth.Api    --launch-profile http -- --ConnectionStrings:DefaultConnection="<ecom-auth connection string>"
dotnet run --project src/ECom.Product.Api --launch-profile http -- --ConnectionStrings:DefaultConnection="<ecom-product connection string>"
```

Stop each one (Ctrl+C) once it logs `Admin account created.` or `Seeded <n> products.`. The admin uses your local `AdminSeed:*` user-secrets.

### 7. Deploy and check

1. Publish each project to its app. In Visual Studio, use right-click → **Publish** → Azure App Service. In VS Code, use the Azure App Service extension and **Deploy to Web App** on the `bin/Release/net10.0/publish` folder after running `dotnet publish -c Release`.
2. Open `https://<each-app>.azurewebsites.net/health`; each should return `Healthy`.
3. If an app won't start, open **Log stream** on that app. A missing or invalid setting shows up as an explicit startup error, for example `Jwt configuration is invalid` or `'ServiceBus:ConnectionString' is not a valid Service Bus connection string`.
4. Point the frontend's `VITE_API_BASE_URL` at `https://ecom-gateway.azurewebsites.net` and rebuild it.

**Never** let an app run with the placeholder `Jwt` values from `appsettings.json`. The placeholder signing key is public, so tokens could be forged.

## Gateway notes
- Routes that need a JWT have `AuthenticationOptions` in `ocelot.json`. Anonymous routes are: register, login, product list, product detail, and SignalR negotiate. Role checks (Admin) are still enforced by each service.
- `/internal/*` is never routed. Internal endpoints still require the forwarded user JWT and assume a private network between services.
- SignalR: `POST /hubs/orders/negotiate` is proxied over HTTP and `/hubs/orders` over WebSockets, with the token in the `access_token` query string. Only the WebSocket transport is proxied.
- `ocelot.Production.json` is loaded when `ASPNETCORE_ENVIRONMENT=Production`. Replace its placeholder hosts.

## Operational notes
- **Outbox:** if an outbox row hits `Outbox:MaximumAttempts` (default 5), it is not retried automatically. For example, this happens to integration events while Service Bus isn't configured. To replay them:
  `UPDATE OutboxMessages SET AttemptCount = 0, NextAttemptAt = NULL WHERE ProcessedAt IS NULL;`
- **Instances:** the outbox worker assumes a single Order instance, as in the monolith. Consumers are idempotent, so duplicates are harmless.
- **Dead letters:** consumers dead-letter payloads they can't deserialize. Other failures are retried until Service Bus `MaxDeliveryCount`. Monitor the DLQs.
