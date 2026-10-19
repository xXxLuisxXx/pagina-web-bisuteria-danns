# Revisa configuración JSON en los commits locales sin imprimir valores sensibles.
$ErrorActionPreference = 'Stop'
$repositoryRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) { throw 'Ejecuta este script dentro del repositorio.' }
$findings = [System.Collections.Generic.List[string]]::new()

function Test-ConfigNode($node, [string]$path, [string]$source) {
    if ($node -is [System.Collections.IDictionary]) {
        foreach ($key in $node.Keys) {
            $value = $node[$key]
            $next = "$path/$key"
            if ($key -match '(?i)password|passwd|secret|api.?key|access.?token' -and
                $value -is [string] -and -not [string]::IsNullOrWhiteSpace($value)) {
                $findings.Add("${source}:$next")
            } elseif ($next -match 'ConnectionStrings' -and $value -is [string] -and
                $value -match '(?i)(Password|Pwd)\s*=\s*[^;\s]+') {
                $findings.Add("${source}:$next")
            } else {
                Test-ConfigNode $value $next $source
            }
        }
    } elseif ($node -is [array]) {
        for ($index = 0; $index -lt $node.Count; $index++) {
            Test-ConfigNode $node[$index] "$path/$index" $source
        }
    }
}

Push-Location -LiteralPath $repositoryRoot
try {
    $commits = @(git rev-list --all)
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron enumerar los commits locales.' }
    $scanned = 0
    foreach ($revision in $commits) {
        $tree = @(git ls-tree -r --name-only $revision)
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer un árbol de Git.' }
        $paths = @($tree | Where-Object { $_ -match '(^|/)(appsettings[^/]*|secrets)\.json$' })
        foreach ($path in $paths) {
            $raw = (git show "${revision}:$path") -join "`n"
            if ($LASTEXITCODE -ne 0) { throw 'No se pudo leer un archivo histórico.' }
            try { $parsed = ConvertFrom-Json -InputObject $raw -AsHashtable }
            catch { throw "JSON no válido en $($revision.Substring(0,7))/$path" }
            Test-ConfigNode $parsed '' "$($revision.Substring(0,7))/$path"
            $scanned++
        }
    }
    [pscustomobject]@{
        Commits = $commits.Count
        HistoricalConfigFiles = $scanned
        SuspiciousPaths = $findings.Count
    }
    $findings | Sort-Object -Unique
    if ($findings.Count -gt 0) { exit 1 }
} finally {
    Pop-Location
}
