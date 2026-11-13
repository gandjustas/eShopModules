#requires -Version 7
<#
.SYNOPSIS
  Measures eShop resource consumption under the e2e (Playwright) test load.

.DESCRIPTION
  1. Starts the AppHost (aspire start) and waits until all services are Running.
  2. Finds the service processes and containers created by the AppHost.
  3. While the e2e tests run, collects:
       - per process: CPU time, working set, private bytes, threads, handles;
       - from the .NET runtime (dotnet-counters): allocations, GC collections, GC pauses, heap size;
       - per container (docker stats): CPU %, memory.
  4. Stops the AppHost and writes summary.md / summary.json + raw CSVs.

  The script can be run from the main checkout against any worktree via -RepoRoot.

.EXAMPLE
  ./scripts/measure-resources.ps1 -Label microservices
.EXAMPLE
  ./scripts/measure-resources.ps1 -RepoRoot D:\Projects\eShop -Label monolith -Iterations 3 -WarmupIterations 1
.EXAMPLE
  ./scripts/measure-resources.ps1 -Compare artifacts\measurements\microservices-..., artifacts\measurements\monolith-...
#>
[CmdletBinding(DefaultParameterSetName = 'Measure')]
param(
    # Root of the repository / worktree to measure.
    [Parameter(ParameterSetName = 'Measure')]
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),

    # Measurement label (defaults to the branch name).
    [Parameter(ParameterSetName = 'Measure')]
    [string]$Label,

    # Where to write results (defaults to <script root>\artifacts\measurements\<label>-<time>).
    [Parameter(ParameterSetName = 'Measure')]
    [string]$OutDir,

    # How many times to run e2e under measurement.
    [Parameter(ParameterSetName = 'Measure')]
    [ValidateRange(1, 100)]
    [int]$Iterations = 1,

    # Warm-up e2e runs (not included in the results).
    [Parameter(ParameterSetName = 'Measure')]
    [ValidateRange(0, 100)]
    [int]$WarmupIterations = 0,

    # Process sampling interval, seconds.
    [Parameter(ParameterSetName = 'Measure')]
    [ValidateRange(1, 60)]
    [int]$IntervalSec = 1,

    # How many seconds of idle metrics to collect before the load.
    [Parameter(ParameterSetName = 'Measure')]
    [int]$IdleSec = 10,

    # Start the AppHost and services with DOTNET_SYSTEM_NET_DISABLEIPV6=1.
    # Needed on machines where localhost resolves to ::1 but Docker publishes ports only on 127.0.0.1:
    # without it the AppHost DB health checks time out and some services fail to start.
    [Parameter(ParameterSetName = 'Measure')]
    [switch]$DisableIPv6,

    # By default the AppHost is started with ESHOP_USE_HTTP_ENDPOINTS=1 (as in CI): otherwise WebApp redirects to https
    # with a self-signed certificate, Playwright does not see the running app and tries to start a second one.
    [Parameter(ParameterSetName = 'Measure')]
    [switch]$UseHttps,

    [Parameter(ParameterSetName = 'Measure')]
    [string]$AppHostProject = 'src/eShop.AppHost/eShop.AppHost.csproj',

    # Address that must respond before the tests start (baseURL from playwright.config.ts).
    [Parameter(ParameterSetName = 'Measure')]
    [string]$BaseUrl = 'http://localhost:5045',

    [Parameter(ParameterSetName = 'Measure')]
    [int]$StartupTimeoutSec = 600,

    # dotnet-counters providers (comma-separated).
    [Parameter(ParameterSetName = 'Measure')]
    [string]$CounterProviders = 'System.Runtime',

    # Extra arguments for `npx playwright test`.
    [Parameter(ParameterSetName = 'Measure')]
    [string[]]$PlaywrightArgs = @(),

    # File with USERNAME1/PASSWORD for e2e, if RepoRoot has no .env of its own.
    [Parameter(ParameterSetName = 'Measure')]
    [string]$EnvFile,

    # Do not stop the AppHost after the measurement.
    [Parameter(ParameterSetName = 'Measure')]
    [switch]$KeepRunning,

    # Compare two measurements: -Compare <baseline>,<candidate> (folders containing summary.json).
    [Parameter(ParameterSetName = 'Compare', Mandatory)]
    [string[]]$Compare
)

$ErrorActionPreference = 'Stop'
[cultureinfo]::CurrentCulture = [cultureinfo]::InvariantCulture
$inv = [cultureinfo]::InvariantCulture

function Rnd([double]$value, [int]$digits = 1) { [Math]::Round($value, $digits) }

function ConvertTo-MdTable([object[]]$rows, [string[]]$columns) {
    if (-not $rows) { return '_no data_' }
    $lines = @('| ' + ($columns -join ' | ') + ' |')
    $lines += '|' + (($columns | ForEach-Object { '---' }) -join '|') + '|'
    foreach ($row in $rows) {
        $lines += '| ' + (($columns | ForEach-Object { "$($row.$_)" }) -join ' | ') + ' |'
    }
    $lines -join "`n"
}

# ----------------------------------------------------------------------------
# Compare mode
# ----------------------------------------------------------------------------
if ($PSCmdlet.ParameterSetName -eq 'Compare') {
    $Compare = @($Compare -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })   # pwsh -File passes "a,b" as a single string
    if ($Compare.Count -ne 2) { throw '-Compare expects two folders: <baseline>,<candidate>.' }
    $a, $b = $Compare | ForEach-Object {
        $file = Join-Path $_ 'summary.json'
        if (-not (Test-Path $file)) { throw "Not found: $file" }
        Get-Content $file -Raw | ConvertFrom-Json
    }
    $metrics = [ordered]@{
        'App startup, s'                    = { param($s) $s.startupSec }
        'e2e, s (average per run)'          = { param($s) $s.e2eAvgSec }
        'Service processes'                 = { param($s) $s.totals.processCount }
        'Services CPU time, s'              = { param($s) $s.totals.cpuSec }
        'Services CPU, % of core (average)' = { param($s) $s.totals.cpuAvgPct }
        'Idle working set, MB'              = { param($s) $s.totals.idleWsMb }
        'Working set, MB (average)'         = { param($s) $s.totals.wsAvgMb }
        'Working set, MB (sum peak)'        = { param($s) $s.totals.wsPeakMb }
        'Private bytes, MB (average)'       = { param($s) $s.totals.privAvgMb }
        'Private bytes, MB (sum peak)'      = { param($s) $s.totals.privPeakMb }
        'Threads (average)'                 = { param($s) $s.totals.threadsAvg }
        'Allocations, MB'                   = { param($s) $s.totals.allocMb }
        'GC gen0 / gen1 / gen2'             = { param($s) $s.totals.gcCount }
        'GC pauses, ms'                     = { param($s) $s.totals.gcPauseMs }
        'GC heap, MB (sum of peaks)'        = { param($s) $s.totals.gcHeapPeakMb }
        'Containers: CPU, % of core (average)' = { param($s) $s.containerTotals.cpuAvgPct }
        'Containers: memory, MB (average)'  = { param($s) $s.containerTotals.memAvgMb }
        'Containers: memory, MB (sum peak)' = { param($s) $s.containerTotals.memPeakMb }
    }
    $rows = foreach ($name in $metrics.Keys) {
        $va = & $metrics[$name] $a
        $vb = & $metrics[$name] $b
        $delta = ''
        if ($va -is [ValueType] -and $vb -is [ValueType] -and [double]$va -ne 0) {
            $delta = '{0:+0.0;-0.0;0.0}%' -f (([double]$vb - [double]$va) / [double]$va * 100)
        }
        [pscustomobject]([ordered]@{ 'Metric' = $name; "A: $($a.label)" = $va; "B: $($b.label)" = $vb; 'Δ' = $delta })
    }
    "Baseline : $($a.label)  ($($a.branch) @ $($a.commit), $($a.configuration), e2e x$($a.iterations.Count))"
    "Candidate: $($b.label)  ($($b.branch) @ $($b.commit), $($b.configuration), e2e x$($b.iterations.Count))"
    if ($a.configuration -ne $b.configuration) { Write-Warning 'The measurements were taken with different build configurations — the comparison is not valid.' }
    $rows | Format-Table -AutoSize
    return
}

# ----------------------------------------------------------------------------
# Preparation
# ----------------------------------------------------------------------------
$RepoRoot = (Resolve-Path $RepoRoot).Path.TrimEnd('\')
$appHost = Join-Path $RepoRoot $AppHostProject
if (-not (Test-Path $appHost)) { throw "AppHost not found: $appHost" }

foreach ($tool in 'aspire', 'dotnet-counters', 'docker', 'npx', 'git') {
    if (-not (Get-Command $tool -ErrorAction Ignore)) { throw "Tool '$tool' not found in PATH." }
}
docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker is not running.' }

$port = ([uri]$BaseUrl).Port
if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction Ignore) {
    throw "Port $port is already in use: stop the running app (aspire stop --all) — a clean start is needed for comparable numbers."
}

# e2e credentials: environment -> .env in RepoRoot (read by Playwright itself) -> -EnvFile / .env next to the script.
if (-not ($env:USERNAME1 -and $env:PASSWORD) -and -not (Test-Path (Join-Path $RepoRoot '.env'))) {
    if (-not $EnvFile) { $EnvFile = Join-Path (Split-Path -Parent $PSScriptRoot) '.env' }
    if (-not (Test-Path $EnvFile)) {
        throw "e2e needs USERNAME1 and PASSWORD: set the environment variables or create a .env in $RepoRoot."
    }
    foreach ($line in Get-Content $EnvFile) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$') {
            [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2].Trim().Trim('"', "'"), 'Process')
        }
    }
}

$branch = (git -C $RepoRoot rev-parse --abbrev-ref HEAD).Trim()
$commit = (git -C $RepoRoot rev-parse --short HEAD).Trim()
$dirty = @(git -C $RepoRoot status --porcelain).Count
if (-not $Label) { $Label = ($branch -replace '[^\w.-]+', '-') }
if (-not $OutDir) {
    $OutDir = Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts\measurements\$Label-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
}
$null = New-Item -ItemType Directory -Force -Path $OutDir, (Join-Path $OutDir 'counters')
$OutDir = (Resolve-Path $OutDir).Path

function Log([string]$message) { Write-Host ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $message) }

function Get-AspireResources {
    $json = aspire describe --apphost $appHost --format Json --nologo --non-interactive 2>$null | Out-String
    if (-not $json.Trim()) { return @() }
    try { @(($json | ConvertFrom-Json).resources) } catch { @() }
}

function Get-ServiceProcesses {
    $root = $RepoRoot + '\'
    Get-CimInstance Win32_Process | Where-Object {
        $_.ExecutablePath -and
        $_.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -and
        $_.ExecutablePath.Substring($root.Length) -notmatch '^(\.claude|node_modules)\\' -and
        $_.Name -notmatch 'AppHost'
    }
}

$samples = [System.Collections.Generic.List[object]]::new()
$tick = 0
function Add-Sample([string]$phase) {
    $script:tick++
    $now = Get-Date
    foreach ($svc in $services) {
        try {
            $p = [Diagnostics.Process]::GetProcessById($svc.pid)
            $samples.Add([pscustomobject]@{
                    ts         = $now.ToString('o')
                    tick       = $script:tick
                    phase      = $phase
                    service    = $svc.name
                    pid        = $svc.pid
                    cpu_s      = Rnd $p.TotalProcessorTime.TotalSeconds 3
                    ws_mb      = Rnd ($p.WorkingSet64 / 1MB) 2
                    private_mb = Rnd ($p.PrivateMemorySize64 / 1MB) 2
                    threads    = $p.Threads.Count
                    handles    = $p.HandleCount
                })
            $p.Dispose()
        }
        catch {
            if (-not $svc.exited) { $svc.exited = $true; Write-Warning "Process $($svc.name) ($($svc.pid)) exited during the measurement." }
        }
    }
}

$windows = [System.Collections.Generic.List[object]]::new()
$counterProcs = @()
$dockerJob = $null
$dockerStop = Join-Path $OutDir '.docker-stop'
$started = $false
$prevDisableIPv6 = $env:DOTNET_SYSTEM_NET_DISABLEIPV6
$prevHttpEndpoints = $env:ESHOP_USE_HTTP_ENDPOINTS

try {
    # ------------------------------------------------------------------------
    # Start the AppHost
    # ------------------------------------------------------------------------
    if ($DisableIPv6) { $env:DOTNET_SYSTEM_NET_DISABLEIPV6 = '1' }
    if (-not $UseHttps) { $env:ESHOP_USE_HTTP_ENDPOINTS = '1' }
    Log "Starting AppHost: $appHost"
    $startup = [Diagnostics.Stopwatch]::StartNew()
    aspire start --apphost $appHost --non-interactive --nologo *> (Join-Path $OutDir 'aspire-start.log')
    if ($LASTEXITCODE -ne 0) { throw "aspire start exited with code $LASTEXITCODE, see $OutDir\aspire-start.log" }
    $started = $true

    Log "Waiting for services to become ready (up to $StartupTimeoutSec s)…"
    $ready = $false
    while ($startup.Elapsed.TotalSeconds -lt $StartupTimeoutSec) {
        $projects = @(Get-AspireResources | Where-Object resourceType -eq 'Project')
        $pending = @($projects | Where-Object { $_.state -ne 'Running' -or ($_.healthStatus -and $_.healthStatus -ne 'Healthy') })
        if ($projects.Count -gt 0 -and $pending.Count -eq 0) {
            try {
                $response = Invoke-WebRequest $BaseUrl -TimeoutSec 5 -SkipHttpErrorCheck
                if ($response.StatusCode -lt 500) { $ready = $true; break }
            }
            catch { }
        }
        Start-Sleep -Seconds 2
    }
    $startup.Stop()
    if (-not $ready) {
        $state = ($pending | ForEach-Object { "$($_.name)=$($_.state)/$($_.healthStatus)" }) -join ', '
        throw "The app did not come up within $StartupTimeoutSec s. Not ready: $state. If a DB is Unhealthy (Failed to connect) in the dashboard, try -DisableIPv6."
    }
    $startupSec = Rnd $startup.Elapsed.TotalSeconds 1
    Log "Ready in $startupSec s (including build)."

    # ------------------------------------------------------------------------
    # What to measure
    # ------------------------------------------------------------------------
    $services = @(Get-ServiceProcesses | Sort-Object Name | ForEach-Object {
            [pscustomobject]@{
                name    = [IO.Path]::GetFileNameWithoutExtension($_.Name)
                pid     = [int]$_.ProcessId
                path    = $_.ExecutablePath
                exited  = $false
            }
        })
    if (-not $services) { throw "No service processes found under $RepoRoot." }
    foreach ($group in $services | Group-Object name | Where-Object Count -gt 1) {
        foreach ($svc in $group.Group) { $svc.name = "$($svc.name)#$($svc.pid)" }
    }
    $detectedConfig = (@($services.path | ForEach-Object { if ($_ -match '\\(debug|release)[\\_]') { $Matches[1].ToLower() } }) | Sort-Object -Unique) -join '+'
    Log "Services ($($services.Count), $detectedConfig): $($services.name -join ', ')"

    # Containers of this AppHost only: Aspire container resource names match the Docker container names.
    $resourceNames = @((Get-AspireResources).name)
    $containers = @(docker ps --format '{{.Names}}' | Where-Object { $_ -in $resourceNames })
    Log "Containers ($($containers.Count)): $($containers -join ', ')"

    foreach ($svc in $services) {
        $csv = Join-Path $OutDir "counters\$($svc.name).csv"
        $counterProcs += Start-Process dotnet-counters -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $OutDir "counters\$($svc.name).log") `
            -ArgumentList 'collect', '-p', $svc.pid, '--refresh-interval', $IntervalSec, '--format', 'csv', '-o', "`"$csv`"", '--counters', $CounterProviders
    }

    # dotnet-counters does not attach instantly — wait for the first data so the load is captured in full.
    $attachDeadline = (Get-Date).AddSeconds(90)
    do {
        Start-Sleep -Seconds 1
        $waiting = @($services | Where-Object { $f = Join-Path $OutDir "counters\$($_.name).csv"; -not (Test-Path $f) -or (Get-Item $f).Length -lt 200 })
    } while ($waiting -and (Get-Date) -lt $attachDeadline)
    if ($waiting) { Write-Warning "dotnet-counters did not attach to: $($waiting.name -join ', ') — GC metrics for them will be empty." }

    if ($containers) {
        $dockerJob = Start-ThreadJob -ArgumentList $containers, (Join-Path $OutDir 'containers.raw'), $dockerStop -ScriptBlock {
            param($names, $file, $stopFile)
            while (-not (Test-Path $stopFile)) {
                $ts = (Get-Date).ToString('o')
                $lines = docker stats --no-stream --format '{{.Name}}|{{.CPUPerc}}|{{.MemUsage}}' @names 2>$null
                if ($lines) { $lines | ForEach-Object { "$ts|$_" } | Add-Content -Path $file }
                else { Start-Sleep -Seconds 1 }
            }
        }
    }

    # ------------------------------------------------------------------------
    # Idle
    # ------------------------------------------------------------------------
    if ($IdleSec -gt 0) {
        Log "Measuring idle: $IdleSec s"
        $idleStart = Get-Date
        while (((Get-Date) - $idleStart).TotalSeconds -lt $IdleSec) { Add-Sample 'idle'; Start-Sleep -Seconds $IntervalSec }
        $windows.Add([pscustomobject]@{ phase = 'idle'; measured = $false; start = $idleStart; end = Get-Date })
    }

    # ------------------------------------------------------------------------
    # Load: e2e
    # ------------------------------------------------------------------------
    $phases = @(1..($WarmupIterations + $Iterations) | ForEach-Object {
            if ($_ -le $WarmupIterations) { "warmup$_" } else { "run$($_ - $WarmupIterations)" }
        })
    foreach ($phase in $phases) {
        Log "e2e: $phase"
        $report = Join-Path $OutDir "playwright-$phase.json"
        $env:PLAYWRIGHT_JSON_OUTPUT_NAME = $report
        $phaseStart = Get-Date
        Add-Sample $phase
        $pw = Start-Process cmd.exe -WorkingDirectory $RepoRoot -NoNewWindow -PassThru `
            -ArgumentList (@('/c', 'npx', 'playwright', 'test', '--reporter=list,json') + $PlaywrightArgs) `
            -RedirectStandardOutput (Join-Path $OutDir "playwright-$phase.log") `
            -RedirectStandardError (Join-Path $OutDir "playwright-$phase.err.log")
        $null = $pw.Handle
        while (-not $pw.WaitForExit($IntervalSec * 1000)) { Add-Sample $phase }
        Add-Sample $phase
        $phaseEnd = Get-Date

        $stats = $null
        if (Test-Path $report) { try { $stats = (Get-Content $report -Raw | ConvertFrom-Json).stats } catch { } }
        $window = [pscustomobject]@{
            phase       = $phase
            measured    = $phase -like 'run*'
            start       = $phaseStart
            end         = $phaseEnd
            durationSec = Rnd ($phaseEnd - $phaseStart).TotalSeconds 1
            exitCode    = $pw.ExitCode
            passed      = $stats.expected
            failed      = $stats.unexpected
            flaky       = $stats.flaky
            skipped     = $stats.skipped
        }
        $windows.Add($window)
        Log ("e2e: {0} — {1} s, passed={2} failed={3} (exit {4})" -f $phase, $window.durationSec, $window.passed, $window.failed, $window.exitCode)
        if ($pw.ExitCode -ne 0) { Write-Warning "e2e ($phase) failed — see $OutDir\playwright-$phase.log. Numbers from this run may not be comparable." }
    }
}
finally {
    $env:DOTNET_SYSTEM_NET_DISABLEIPV6 = $prevDisableIPv6
    $env:ESHOP_USE_HTTP_ENDPOINTS = $prevHttpEndpoints
    $env:PLAYWRIGHT_JSON_OUTPUT_NAME = $null
    if ($dockerJob) {
        $null = New-Item -ItemType File -Force -Path $dockerStop
        $null = $dockerJob | Wait-Job -Timeout 15
        $dockerJob | Remove-Job -Force
        Remove-Item $dockerStop -ErrorAction Ignore
    }
    if ($started -and -not $KeepRunning) {
        Log 'Stopping AppHost…'
        aspire stop --apphost $appHost --non-interactive --nologo *> (Join-Path $OutDir 'aspire-stop.log')
    }
    # dotnet-counters exits on its own and flushes the CSV when the target process exits.
    if ($counterProcs) {
        if ($KeepRunning) { Start-Sleep -Seconds 2 }
        else { $null = $counterProcs | Wait-Process -Timeout 30 -ErrorAction Ignore }
        $counterProcs | Where-Object { -not $_.HasExited } | Stop-Process -Force -ErrorAction Ignore
    }
}

# ----------------------------------------------------------------------------
# Results
# ----------------------------------------------------------------------------
$samples | Export-Csv (Join-Path $OutDir 'processes.csv') -NoTypeInformation -Encoding utf8

$measured = @($windows | Where-Object measured)
$wallSec = ($measured | Measure-Object durationSec -Sum).Sum
function Test-InMeasuredWindow([datetime]$time) {
    foreach ($w in $measured) { if ($time -ge $w.start -and $time -le $w.end.AddSeconds(1)) { return $true } }
    $false
}

# --- dotnet-counters ---------------------------------------------------------
function Get-CounterSummary([string]$file) {
    $empty = @{ allocMb = $null; gen0 = $null; gen1 = $null; gen2 = $null; gcPauseMs = $null; gcHeapPeakMb = $null }
    if (-not (Test-Path $file)) { return $empty }
    $alloc = 0.0; $pause = 0.0; $gen = @{ gen0 = 0.0; gen1 = 0.0; gen2 = 0.0 }; $heapByTs = @{}; $any = $false
    foreach ($row in Import-Csv $file) {
        $time = [datetime]::ParseExact($row.Timestamp, 'MM/dd/yyyy HH:mm:ss', $inv)
        if (-not (Test-InMeasuredWindow $time)) { continue }
        $any = $true
        $name = $row.'Counter Name'
        $value = [double]::Parse($row.'Mean/Increment', $inv)
        if ($name.StartsWith('dotnet.gc.heap.total_allocated')) { $alloc += $value * $IntervalSec }
        elseif ($name.StartsWith('dotnet.gc.pause.time')) { $pause += $value * $IntervalSec }
        elseif ($name -match '^dotnet\.gc\.collections .*generation=(gen[012])\]') { $gen[$Matches[1]] += $value * $IntervalSec }
        elseif ($name.StartsWith('dotnet.gc.last_collection.heap.size')) { $heapByTs[$row.Timestamp] = [double]$heapByTs[$row.Timestamp] + $value }
    }
    if (-not $any) { return $empty }
    @{
        allocMb      = Rnd ($alloc / 1MB) 1
        gen0         = [int][Math]::Round($gen.gen0)
        gen1         = [int][Math]::Round($gen.gen1)
        gen2         = [int][Math]::Round($gen.gen2)
        gcPauseMs    = Rnd ($pause * 1000) 1
        gcHeapPeakMb = if ($heapByTs.Count) { Rnd ((($heapByTs.Values | Measure-Object -Maximum).Maximum) / 1MB) 1 } else { $null }
    }
}

# --- processes ---------------------------------------------------------------
$serviceRows = foreach ($svc in $services) {
    $all = @($samples | Where-Object service -eq $svc.name)
    $idle = @($all | Where-Object phase -eq 'idle')
    $load = @($all | Where-Object { $_.phase -like 'run*' })
    $cpuSec = 0.0; $cpuPeak = 0.0
    foreach ($group in $load | Group-Object phase) {
        $rows = $group.Group
        $cpuSec += $rows[-1].cpu_s - $rows[0].cpu_s
        for ($i = 1; $i -lt $rows.Count; $i++) {
            $dt = ([datetime]$rows[$i].ts - [datetime]$rows[$i - 1].ts).TotalSeconds
            if ($dt -gt 0) { $cpuPeak = [Math]::Max($cpuPeak, ($rows[$i].cpu_s - $rows[$i - 1].cpu_s) / $dt * 100) }
        }
    }
    $counters = Get-CounterSummary (Join-Path $OutDir "counters\$($svc.name).csv")
    [pscustomobject]@{
        service      = $svc.name
        idleWsMb     = if ($idle) { Rnd ($idle | Measure-Object ws_mb -Average).Average } else { $null }
        cpuSec       = Rnd $cpuSec 2
        cpuAvgPct    = if ($wallSec) { Rnd ($cpuSec / $wallSec * 100) } else { $null }
        cpuPeakPct   = Rnd $cpuPeak
        wsAvgMb      = Rnd ($load | Measure-Object ws_mb -Average).Average
        wsPeakMb     = Rnd ($load | Measure-Object ws_mb -Maximum).Maximum
        privAvgMb    = Rnd ($load | Measure-Object private_mb -Average).Average
        privPeakMb   = Rnd ($load | Measure-Object private_mb -Maximum).Maximum
        threadsAvg   = Rnd ($load | Measure-Object threads -Average).Average 0
        handlesPeak  = ($load | Measure-Object handles -Maximum).Maximum
        allocMb      = $counters.allocMb
        gen0         = $counters.gen0
        gen1         = $counters.gen1
        gen2         = $counters.gen2
        gcPauseMs    = $counters.gcPauseMs
        gcHeapPeakMb = $counters.gcHeapPeakMb
    }
}

$perTick = $samples | Where-Object { $_.phase -like 'run*' } | Group-Object tick | ForEach-Object {
    [pscustomobject]@{
        ws   = ($_.Group | Measure-Object ws_mb -Sum).Sum
        priv = ($_.Group | Measure-Object private_mb -Sum).Sum
    }
}
function Sum($rows, $property) { ($rows | Measure-Object $property -Sum).Sum }
$totals = [ordered]@{
    processCount = $services.Count
    cpuSec       = Rnd (Sum $serviceRows cpuSec) 2
    cpuAvgPct    = Rnd (Sum $serviceRows cpuAvgPct)
    idleWsMb     = Rnd (Sum $serviceRows idleWsMb)
    wsAvgMb      = Rnd (Sum $serviceRows wsAvgMb)
    wsPeakMb     = Rnd ($perTick | Measure-Object ws -Maximum).Maximum
    privAvgMb    = Rnd (Sum $serviceRows privAvgMb)
    privPeakMb   = Rnd ($perTick | Measure-Object priv -Maximum).Maximum
    threadsAvg   = Sum $serviceRows threadsAvg
    allocMb      = Rnd (Sum $serviceRows allocMb)
    gcCount      = '{0} / {1} / {2}' -f (Sum $serviceRows gen0), (Sum $serviceRows gen1), (Sum $serviceRows gen2)
    gcPauseMs    = Rnd (Sum $serviceRows gcPauseMs)
    gcHeapPeakMb = Rnd (Sum $serviceRows gcHeapPeakMb)
}

# --- containers --------------------------------------------------------------
function ConvertTo-Mb([string]$text) {
    if ($text -notmatch '^\s*([\d.]+)\s*([A-Za-z]+)') { return 0.0 }
    $value = [double]::Parse($Matches[1], $inv)
    switch -Regex ($Matches[2]) {
        '^Gi?B$' { $value * 1024; break }
        '^Mi?B$' { $value; break }
        '^Ki?B$' { $value / 1024; break }
        default { $value / 1MB }
    }
}
$containerSamples = @()
$rawFile = Join-Path $OutDir 'containers.raw'
if (Test-Path $rawFile) {
    $containerSamples = @(Get-Content $rawFile | ForEach-Object {
            $parts = $_ -split '\|'
            if ($parts.Count -ge 4) {
                [pscustomobject]@{
                    ts        = $parts[0]
                    container = $parts[1] -replace '-([0-9a-f]{8}-[0-9a-f-]{27}|[a-z0-9]{8})$', ''
                    cpu_pct   = [double]::Parse(($parts[2] -replace '%', ''), $inv)
                    mem_mb    = Rnd (ConvertTo-Mb (($parts[3] -split '/')[0])) 2
                    measured  = Test-InMeasuredWindow ([datetime]$parts[0])
                }
            }
        })
    $containerSamples | Export-Csv (Join-Path $OutDir 'containers.csv') -NoTypeInformation -Encoding utf8
    Remove-Item $rawFile
}
$containerLoad = @($containerSamples | Where-Object measured)
$containerRows = foreach ($group in $containerLoad | Group-Object container | Sort-Object Name) {
    [pscustomobject]@{
        container  = $group.Name
        cpuAvgPct  = Rnd ($group.Group | Measure-Object cpu_pct -Average).Average 2
        cpuPeakPct = Rnd ($group.Group | Measure-Object cpu_pct -Maximum).Maximum 2
        memAvgMb   = Rnd ($group.Group | Measure-Object mem_mb -Average).Average
        memPeakMb  = Rnd ($group.Group | Measure-Object mem_mb -Maximum).Maximum
    }
}
$containerTotals = [ordered]@{
    containerCount = @($containerRows).Count
    cpuAvgPct      = Rnd (Sum $containerRows cpuAvgPct) 2
    memAvgMb       = Rnd (Sum $containerRows memAvgMb)
    memPeakMb      = Rnd (($containerLoad | Group-Object ts | ForEach-Object { ($_.Group | Measure-Object mem_mb -Sum).Sum } | Measure-Object -Maximum).Maximum)
}

# --- output ------------------------------------------------------------------
$e2eAvgSec = if ($measured) { Rnd ($measured | Measure-Object durationSec -Average).Average } else { $null }
$summary = [ordered]@{
    label           = $Label
    repoRoot        = $RepoRoot
    branch          = $branch
    commit          = $commit
    uncommitted     = $dirty
    configuration   = $detectedConfig
    measuredAt      = (Get-Date).ToString('s')
    machine         = [ordered]@{ logicalCpus = [Environment]::ProcessorCount; memoryGb = Rnd ((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB) }
    ipv6Disabled    = [bool]$DisableIPv6
    intervalSec     = $IntervalSec
    startupSec      = $startupSec
    e2eAvgSec       = $e2eAvgSec
    iterations      = @($windows | Where-Object phase -ne 'idle' | Select-Object phase, measured, durationSec, exitCode, passed, failed, flaky, skipped)
    totals          = $totals
    services        = @($serviceRows)
    containerTotals = $containerTotals
    containers      = @($containerRows)
}
$summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $OutDir 'summary.json') -Encoding utf8

$totalsRows = $totals.GetEnumerator() | ForEach-Object { [pscustomobject]@{ metric = $_.Key; value = $_.Value } }
$md = @"
# Resource measurement: $Label

- Repository: ``$RepoRoot`` — $branch @ $commit$(if ($dirty) { " (+$dirty uncommitted files)" })
- Build configuration: **$detectedConfig**; machine: $([Environment]::ProcessorCount) logical CPUs
- App startup (including build): **$startupSec s**
- Load: e2e (Playwright) × $($measured.Count), average **$e2eAvgSec s** per run; sampling interval $IntervalSec s
- CPU % is a percentage of one core; memory in MB; allocations and GC are for the duration of the measured runs

## e2e runs

$(ConvertTo-MdTable ($summary.iterations) 'phase', 'measured', 'durationSec', 'exitCode', 'passed', 'failed', 'flaky', 'skipped')

## Services — totals

$(ConvertTo-MdTable $totalsRows 'metric', 'value')

## Services

$(ConvertTo-MdTable $serviceRows 'service', 'idleWsMb', 'cpuSec', 'cpuAvgPct', 'cpuPeakPct', 'wsAvgMb', 'wsPeakMb', 'privAvgMb', 'privPeakMb', 'threadsAvg', 'allocMb', 'gen0', 'gen1', 'gen2', 'gcPauseMs', 'gcHeapPeakMb')

## Containers

$(ConvertTo-MdTable $containerRows 'container', 'cpuAvgPct', 'cpuPeakPct', 'memAvgMb', 'memPeakMb')

Total: CPU $($containerTotals.cpuAvgPct) % of a core, average memory $($containerTotals.memAvgMb) MB, peak of the sum $($containerTotals.memPeakMb) MB.
"@
$md | Set-Content (Join-Path $OutDir 'summary.md') -Encoding utf8

''
$serviceRows | Format-Table service, idleWsMb, cpuSec, cpuAvgPct, wsAvgMb, wsPeakMb, privPeakMb, allocMb, gen0, gen1, gen2, gcPauseMs -AutoSize
$containerRows | Format-Table -AutoSize
[pscustomobject]$totals | Format-List
Log "Results: $OutDir"
if ($windows | Where-Object { $_.phase -ne 'idle' -and $_.exitCode -ne 0 }) { exit 1 }
