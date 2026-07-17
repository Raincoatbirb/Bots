# Build script for Windows
# Run this on your Windows PC with .NET SDK installed

dotnet restore
dotnet build --configuration Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist

Write-Host "Built: dist/TwitchBot.exe"