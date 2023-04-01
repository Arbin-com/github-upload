[System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription

$utilProjPath = "$PSScriptRoot/ArbinUtil/ArbinUtil.csproj"
dotnet restore "$PSScriptRoot/ArbinUtil.sln"
#dotnet build $utilProjPath --configuration Release -o "$PSScriptRoot/bin"
dotnet publish "$PSScriptRoot/ArbinUtil.sln" -o "$PSScriptRoot/bin" --configuration Release

ipmo "$PSScriptRoot/bin/ArbinUtil.dll" -Verbose