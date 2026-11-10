[CmdletBinding()]
param(
    [ValidateSet('Local', 'Production')]
    [string]$Target = 'Local',
    [ValidatePattern('^(All|Auth|Product|Cart|Order|Gateway)(,(All|Auth|Product|Cart|Order|Gateway))*$')]
    [string[]]$Service = @('All'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# Name matches the Azure host/container app name (see ocelot.Production.json).
$catalog = [ordered]@{
    Auth    = @{ Project = 'ECom.Auth.Api'; Name = 'auth'; HasDatabase = $true }
    Product = @{ Project = 'ECom.Product.Api'; Name = 'product'; HasDatabase = $true }
    Cart    = @{ Project = 'ECom.Cart.Api'; Name = 'cart'; HasDatabase = $true }
    Order   = @{ Project = 'ECom.Order.Api'; Name = 'order'; HasDatabase = $true }
    Gateway = @{ Project = 'ECom.Gateway'; Name = 'gateway'; HasDatabase = $false }
}

# Production base URLs for ServiceUrls:{key}; keep in step with ocelot.Production.json.
$productionServiceUrls = @{
    AuthApi    = 'https://ecom-auth.internal.ashyground-73772ac9.southindia.azurecontainerapps.io'
    ProductApi = 'https://ecom-product.internal.ashyground-73772ac9.southindia.azurecontainerapps.io'
    CartApi    = 'https://ecom-cart.internal.ashyground-73772ac9.southindia.azurecontainerapps.io'
    OrderApi   = 'https://ecom-order.internal.ashyground-73772ac9.southindia.azurecontainerapps.io'
}

$Service = @($Service | ForEach-Object { $_ -split ',' } | ForEach-Object { (Get-Culture).TextInfo.ToTitleCase($_.Trim().ToLowerInvariant()) })
$selected = if ($Service -contains 'All') { @($catalog.Keys) } else { @($Service | Select-Object -Unique) }
$suffix = if ($Target -eq 'Production') { 'production' } else { 'local' }

$fileName = ".env.$suffix"
function Get-OutputPath([hashtable]$Entry) { Join-Path $root "src/$($Entry.Project)/$fileName" }
function Get-DisplayPath([hashtable]$Entry) { "src/$($Entry.Project)/$fileName" }

$existing = @($selected | Where-Object { Test-Path (Get-OutputPath $catalog[$_]) } |
    ForEach-Object { Get-DisplayPath $catalog[$_] })
if ($existing.Count -gt 0 -and -not $Force) {
    throw "$($existing -join ', ') already exist(s). Use -Force only to replace them, including manual edits."
}

function Read-JsonFile([string]$Path, [string]$Description) {
    try {
        return Get-Content $Path -Raw | ConvertFrom-Json -AsHashtable
    }
    catch {
        throw "Unable to parse $Description. Values have not been printed."
    }
}

function Add-ConfigurationValues {
    param($Values, $Node, [string]$Prefix = '')

    if ($Node -is [System.Collections.IDictionary]) {
        foreach ($key in $Node.Keys) {
            $path = if ($Prefix) { "${Prefix}__$key" } else { [string]$key }
            Add-ConfigurationValues -Values $Values -Node $Node[$key] -Prefix $path.Replace(':', '__')
        }
    }
    elseif ($Node -is [System.Collections.IList]) {
        for ($index = 0; $index -lt $Node.Count; $index++) {
            Add-ConfigurationValues -Values $Values -Node $Node[$index] -Prefix "${Prefix}__$index"
        }
    }
    elseif ($null -ne $Node) {
        $Values[$Prefix] = [Convert]::ToString($Node, [Globalization.CultureInfo]::InvariantCulture)
    }
}

function Remove-ConfigurationSection($Values, [string]$Section) {
    foreach ($key in @($Values.Keys | Where-Object { $_ -like "${Section}__*" -or $_ -eq $Section })) {
        [void]$Values.Remove($key)
    }
}

$jwtKeys = @('Jwt__Issuer', 'Jwt__Audience', 'Jwt__SigningKey', 'Jwt__EncryptionKey')
$results = [ordered]@{}

foreach ($serviceName in $selected) {
    $entry = $catalog[$serviceName]
    $projectDir = Join-Path $root "src/$($entry.Project)"
    $project = Join-Path $projectDir "$($entry.Project).csproj"

    $secretOutput = & dotnet user-secrets list --project $project --json 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read .NET user secrets for $($entry.Project). Check the SDK and its UserSecretsId."
    }
    try {
        $secrets = ($secretOutput -join [Environment]::NewLine) | ConvertFrom-Json -AsHashtable
    }
    catch {
        throw "Unable to parse user secrets for $($entry.Project). Values have not been printed."
    }
    if (-not $secrets -or $secrets.Count -eq 0) {
        throw "No user secrets were found for $($entry.Project)."
    }

    $values = [System.Collections.Generic.SortedDictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    Add-ConfigurationValues -Values $values -Node (Read-JsonFile (Join-Path $projectDir 'appsettings.json') "$($entry.Project) app settings")
    $developmentPath = Join-Path $projectDir 'appsettings.Development.json'
    if (Test-Path $developmentPath) {
        Add-ConfigurationValues -Values $values -Node (Read-JsonFile $developmentPath "$($entry.Project) development settings")
    }
    Add-ConfigurationValues -Values $values -Node $secrets
    $values['ASPNETCORE_ENVIRONMENT'] = 'Production'
    $values['ASPNETCORE_HTTP_PORTS'] = '8080'

    foreach ($required in $jwtKeys) {
        if ([string]::IsNullOrWhiteSpace($values[$required]) -or $values[$required].StartsWith('CONFIGURE_VIA_') -or $values[$required].StartsWith('REPLACE_')) {
            throw "Missing user secret for $($entry.Project): $($required.Replace('__', ':'))."
        }
    }
    if ($values['Jwt__SigningKey'].Length -lt 32) {
        throw "Jwt:SigningKey for $($entry.Project) must contain at least 32 characters."
    }
    try {
        $encryptionBytes = [Convert]::FromBase64String($values['Jwt__EncryptionKey'])
    }
    catch {
        throw "Jwt:EncryptionKey for $($entry.Project) must be base64 encoding of exactly 32 bytes."
    }
    if ($encryptionBytes.Length -ne 32) {
        throw "Jwt:EncryptionKey for $($entry.Project) must be base64 encoding of exactly 32 bytes."
    }

    if ($Target -eq 'Production') {
        if ($entry.HasDatabase) {
            $values['ConnectionStrings__DefaultConnection'] = 'Server=tcp:REPLACE_SERVER.database.windows.net,1433;Database=REPLACE_DATABASE;User ID=REPLACE_USER;Password=REPLACE_PASSWORD;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
        }
        if ($values.ContainsKey('SerilogDb__AutoCreateSqlTable')) {
            $values['SerilogDb__AutoCreateSqlTable'] = 'false'
        }
        foreach ($key in @($values.Keys | Where-Object { $_ -like 'ServiceUrls__*' })) {
            $urlKey = $key.Substring('ServiceUrls__'.Length)
            $values[$key] = if ($productionServiceUrls.ContainsKey($urlKey)) { $productionServiceUrls[$urlKey] } else { "https://REPLACE_$($urlKey.ToUpperInvariant())_HOST" }
        }
        if ($values.Keys | Where-Object { $_ -like 'Cors__AllowedOrigins__*' }) {
            Remove-ConfigurationSection $values 'Cors__AllowedOrigins'
            $values['Cors__AllowedOrigins__0'] = 'https://ecom-frontend.ashyground-73772ac9.southindia.azurecontainerapps.io'
        }
    }

    $results[$serviceName] = $values
}

# Every service validates the same tokens, so the JWT settings must match exactly.
if ($results.Count -gt 1) {
    $first = @($results.Keys)[0]
    foreach ($serviceName in $results.Keys) {
        foreach ($key in $jwtKeys) {
            if ($results[$serviceName][$key] -cne $results[$first][$key]) {
                throw "$($key.Replace('__', ':')) differs between $($catalog[$first].Project) and $($catalog[$serviceName].Project). Align the user secrets; values have not been printed."
            }
        }
    }
}

foreach ($serviceName in $results.Keys) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# Private runtime configuration for $($catalog[$serviceName].Project). Never commit or bake into an image.")
    $lines.Add('# Docker --env-file syntax: unquoted NAME=value; no variable expansion.')
    foreach ($item in $results[$serviceName].GetEnumerator()) {
        if ($item.Key -notmatch '^[A-Za-z_][A-Za-z0-9_.-]*$' -or $item.Value.IndexOfAny([char[]]"`r`n`0") -ge 0) {
            throw "A configuration key or multiline value in $($catalog[$serviceName].Project) cannot be represented safely in a Docker env file."
        }
        $lines.Add("$($item.Key)=$($item.Value)")
    }

    [System.IO.File]::WriteAllLines((Get-OutputPath $catalog[$serviceName]), $lines, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Created $(Get-DisplayPath $catalog[$serviceName]) from app settings and user secrets. No secret values were printed."
}

if ($Target -eq 'Production') {
    Write-Host 'Replace the Azure SQL, ServiceUrls and Cors placeholders before use.'
    Write-Host 'Review copied development settings/credentials (Jwt:EncryptionKey, ServiceBus) and remove AdminSeed entries from the auth-api file after bootstrap.'
}
else {
    Write-Host 'ServiceUrls point at localhost; inside containers change them to reachable hosts (e.g. container names on a shared Docker network or host.docker.internal).'
}
Write-Host 'The gateway runs as Production, so it routes using ocelot.Production.json.'
