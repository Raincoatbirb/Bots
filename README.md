# BozTwitchBot - C# Windows Native EXE

## What this is
A Twitch chat bot written in C# that compiles to a single Windows EXE with zero dependencies.

## Building on Windows

### Option 1: GitHub Actions (Automatic)
Upload these files to GitHub and the Actions workflow will build the EXE automatically.

### Option 2: Build Locally on Windows

1. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
2. Open PowerShell in this folder
3. Run:
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./dist
```
4. Find `TwitchBot.exe` in the `dist` folder

## Files
- `Program.cs` - Main bot code
- `TwitchBot.csproj` - Project file
- `Build.ps1` - Windows build script
- `.github/workflows/build.yml` - GitHub Actions CI

## Running
1. Run `TwitchBot.exe`
2. Edit `config.json` with your Twitch credentials
3. Press `C` to connect
4. Use number keys 1-4 to switch tabs