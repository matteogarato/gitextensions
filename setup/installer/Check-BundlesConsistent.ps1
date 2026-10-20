[CmdletBinding()]
Param(
    [string] $publishedPath,
    [string] $avaloniaWxsPath
)

[xml]$productWxs = Get-Content $PSScriptRoot/Product.wxs;

[array] $components = @();
$components += $productWxs.SelectNodes("//*[local-name()='File']") | ForEach-Object { $_.Source };
if ($avaloniaWxsPath -and (Test-Path $avaloniaWxsPath))
{
    [xml]$avaloniaWxs = Get-Content $avaloniaWxsPath;
    $components += $avaloniaWxs.SelectNodes("//*[local-name()='File']") | ForEach-Object { $_.Source };
}

$missingItems = @();
Get-ChildItem -Path $publishedPath -Recurse | `
    ForEach-Object {
        if ($_.PsIsContainer -eq $true) {
            return;
        }

        if ($publishedPath.EndsWith('\')) {
            $publishedPath = $publishedPath.TrimEnd('\');
        }

        $componentRelativePath = $_.FullName.Replace($publishedPath, '');
        $compoenentExpectedPath = "`$(var.ArtifactsPublishPath)$componentRelativePath";
        if ($_.Extension -eq '.dic') {
            $compoenentExpectedPath = "..\Bin$componentRelativePath";
        }
        if ($componentRelativePath.Contains('PluginManager')) {
            $componentRelativePath = $_.FullName.Replace("$publishedPath\UserPlugins\GitExtensions.PluginManager", '');
            $compoenentExpectedPath = "`$(var.PluginManagerSourceDir)$componentRelativePath";
        }

        $result = ($components | ? { [string]$_ -eq $compoenentExpectedPath });
        if ($result) {
            Write-Verbose "$($_.FullName) mapped"
        }
        else {
            $missingItems += $_.FullName;
        }
}

if ($missingItems) {
    $items = $missingItems -join "`r`n`t";
    Write-Host "[ERROR] Items not declared in Product.wxs:`r`n`t$items" -ForegroundColor Red
    exit -1;
}
