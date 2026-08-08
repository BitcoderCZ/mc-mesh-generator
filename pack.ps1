#!/usr/bin/env pwsh

Param (
	[Parameter(Mandatory=$false)]
	[ValidateSet("Debug", "Release")]
	[string]$Configuration = 'Release'
)

Remove-Item "./nupkgs/*"
dotnet clean
dotnet build -c $Configuration -p:WarningLevel=0 -p:PACKAGE_PUBLISH=true
dotnet pack -c $Configuration -o nupkgs/ -p:WarningLevel=0 -p:PACKAGE_PUBLISH=true
