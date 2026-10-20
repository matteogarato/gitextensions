[CmdletBinding()]
Param(
    [Parameter(Mandatory = $true)]
    [string] $PublishPath,
    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$publishRoot = (Resolve-Path $PublishPath).Path.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
$files = Get-ChildItem -Path $publishRoot -File -Recurse | Sort-Object FullName
if ($files.Count -eq 0)
{
    throw "No Avalonia publish files were found in '$publishRoot'."
}

$namespace = 'http://schemas.microsoft.com/wix/2006/wi'
$document = [System.Xml.XmlDocument]::new()
$document.AppendChild($document.CreateXmlDeclaration('1.0', 'utf-8', $null)) | Out-Null
$wix = $document.CreateElement('Wix', $namespace)
$document.AppendChild($wix) | Out-Null
$directoryFragment = $document.CreateElement('Fragment', $namespace)
$wix.AppendChild($directoryFragment) | Out-Null
$directoryRef = $document.CreateElement('DirectoryRef', $namespace)
$directoryRef.SetAttribute('Id', 'AvaloniaAppDir')
$directoryFragment.AppendChild($directoryRef) | Out-Null
$directoryMap = @{ '' = $directoryRef }
$componentIds = New-Object 'System.Collections.Generic.List[string]'

foreach ($file in $files)
{
    $relativePath = $file.FullName.Substring($publishRoot.Length)
    $relativeDirectory = [IO.Path]::GetDirectoryName($relativePath)
    if ($relativeDirectory)
    {
        $currentPath = ''
        $parent = $directoryRef
        foreach ($directoryName in ($relativeDirectory -split '[\\/]'))
        {
            $currentPath = if ($currentPath) { Join-Path $currentPath $directoryName } else { $directoryName }
            if (-not $directoryMap.ContainsKey($currentPath))
            {
                $hashInput = [Text.Encoding]::UTF8.GetBytes($currentPath)
                $sha = [Security.Cryptography.SHA256]::Create()
                try
                {
                    $hash = [BitConverter]::ToString($sha.ComputeHash($hashInput)).Replace('-', '').Substring(0, 16)
                }
                finally
                {
                    $sha.Dispose()
                }

                $directory = $document.CreateElement('Directory', $namespace)
                $directory.SetAttribute('Id', "AvaloniaDir_$hash")
                $directory.SetAttribute('Name', $directoryName)
                $parent.AppendChild($directory) | Out-Null
                $directoryMap[$currentPath] = $directory
            }

            $parent = $directoryMap[$currentPath]
        }
    }
    else
    {
        $parent = $directoryRef
    }

    $hashInput = [Text.Encoding]::UTF8.GetBytes($relativePath)
    $sha = [Security.Cryptography.SHA256]::Create()
    try
    {
        $hash = [BitConverter]::ToString($sha.ComputeHash($hashInput)).Replace('-', '').Substring(0, 16)
    }
    finally
    {
        $sha.Dispose()
    }

    $componentId = "AvaloniaComponent_$hash"
    $fileId = if ($file.Name -eq 'GitExtensions.Avalonia.exe') { 'GitExtensionsAvaloniaExe' } else { "AvaloniaFile_$hash" }
    $component = $document.CreateElement('Component', $namespace)
    $component.SetAttribute('Id', $componentId)
    $component.SetAttribute('Guid', '*')
    $parent.AppendChild($component) | Out-Null

    $wixPath = $relativePath.Replace('/', '\')
    $wixFile = $document.CreateElement('File', $namespace)
    $wixFile.SetAttribute('Id', $fileId)
    $wixFile.SetAttribute('Source', "`$(var.ArtifactsPublishPath)\Avalonia\$wixPath")
    $wixFile.SetAttribute('KeyPath', 'yes')
    $component.AppendChild($wixFile) | Out-Null
    $componentIds.Add($componentId)
}

$groupFragment = $document.CreateElement('Fragment', $namespace)
$wix.AppendChild($groupFragment) | Out-Null
$componentGroup = $document.CreateElement('ComponentGroup', $namespace)
$componentGroup.SetAttribute('Id', 'AvaloniaAppFiles')
$groupFragment.AppendChild($componentGroup) | Out-Null
foreach ($componentId in $componentIds)
{
    $componentRef = $document.CreateElement('ComponentRef', $namespace)
    $componentRef.SetAttribute('Id', $componentId)
    $componentGroup.AppendChild($componentRef) | Out-Null
}

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -Path $outputDirectory -ItemType Directory -Force | Out-Null
$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [System.Text.UTF8Encoding]::new($false)
$writer = [System.Xml.XmlWriter]::Create($OutputPath, $settings)
try
{
    $document.Save($writer)
}
finally
{
    $writer.Dispose()
}
