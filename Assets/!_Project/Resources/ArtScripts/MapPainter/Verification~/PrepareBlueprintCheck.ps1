$ErrorActionPreference='Stop'
$project='C:/Fork/CodingGame'
$source=Join-Path $project 'Assets/!_Project/Resources/ArtScripts/MapPainter'
$sandbox=Join-Path $PSScriptRoot 'UnitySandbox'
if(Test-Path -LiteralPath $sandbox){throw 'Sandbox already exists; inspect before recreating.'}
New-Item -ItemType Directory -Path "$sandbox/Assets/!_Project/Resources/ArtScripts/MapPainter","$sandbox/Packages" -Force | Out-Null
foreach($folder in @('Runtime','Editor','Shaders','Generated')){
 Copy-Item -LiteralPath "$source/$folder" -Destination "$sandbox/Assets/!_Project/Resources/ArtScripts/MapPainter/$folder" -Recurse
}
Copy-Item -LiteralPath "$project/Assets/!_Project/Resources/Map" -Destination "$sandbox/Assets/!_Project/Resources/Map" -Recurse
Copy-Item -LiteralPath "$project/Assets/Settings" -Destination "$sandbox/Assets/Settings" -Recurse
Copy-Item -LiteralPath "$project/ProjectSettings" -Destination "$sandbox/ProjectSettings" -Recurse
$packageLookup=@{}
Get-ChildItem -LiteralPath "$project/Library/PackageCache" -Directory | ForEach-Object {
 $json=Join-Path $_.FullName 'package.json'
 if(Test-Path -LiteralPath $json){$metadata=Get-Content -LiteralPath $json -Raw | ConvertFrom-Json; $packageLookup[$metadata.name]=@{path=$_.FullName;metadata=$metadata}}
}
$dependencies=[ordered]@{}
function Add-LocalPackage([string]$name){
 if($dependencies.Contains($name)){return}
 if($name.StartsWith('com.unity.modules.')){$dependencies[$name]='1.0.0';return}
 if(!$packageLookup.ContainsKey($name)){throw "Missing cached package: $name"}
 $item=$packageLookup[$name]; $dependencies[$name]='file:'+($item.path -replace '\\','/')
 foreach($dependency in $item.metadata.dependencies.PSObject.Properties){Add-LocalPackage $dependency.Name}
}
Add-LocalPackage 'com.unity.render-pipelines.universal'
foreach($item in (Get-Content -LiteralPath "$project/Packages/manifest.json" -Raw | ConvertFrom-Json).dependencies.PSObject.Properties){if($item.Name.StartsWith('com.unity.modules.')){Add-LocalPackage $item.Name}}
@{dependencies=$dependencies} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "$sandbox/Packages/manifest.json" -Encoding utf8
Write-Output "Prepared isolated Unity check: $sandbox"
