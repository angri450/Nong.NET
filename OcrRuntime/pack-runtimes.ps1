param(
    [string]$Configuration = "Release",
    [string]$Output = "../nupkg"
)

$ErrorActionPreference = "Stop"
$Project = Join-Path $PSScriptRoot "OcrRuntime.csproj"
$OutputPath = if ([System.IO.Path]::IsPathRooted($Output)) {
    $Output
} else {
    Join-Path $PSScriptRoot $Output
}
[xml]$ProjectXml = Get-Content -LiteralPath $Project
$PackageVersion = $ProjectXml.Project.PropertyGroup.Version | Select-Object -First 1
$Packages = @(
    @{
        PackageId = "Angri450.Nong.OcrRuntime.WinX64"
        RuntimeIdentifier = "win-x64"
        PaddlePackage = "Sdcb.PaddleInference.runtime.win64.mkl"
        PaddleVersion = "3.3.1.70"
        PaddlePrefix = "runtimes/win-x64/native"
        OpenCvPackage = "OpenCvSharp4.runtime.win"
        OpenCvVersion = "4.11.0.20250507"
        OpenCvPrefix = "runtimes/win-x64/native"
        RequiredFiles = @("paddle_inference_c.dll", "OpenCvSharpExtern.dll", "mklml.dll", "mkldnn.dll")
    },
    @{
        PackageId = "Angri450.Nong.OcrRuntime.LinuxX64"
        RuntimeIdentifier = "linux-x64"
        PaddlePackage = "Sdcb.PaddleInference.runtime.linux-x64.openblas"
        PaddleVersion = "3.3.1.70"
        PaddlePrefix = "runtimes/linux-x64/native"
        OpenCvPackage = "OpenCvSharp4.runtime.ubuntu.18.04-x64"
        OpenCvVersion = "4.6.0.20220608"
        OpenCvPrefix = "runtimes/ubuntu.18.04-x64/native"
        RequiredFiles = @("libpaddle_inference_c.so", "libOpenCvSharpExtern.so")
    },
    @{
        PackageId = "Angri450.Nong.OcrRuntime.LinuxArm64"
        RuntimeIdentifier = "linux-arm64"
        PaddlePackage = "Sdcb.PaddleInference.runtime.linux-arm64"
        PaddleVersion = "3.3.1.70"
        PaddlePrefix = "runtimes/linux-arm64/native"
        OpenCvPackage = "OpenCvSharp4.runtime.linux-arm64"
        OpenCvVersion = "4.13.0.20260602"
        OpenCvPrefix = "runtimes/linux-arm64/native"
        RequiredFiles = @("libpaddle_inference_c.so", "libOpenCvSharpExtern.so")
    },
    @{
        PackageId = "Angri450.Nong.OcrRuntime.OsxX64"
        RuntimeIdentifier = "osx-x64"
        PaddlePackage = "Sdcb.PaddleInference.runtime.osx-x64"
        PaddleVersion = "3.3.1.70"
        PaddlePrefix = "runtimes/osx-x64/native"
        OpenCvPackage = "OpenCvSharp4.runtime.osx.10.15-universal"
        OpenCvVersion = "4.7.0.20230224"
        OpenCvPrefix = "runtimes/osx-x64/native"
        RequiredFiles = @("libpaddle_inference_c.dylib", "libOpenCvSharpExtern.dylib")
    },
    @{
        PackageId = "Angri450.Nong.OcrRuntime.OsxArm64"
        RuntimeIdentifier = "osx-arm64"
        PaddlePackage = "Sdcb.PaddleInference.runtime.osx-arm64"
        PaddleVersion = "3.3.1.70"
        PaddlePrefix = "runtimes/osx-arm64/native"
        OpenCvPackage = "OpenCvSharp4.runtime.osx.10.15-universal"
        OpenCvVersion = "4.7.0.20230224"
        OpenCvPrefix = "runtimes/osx-arm64/native"
        RequiredFiles = @("libpaddle_inference_c.dylib", "libOpenCvSharpExtern.dylib")
    }
)

function Test-NongOcrRuntimePackage {
    param(
        [Parameter(Mandatory = $true)][string]$PackagePath,
        [Parameter(Mandatory = $true)]$Package
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $nativePrefix = "runtimes/$($Package.RuntimeIdentifier)/native/"
        $nativeEntries = @($archive.Entries | Where-Object {
            $_.FullName.Replace('\', '/').StartsWith($nativePrefix, [System.StringComparison]::OrdinalIgnoreCase)
        })
        if ($nativeEntries.Count -eq 0) {
            throw "No native runtime entries found under $nativePrefix"
        }

        $allRuntimeEntries = @($archive.Entries | Where-Object {
            $_.FullName.Replace('\', '/').StartsWith("runtimes/", [System.StringComparison]::OrdinalIgnoreCase)
        })
        $runtimeIds = @($allRuntimeEntries | ForEach-Object {
            $parts = $_.FullName.Replace('\', '/').Split('/')
            if ($parts.Length -ge 2) { $parts[1] }
        } | Sort-Object -Unique)
        if ($runtimeIds.Count -ne 1 -or $runtimeIds[0] -ne $Package.RuntimeIdentifier) {
            throw "Expected only RID $($Package.RuntimeIdentifier), found: $($runtimeIds -join ', ')"
        }

        $names = @($nativeEntries | ForEach-Object { [System.IO.Path]::GetFileName($_.FullName) })
        foreach ($required in $Package.RequiredFiles) {
            if ($names -notcontains $required) {
                throw "Required native file missing from $($Package.PackageId): $required"
            }
        }

        $forbidden = @(".exe", ".pdb", ".py", ".whl")
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('\', '/')
            $leaf = [System.IO.Path]::GetFileName($name)
            if ($name -match '(?i)(^|/)python($|/)' -or $name -match '(?i)(^|/)models($|/)') {
                throw "Forbidden path in runtime package: $name"
            }
            foreach ($ext in $forbidden) {
                if ($leaf.EndsWith($ext, [System.StringComparison]::OrdinalIgnoreCase)) {
                    throw "Forbidden file in runtime package: $name"
                }
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $sizeMb = [Math]::Round((Get-Item -LiteralPath $PackagePath).Length / 1MB, 2)
    Write-Host "Validated $($Package.PackageId) $($Package.RuntimeIdentifier): $sizeMb MB"
}

foreach ($package in $Packages) {
    dotnet pack $Project `
        -c $Configuration `
        -o $OutputPath `
        --nologo `
        /p:PackageId=$($package.PackageId) `
        /p:RuntimeIdentifier=$($package.RuntimeIdentifier) `
        /p:UpstreamPaddlePackage=$($package.PaddlePackage) `
        /p:UpstreamPaddleVersion=$($package.PaddleVersion) `
        /p:UpstreamPaddleNativePrefix=$($package.PaddlePrefix) `
        /p:UpstreamOpenCvPackage=$($package.OpenCvPackage) `
        /p:UpstreamOpenCvVersion=$($package.OpenCvVersion) `
        /p:UpstreamOpenCvNativePrefix=$($package.OpenCvPrefix)

    $packagePath = Join-Path (Resolve-Path $OutputPath).Path "$($package.PackageId).$PackageVersion.nupkg"
    Test-NongOcrRuntimePackage -PackagePath $packagePath -Package $package
}
