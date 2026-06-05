Param(
  [string]$Configuration = "Release",
  [string]$Runtime = "win-x64",
  [string]$Output = "release"
)

Write-Host "==> Building solution..." -ForegroundColor Cyan
 dotnet build IISDeployStudio.slnx -c $Configuration

Write-Host "==> Publishing UI to $Output..." -ForegroundColor Cyan
 dotnet publish src/UI/IISDeploy.UI.csproj -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -o $Output

Write-Host "==> Release output ready at ./$Output" -ForegroundColor Green
