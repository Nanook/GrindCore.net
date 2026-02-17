<#
Runs dotnet tests with monitoring and optional diagnostics collection.

Usage examples:
  .\tools\run-tests-monitor.ps1 -Filter "CompressionBlockTests" -Configuration Release -RID win-x64 -CollectCounters -CollectTrace -CollectGCDump

Parameters:
  -Configuration  Build configuration (Default: Release)
  -RID            Runtime identifier for build/test (Default: win-x64)
  -Filter         Test filter string for `dotnet test --filter` (Default: FullyQualifiedName~CompressionBlockTests)
  -OutputDir      Directory to place artifacts (Default: .\artifacts)
  -CollectCounters  Switch: run `dotnet-counters` (if available)
  -CollectTrace     Switch: run `dotnet-trace` while tests run (if available)
  -CollectGCDump    Switch: collect a single `dotnet-gcdump` after Delay seconds (if available)
  -DumpDelaySec     Delay before capturing gcdump (Default: 5)
  -RefreshInterval  Sample interval seconds for memory logging (Default: 1)
  -NoBuild          Skip `dotnet build` step (Default: $false)
#>

param(
    [string]$Configuration = 'Release',
    [string]$RID = 'win-x64',
    [string]$Filter = 'CompressionBlockTests',
    [string]$OutputDir = "artifacts",
    [switch]$CollectCounters,
    [switch]$CollectTrace,
    [switch]$CollectGCDump,
    [int]$DumpDelaySec = 5,
    [int]$RefreshInterval = 1,
    [switch]$NoBuild
)

if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Path $OutputDir | Out-Null }
$timestamp = (Get-Date).ToString('yyyyMMdd_HHmmss')
$trxFile = Join-Path $OutputDir "tests_$timestamp.trx"
$memLog = Join-Path $OutputDir "mem_$timestamp.csv"
$countersOut = Join-Path $OutputDir "counters_$timestamp.txt"
$countersErr = Join-Path $OutputDir "counters_err_$timestamp.txt"
$gcdumpOut = Join-Path $OutputDir "gcdump_$timestamp.gcdump"
$traceOut = Join-Path $OutputDir "trace_$timestamp.nettrace"
$traceErr = Join-Path $OutputDir "trace_err_$timestamp.txt"
$testOut = Join-Path $OutputDir "test_stdout_$timestamp.txt"
$testErr = Join-Path $OutputDir "test_stderr_$timestamp.txt"
$dotnetInfo = Join-Path $OutputDir "dotnet_info_$timestamp.txt"
$envOut = Join-Path $OutputDir "env_$timestamp.txt"

Write-Host "Output dir: $OutputDir"
Write-Host "Build configuration: $Configuration | RID: $RID"

if (-not $NoBuild) {
    Write-Host "Building solution (no RID)..."
    # Don't specify a RuntimeIdentifier when building the entire solution — that is only valid at project level
    dotnet build -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
}

# Capture dotnet and environment information useful for diagnosing runtime/platform issues
try { dotnet --info | Out-File -FilePath $dotnetInfo -Encoding utf8 } catch { }
Get-ChildItem Env: | Out-File -FilePath $envOut -Encoding utf8

# Start the tests (no-build because we already built)
# Build the argument list as an array to avoid PowerShell parsing issues with the semicolon in the logger spec
$testArgs = @('test', '-c', $Configuration, '-r', $RID, '--no-build', '--logger', "trx;LogFileName=$trxFile", '--filter', "FullyQualifiedName~$Filter")
Write-Host ("Starting tests: dotnet " + ($testArgs -join ' '))
Write-Host "Writing test stdout -> $testOut, stderr -> $testErr"
$testProc = Start-Process -FilePath dotnet -ArgumentList $testArgs -RedirectStandardOutput $testOut -RedirectStandardError $testErr -NoNewWindow -PassThru
Start-Sleep -Milliseconds 500

if (-not $testProc -or $testProc.Id -eq $null) { throw "Failed to start test process" }
$testPid = $testProc.Id
Write-Host "Test host PID: $testPid"

# Start optional collectors
$counterProc = $null
$traceProc = $null
$gcdumpTaken = $false

if ($CollectCounters) {
    if (Get-Command dotnet-counters -ErrorAction SilentlyContinue) {
        Write-Host "Starting dotnet-counters (System.Runtime) -> $countersOut"
        $counterArgs = @('monitor', '-p', $testPid, 'System.Runtime', '--refresh-interval', $RefreshInterval)
        $counterProc = Start-Process -FilePath dotnet-counters -ArgumentList $counterArgs -RedirectStandardOutput $countersOut -RedirectStandardError $countersErr -NoNewWindow -PassThru
    } else { Write-Warning "dotnet-counters not found in PATH. Skipping." }
}

if ($CollectTrace) {
    if (Get-Command dotnet-trace -ErrorAction SilentlyContinue) {
        Write-Host "Starting dotnet-trace -> $traceOut"
        # dotnet-trace runs until killed; start it and keep reference
        $traceArgs = @('collect', '-p', $testPid, '-o', $traceOut)
        $traceProc = Start-Process -FilePath dotnet-trace -ArgumentList $traceArgs -RedirectStandardError $traceErr -NoNewWindow -PassThru
    } else { Write-Warning "dotnet-trace not found in PATH. Skipping." }
}

# Memory logging header
"Time,WorkingSet,PrivateBytes" | Out-File $memLog -Encoding utf8

Write-Host "Sampling memory and waiting for test process to exit..."

try {
    while (-not $testProc.HasExited) {
        try {
            $p = Get-Process -Id $testPid -ErrorAction Stop
            $ws = $p.WorkingSet64
            $pb = $p.PrivateMemorySize64
            "$((Get-Date).ToString('o')),$ws,$pb" | Out-File -Append $memLog -Encoding utf8
        } catch {
            # process may have exited since check
        }

        # Attempt gcdump after delay if requested and not yet taken
        if ($CollectGCDump -and -not $gcdumpTaken -and (Get-Command dotnet-gcdump -ErrorAction SilentlyContinue)) {
            $uptime = (Get-Date) - $testProc.StartTime
            if ($uptime.TotalSeconds -ge $DumpDelaySec) {
                Write-Host "Collecting gcdump -> $gcdumpOut"
                & dotnet-gcdump collect -p $testPid -o $gcdumpOut
                if ($LASTEXITCODE -eq 0) { $gcdumpTaken = $true } else { Write-Warning "gcdump collection failed" }
            }
        }

        Start-Sleep -Seconds $RefreshInterval
    }
} finally {
    Write-Host "Test process exited. Exit code: $($testProc.ExitCode)"

    # Stop trace and counters
    if ($traceProc -and -not $traceProc.HasExited) {
        try { $traceProc.Kill(); Write-Host "Stopped dotnet-trace" } catch { }
    }
    if ($counterProc -and -not $counterProc.HasExited) {
        try { $counterProc.Kill(); Write-Host "Stopped dotnet-counters" } catch { }
    }

    # If gcdump requested but not yet taken, attempt now (only works while process alive)
            if ($CollectGCDump -and -not $gcdumpTaken) {
        if (Get-Command dotnet-gcdump -ErrorAction SilentlyContinue) {
            Write-Host "Attempting gcdump post-exit -> $gcdumpOut"
            & dotnet-gcdump collect -p $testPid -o $gcdumpOut
            if ($LASTEXITCODE -eq 0) { $gcdumpTaken = $true } else { Write-Warning "gcdump post-exit failed" }
        }
    }
}

Write-Host "Summary"
Write-Host "  TRX: $trxFile"
Write-Host "  Mem log: $memLog"
if ($CollectCounters) { Write-Host "  Counters: $countersOut" }
if ($CollectGCDump) { Write-Host "  GCDump: $gcdumpOut (exists: $(Test-Path $gcdumpOut))" }
if ($CollectTrace) { Write-Host "  Trace: $traceOut (exists: $(Test-Path $traceOut))" }

Write-Host "Done."
