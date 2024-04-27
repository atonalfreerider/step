#!/usr/bin/pwsh

[CmdletBinding(PositionalBinding = $false)]
param (
    [string]$configuration = "Debug",
    [switch]$noTest
)

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Fail([string]$message) {
    throw $message
}

function Single([string]$pattern) {
    $items = @(Get-Item $pattern)
    if ($items.Length -ne 1) {
        $itemsList = $items -Join "`n"
        Fail "Expected single item, found`n$itemsList`n"
    }

    return $items[0]
}

try {
    dotnet restore

    dotnet run --project "$PSScriptRoot/src/IxMilia.Step.Generator.Console/IxMilia.Step.Generator.Console.csproj"

    dotnet build --configuration $configuration 
    if (-Not $noTest) {
        dotnet test --no-restore --no-build --configuration $configuration 
    }
    dotnet pack --no-restore --no-build --configuration $configuration 
    $package = Single "$PSScriptRoot/artifacts/packages/$configuration/*.nupkg"
}
catch {
    Write-Host $_
    Write-Host $_.Exception
    Write-Host $_.ScriptStackTrace
    exit 1
}
