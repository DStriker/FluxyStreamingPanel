<#
.SYNOPSIS
    Takes a database backup by asking the running API to create one.

.DESCRIPTION
    A thin caller of `POST /admin/backups`. It holds no naming rule and no rotation logic:
    the archive's name, its exclusion list and the rotation of the directory all belong to
    the server, and a second copy of them here would be a copy that drifts.

    The flow is the one every write of this API takes: mint an antiforgery pair with
    `GET /auth/csrf`, sign in with `POST /auth/admin-login`, mint again (a token is bound to
    the identity it was issued for), then `POST /admin/backups` with the header the
    frontend sends, `X-CSRF-Token`.

    Written for Windows PowerShell 5.1, which ships with Windows: `Invoke-RestMethod` answers
    a 4xx/5xx by throwing, so the refusal body is read out of the exception rather than
    switched off with a 7.x-only parameter.

.PARAMETER BaseUrl
    Origin of the API. The `http` launch profile answers on http://localhost:5159.

.PARAMETER Username
    Administrator to sign in as. The password comes from FLUXY_ADMIN_PASSWORD - the same
    convention the frontend probes use, so the seeded password is never written down.

.EXAMPLE
    $env:FLUXY_ADMIN_PASSWORD = '...'
    powershell -ExecutionPolicy Bypass -File scripts\backup-db.ps1

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\backup-db.ps1 -BaseUrl http://localhost:5159 -Username admin
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'http://localhost:5159',
    [string]$Username = 'admin'
)

$ErrorActionPreference = 'Stop'

$password = $env:FLUXY_ADMIN_PASSWORD

if ([string]::IsNullOrWhiteSpace($password)) {
    throw 'FLUXY_ADMIN_PASSWORD is not set. Export the administrator password and run again.'
}

$base = $BaseUrl.TrimEnd('/')

# The antiforgery pair is a cookie plus a header, and PowerShell has no browser to keep either.
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Token = '',
        $Body = $null
    )

    $headers = @{}

    if ($Token) {
        $headers['X-CSRF-Token'] = $Token
    }

    $arguments = @{
        Method     = $Method
        Uri        = "$base$Path"
        WebSession = $session
        Headers    = $headers
    }

    if ($null -ne $Body) {
        $arguments['Body'] = ($Body | ConvertTo-Json -Depth 5)
        $arguments['ContentType'] = 'application/json'
    }

    try {
        return Invoke-RestMethod @arguments
    }
    catch {
        # A refusal is the answer, not an accident: read the envelope out of the response and
        # let the caller report the code the server chose.
        $response = $_.Exception.Response

        if ($null -eq $response) {
            throw
        }

        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $text = $reader.ReadToEnd()
        $reader.Dispose()

        try {
            return $text | ConvertFrom-Json
        }
        catch {
            return [pscustomobject]@{ code = 'unreadable_response'; message = $text }
        }
    }
}

function Get-CsrfToken {
    $answer = Invoke-Api -Method 'GET' -Path '/auth/csrf'

    if (-not $answer.token) {
        throw 'GET /auth/csrf answered no token.'
    }

    return $answer.token
}

# The pair is minted before the sign-in and again after it: the service refuses a token that was
# issued for a different identity, which is what makes the second mint mandatory rather than tidy.
$null = Get-CsrfToken

$login = Invoke-Api -Method 'POST' -Path '/auth/admin-login' -Token (Get-CsrfToken) -Body @{
    username = $Username
    password = $password
}

# The success code is `authenticated`; every refusal is a different one, and each is reported
# with the server's own sentence rather than a guess.
if ($login.code -ne 'authenticated') {
    throw "The sign-in was refused: $($login.code) - $($login.message)"
}

$answer = Invoke-Api -Method 'POST' -Path '/admin/backups' -Token (Get-CsrfToken)

if ($answer.code -eq 'backup_created') {
    Write-Output "Created $($answer.fileName)"
    exit 0
}

throw "The backup was refused: $($answer.code) - $($answer.message)"
