[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $Publish,

    [switch] $SelfContained,

    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$solution = Join-Path $root 'PowerMonitorDashboard.sln'
$appProject = Join-Path $root 'src\PowerMonitorDashboard.App\PowerMonitorDashboard.App.csproj'

Push-Location $root
try {
    Write-Host '==> Restoring packages' -ForegroundColor Cyan
    dotnet restore $solution
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    Write-Host '==> Building solution' -ForegroundColor Cyan
    dotnet build $solution -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    if (-not $SkipTests) {
        Write-Host '==> Running tests' -ForegroundColor Cyan
        dotnet test $solution -c $Configuration --no-build --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    }

    if ($Publish) {
        $outDir = Join-Path $root 'artifacts\publish'
        Write-Host "==> Publishing to $outDir" -ForegroundColor Cyan
        $selfContainedValue = if ($SelfContained) { 'true' } else { 'false' }
        dotnet publish $appProject -c $Configuration -r win-x64 --self-contained $selfContainedValue -o $outDir
        if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
        Write-Host "==> Publish completed: $outDir" -ForegroundColor Green
    }

    Write-Host '==> Build pipeline finished successfully.' -ForegroundColor Green
}
finally {
    Pop-Location
}
