[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot 'KLEP.sln'
$testRoot = Join-Path $repositoryRoot 'tests'

Push-Location $repositoryRoot
try {
    dotnet build $solution --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "KLEP solution build failed with exit code $LASTEXITCODE."
    }

    $projects = Get-ChildItem -LiteralPath $testRoot -Recurse -Filter '*.csproj' -File |
        Sort-Object FullName

    foreach ($project in $projects) {
        Write-Host "Running $($project.Directory.Name)..."
        dotnet run --project $project.FullName --configuration Release --no-build --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "$($project.Directory.Name) failed with exit code $LASTEXITCODE."
        }
    }

    Write-Host "All $($projects.Count) KLEP contract suites passed."
}
finally {
    Pop-Location
}
