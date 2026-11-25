[CmdletBinding()]
param([switch]$RefreshEditor)

$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    if ($RefreshEditor) {
        $cli = Get-Command unity.exe -ErrorAction SilentlyContinue
        if (!$cli) { throw 'Unity CLI is required for -RefreshEditor.' }
        $cliArgs = @('command', '--project-path', $PSScriptRoot, '--timeout', '8', '--result-only', '--json')
        & $cli.Source @cliArgs editor_status 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            & $cli.Source @cliArgs recompile
            if ($LASTEXITCODE -ne 0) { throw 'Unity could not start the compilation request.' }
            $deadline = [DateTime]::UtcNow.AddMinutes(3)
            do {
                Start-Sleep -Seconds 1
                $reply = & $cli.Source @cliArgs recompile_status 2>$null
                if ($LASTEXITCODE -ne 0) { continue } # A domain reload can briefly disconnect the CLI.
                $status = ($reply -join "`n") | ConvertFrom-Json
                if ($status -is [string]) { $status = $status | ConvertFrom-Json }
                if ($status.failed) { throw ("Unity compilation failed. " + ($status.errors -join "`n")) }
                if ($status.status -in 'completed','up_to_date') {
                    Write-Output "Unity Editor compilation: $($status.status)"
                    return
                }
            } while ([DateTime]::UtcNow -lt $deadline)
            throw 'Timed out waiting for Unity to report compilation results.'
        }
        throw 'Could not connect to the Unity Editor to refresh the project.'
    }

    Write-Output 'Checking C# assemblies using the last Unity import, without requesting an Editor refresh.'
    $versionLine = Get-Content 'ProjectSettings/ProjectVersion.txt' | Select-String '^m_EditorVersion: (.+)$'
    $version = $versionLine.Matches[0].Groups[1].Value.Trim()
    $data = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Data"
    $dotnet = Join-Path $data 'NetCoreRuntime/dotnet.exe'
    $compiler = Join-Path $data 'DotNetSdkRoslyn/csc.dll'
    $editorRsp = Get-ChildItem 'Library/Bee/artifacts' -Recurse -Filter 'Assembly-CSharp-Editor.rsp' |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (!$editorRsp) { throw 'Open this project in Unity once to generate the compiler inputs.' }

    $scratch = New-Item -ItemType Directory -Path (Join-Path ([IO.Path]::GetTempPath()) ("unity-compile-" + [guid]::NewGuid()))
    try {
        $out = $scratch.FullName.Replace('\','/')
        foreach ($name in 'Assembly-CSharp','Assembly-CSharp-Editor') {
            $response = [IO.File]::ReadAllText((Join-Path $editorRsp.DirectoryName "$name.rsp"))
            $response = $response -replace '(?m)^-out:"[^"]+"', "-out:`"$out/$name.dll`""
            $response = $response -replace '(?m)^-refout:"[^"]+"', "-refout:`"$out/$name.ref.dll`""
            $response = $response -replace '(?m)^-r:"[^"]*/Assembly-CSharp.ref.dll"', "-r:`"$out/Assembly-CSharp.ref.dll`""
            [IO.File]::WriteAllText("$out/$name.rsp", $response)
            $messages = & $dotnet $compiler "@$out/$name.rsp"
            if ($LASTEXITCODE -ne 0) { throw ("$name failed.`n" + ($messages -join "`n")) }
            Write-Output "$name compiled."
        }
    } finally {
        Get-ChildItem -LiteralPath $scratch.FullName -File | Remove-Item -Force
        Remove-Item -LiteralPath $scratch.FullName
    }
} finally {
    Pop-Location
}
