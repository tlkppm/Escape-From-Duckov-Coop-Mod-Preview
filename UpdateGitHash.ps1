param([string]$buildInfoPath)

try {
    $hash = (git log -1 --format="%h" 2>$null)
    if ([string]::IsNullOrEmpty($hash)) {
        $hash = 'dev'
    }

    $content = Get-Content $buildInfoPath -Raw
    $newContent = $content -replace '(?<=internal const string GitCommit = ").*?(?=";)', $hash

    if ($content -ne $newContent) {
        Set-Content $buildInfoPath $newContent -NoNewline
        Write-Host "Updated GitCommit to: $hash"
    }
} catch {
    Write-Host "Git hash update skipped"
}

