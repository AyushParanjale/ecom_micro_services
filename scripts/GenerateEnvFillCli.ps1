[CmdletBinding(DefaultParameterSetName = 'ByService')]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    # Explicit mode, same as the monolith script: one env file -> one container app.
    [Parameter(Mandatory = $true, ParameterSetName = 'ByFile')]
    [string]$ContainerAppName,

    [Parameter(Mandatory = $true, ParameterSetName = 'ByFile')]
    [string]$EnvFile,

    # Convention mode: src/<Project>/.env.<target> -> <prefix><name>, e.g. src/ECom.Cart.Api/.env.production -> ecom-cart.
    [Parameter(ParameterSetName = 'ByService')]
    [ValidatePattern('^(All|Auth|Product|Cart|Order|Gateway)(,(All|Auth|Product|Cart|Order|Gateway))*$')]
    [string[]]$Service = @('All'),

    [Parameter(ParameterSetName = 'ByService')]
    [ValidateSet('Local', 'Production')]
    [string]$Target = 'Production',

    [Parameter(ParameterSetName = 'ByService')]
    [string]$ContainerAppPrefix = 'ecom-'
)

$ErrorActionPreference = 'Stop'

function Get-ContainerAppCommand([string]$File, [string]$AppName) {
    if (!(Test-Path $File)) {
        throw "Env file not found: $File"
    }

    $envVars = @()
    Get-Content $File | ForEach-Object {
        $line = $_.Trim()

        # Skip blank lines and comments
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
            return
        }

        if ($line -match '^(.*?)=(.*)$') {
            $name = $matches[1].Trim()
            $value = $matches[2].Trim().Replace('"', '\"')
            $envVars += "$name=`"$value`""
        }
    }

    if ($envVars.Count -eq 0) {
        throw "No variables found in $File"
    }

    return "az containerapp update --name $AppName --resource-group $ResourceGroup --set-env-vars $($envVars -join ' ')"
}

$commands = [System.Collections.Generic.List[string]]::new()

if ($PSCmdlet.ParameterSetName -eq 'ByFile') {
    $commands.Add((Get-ContainerAppCommand $EnvFile $ContainerAppName))
}
else {
    $root = Split-Path $PSScriptRoot -Parent
    $names = [ordered]@{
        Auth    = @{ Project = 'ECom.Auth.Api'; Name = 'auth' }
        Product = @{ Project = 'ECom.Product.Api'; Name = 'product' }
        Cart    = @{ Project = 'ECom.Cart.Api'; Name = 'cart' }
        Order   = @{ Project = 'ECom.Order.Api'; Name = 'order' }
        Gateway = @{ Project = 'ECom.Gateway'; Name = 'gateway' }
    }
    $Service = @($Service | ForEach-Object { $_ -split ',' } | ForEach-Object { (Get-Culture).TextInfo.ToTitleCase($_.Trim().ToLowerInvariant()) })
    $selected = if ($Service -contains 'All') { @($names.Keys) } else { @($Service | Select-Object -Unique) }
    $suffix = $Target.ToLowerInvariant()

    foreach ($serviceName in $selected) {
        $entry = $names[$serviceName]
        $file = Join-Path $root "src/$($entry.Project)/.env.$suffix"
        if (!(Test-Path $file)) {
            throw "Env file not found: $file. Run scripts/Export-ContainerEnvironment.ps1 -Target $Target first."
        }
        $commands.Add((Get-ContainerAppCommand $file "$ContainerAppPrefix$($entry.Name)"))
    }
}

$output = $commands -join [Environment]::NewLine
try {
    $output | Set-Clipboard
    $copied = $true
}
catch {
    $copied = $false
}

Write-Host ''
Write-Host $(if ($copied) { 'Copied to clipboard:' } else { 'Clipboard unavailable; copy the command(s) below:' })
Write-Host '----------------------------------------'
Write-Host $output
Write-Host '----------------------------------------'
