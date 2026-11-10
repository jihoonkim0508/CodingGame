$ErrorActionPreference='Stop'
$project='C:/Fork/CodingGame'
$sandbox=Join-Path $PSScriptRoot 'UnitySandbox'
if(Test-Path -LiteralPath $sandbox){throw 'Sandbox already exists.'}
$target="$sandbox/Assets/!_Project/Resources"
New-Item -ItemType Directory -Path "$target/ArtScripts/MapPainter","$target/ArtScripts/WorkshopArt","$target/ArtPrefabs","$target/Models/MonsterBase","$sandbox/Packages" -Force | Out-Null
foreach($folder in @('Runtime','Editor','Shaders','Generated')){Copy-Item -LiteralPath "$project/Assets/!_Project/Resources/ArtScripts/MapPainter/$folder" -Destination "$target/ArtScripts/MapPainter/$folder" -Recurse}
Copy-Item -LiteralPath "$project/Assets/!_Project/Resources/ArtScripts/WorkshopArt/Editor" -Destination "$target/ArtScripts/WorkshopArt/Editor" -Recurse
Copy-Item -LiteralPath "$project/Assets/!_Project/Resources/Models/Stage01_Prop" -Destination "$target/Models/Stage01_Prop" -Recurse
Copy-Item -LiteralPath "$project/Assets/!_Project/Resources/Map" -Destination "$target/Map" -Recurse
foreach($file in @('Stage01.prefab','Stage01.prefab.meta')){Copy-Item -LiteralPath "$project/Assets/!_Project/Resources/ArtPrefabs/$file" -Destination "$target/ArtPrefabs/$file"}
foreach($file in @('MonsterBaseFix.fbx','MonsterBaseFix.fbx.meta')){Copy-Item -LiteralPath "$project/Assets/!_Project/Resources/Models/MonsterBase/$file" -Destination "$target/Models/MonsterBase/$file"}
Copy-Item -LiteralPath "$project/Assets/Settings" -Destination "$sandbox/Assets/Settings" -Recurse
Copy-Item -LiteralPath "$project/ProjectSettings" -Destination "$sandbox/ProjectSettings" -Recurse
$lookup=@{}
Get-ChildItem -LiteralPath "$project/Library/PackageCache" -Directory | ForEach-Object {$path=Join-Path $_.FullName 'package.json';if(Test-Path -LiteralPath $path){$json=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json;$lookup[$json.name]=@{path=$_.FullName;json=$json}}}
$dependencies=[ordered]@{}
function Add-Package([string]$name){if($dependencies.Contains($name)){return};if($name.StartsWith('com.unity.modules.')){$dependencies[$name]='1.0.0';return};if(!$lookup.ContainsKey($name)){throw "Missing $name"};$item=$lookup[$name];$dependencies[$name]='file:'+($item.path -replace '\\','/');foreach($dep in $item.json.dependencies.PSObject.Properties){Add-Package $dep.Name}}
Add-Package 'com.unity.render-pipelines.universal'
foreach($item in (Get-Content "$project/Packages/manifest.json" -Raw|ConvertFrom-Json).dependencies.PSObject.Properties){if($item.Name.StartsWith('com.unity.modules.')){Add-Package $item.Name}}
@{dependencies=$dependencies}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath "$sandbox/Packages/manifest.json" -Encoding utf8
Write-Output "Prepared $sandbox"
