<#
.SYNOPSIS
    Генерирует .env с паролями локальной инфраструктуры (postgres, pgadmin, redis).

.DESCRIPTION
    Значения берутся из .env.example (порядок и комментарии сохраняются),
    незаполненные ключи заполняются случайными значениями. Уже заданные
    значения не трогаются - скрипт идемпотентен.

    Хеши Redis пересчитываются на каждом запуске, поэтому REDIS_ACL_HASH
    всегда соответствует REDIS_PASSWORD. В контейнер уходит только хеш.

.PARAMETER Force
    Перегенерировать все пароли, даже если они уже заданы.

.PARAMETER RedisAdmin
    Дополнительно создать пользователя dev-admin с полными правами (+@all)
    для ручной отладки redis. По умолчанию выключен.

.PARAMETER SeedUserSecrets
    Дополнительно записать строки подключения в user-secrets проекта
    Fluxy.API (ConnectionStrings:Postgres / ConnectionStrings:Redis).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\init-secrets.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\init-secrets.ps1 -RedisAdmin -SeedUserSecrets
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$RedisAdmin,
    [switch]$SeedUserSecrets
)

$ErrorActionPreference = 'Stop'

$root       = Split-Path -Parent $PSScriptRoot
$envExample = Join-Path $root '.env.example'
$envFile    = Join-Path $root '.env'

if (-not (Test-Path $envExample)) {
    throw "Не найден $envExample - сгенерировать секреты нечего."
}

function New-RandomHex {
    param([int]$ByteCount = 32)
    $bytes = New-Object byte[] $ByteCount
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return (-join ($bytes | ForEach-Object { $_.ToString('x2') }))
}

function Get-Sha256Hex {
    param([string]$Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        return (-join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }))
    } finally { $sha.Dispose() }
}

function Get-EnvMap {
    param([string]$Path)
    $map = [ordered]@{}
    if (-not (Test-Path $Path)) { return $map }
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        $trimmed = $line.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
        $i = $trimmed.IndexOf('=')
        if ($i -lt 1) { continue }
        $map[$trimmed.Substring(0, $i).Trim()] = $trimmed.Substring($i + 1).Trim()
    }
    return $map
}

$values = Get-EnvMap $envExample
$existing = Get-EnvMap $envFile
foreach ($key in $existing.Keys) {
    if (-not $values.Contains($key)) {
        Write-Warning "В .env есть ключ '$key', которого нет в .env.example - он будет сохранён как есть."
    }
    $values[$key] = $existing[$key]
}

$generated = @()

function Set-Secret {
    param([string]$Key, [switch]$Always, [int]$ByteCount = 32)
    $current = [string]$values[$Key]
    if ([string]::IsNullOrWhiteSpace($current) -or $Always) {
        $values[$Key] = New-RandomHex -ByteCount $ByteCount
        $script:generated += $Key
        return $true
    }
    return $false
}

# Пароли: заполняются только если пусто, либо по -Force
$null = Set-Secret -Key 'POSTGRES_PASSWORD' -Always:$Force
$null = Set-Secret -Key 'PGADMIN_PASSWORD'  -Always:$Force
$null = Set-Secret -Key 'REDIS_PASSWORD'    -Always:$Force

# Ключ подписи access токенов. 64 байта (128 hex-символов) - вдвое больше
# минимума в 32 байта, требуемого HMAC-SHA256. Ротация (-Force) обрывает все
# выданные access токены, но не трогает refresh-цепочки в базе.
$null = Set-Secret -Key 'JWT_SIGNING_KEY' -Always:$Force -ByteCount 64

if ($RedisAdmin) {
    $null = Set-Secret -Key 'REDIS_ADMIN_PASSWORD' -Always:$Force
} elseif (-not [string]::IsNullOrWhiteSpace([string]$values['REDIS_ADMIN_PASSWORD'])) {
    Write-Warning 'REDIS_ADMIN_PASSWORD уже задан, а -RedisAdmin не передан. Используется существующий.'
}

# Хеши всегда соответствуют текущим паролям
$values['REDIS_ACL_HASH'] = Get-Sha256Hex -Text ([string]$values['REDIS_PASSWORD'])
if (-not [string]::IsNullOrWhiteSpace([string]$values['REDIS_ADMIN_PASSWORD'])) {
    $values['REDIS_ADMIN_ACL_HASH'] = Get-Sha256Hex -Text ([string]$values['REDIS_ADMIN_PASSWORD'])
} else {
    $values['REDIS_ADMIN_PASSWORD'] = ''
    $values['REDIS_ADMIN_ACL_HASH'] = ''
}

# Запись: комментарии и пустые строки берём из .env.example, значения - из $values
$out = New-Object System.Collections.Generic.List[string]
foreach ($line in [System.IO.File]::ReadAllLines($envExample)) {
    $trimmed = $line.Trim()
    if ($trimmed -eq '' -or $trimmed.StartsWith('#')) {
        $out.Add($line)
        continue
    }
    $i = $trimmed.IndexOf('=')
    if ($i -lt 1) { $out.Add($line); continue }
    $key = $trimmed.Substring(0, $i).Trim()
    if ($values.Contains($key)) {
        $out.Add("$key=$($values[$key])")
    } else {
        $out.Add($line)
    }
}

# Без BOM: compose неверно читает первый ключ, если файл начинается с BOM
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($envFile, (($out -join "`n") + "`n"), $utf8NoBom)

Write-Host "Записан $envFile"
if ($generated.Count -gt 0) {
    Write-Host ("Сгенерированы новые значения: " + ($generated -join ', '))
} else {
    Write-Host 'Все секреты уже были заданы, ничего не перегенерировано (см. -Force).'
}
if ([string]::IsNullOrWhiteSpace([string]$values['REDIS_ADMIN_ACL_HASH'])) {
    Write-Host 'Пользователь redis dev-admin выключен. Включить: scripts\init-secrets.ps1 -RedisAdmin'
}

if ($SeedUserSecrets) {
    $pg = 'Host=localhost;Port=5432;Database={0};Username={1};Password={2}' -f `
        $values['POSTGRES_DB'], $values['POSTGRES_USER'], $values['POSTGRES_PASSWORD']
    $redis = 'localhost:6379,user={0},password={1},defaultDatabase=0,abortConnect=false' -f `
        $values['REDIS_USER'], $values['REDIS_PASSWORD']

    Push-Location $root
    try {
        dotnet user-secrets --project Fluxy.API set 'ConnectionStrings:Postgres' $pg
        dotnet user-secrets --project Fluxy.API set 'ConnectionStrings:Redis' $redis
    } finally {
        Pop-Location
    }
    Write-Host 'Строки подключения записаны в user-secrets проекта Fluxy.API'
}

Write-Host ''
Write-Host 'Пароли намеренно не выводятся в консоль. Смотрите .env.'
