[CmdletBinding()]
param(
    [ValidateSet('Prepare', 'Live', 'SelfTest')][string]$Mode = 'Prepare',
    [ValidateSet('Gemini', 'Groq')][string]$Provider,
    [string]$Model,
    [string]$Cases,
    [string]$Output,
    [ValidateRange(1, 100)][int]$Limit = 20,
    [ValidateRange(1, 120)][int]$TimeoutSeconds = 60,
    [switch]$PromptForApiKey
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$project = Join-Path $repositoryRoot 'test\ai-prompts\AI.PromptEvaluation.csproj'
$runnerArgs = @('run', '--project', $project, '--configuration', 'Release', '--', ('--' + $Mode.ToLowerInvariant()), '--limit', "$Limit", '--timeout-seconds', "$TimeoutSeconds")
if ($Model) { $runnerArgs += @('--model', $Model) }
if ($Cases) { $runnerArgs += @('--cases', [IO.Path]::GetFullPath($Cases)) }
if ($Output) { $runnerArgs += @('--output', [IO.Path]::GetFullPath($Output)) }
$previousKey = [Environment]::GetEnvironmentVariable('AI_PROMPT_API_KEY', 'Process')
$previousProvider = [Environment]::GetEnvironmentVariable('AI_PROMPT_PROVIDER', 'Process')
try {
    if ($Provider) { [Environment]::SetEnvironmentVariable('AI_PROMPT_PROVIDER', $Provider, 'Process') }
    if ($PromptForApiKey) {
        if ($Mode -ne 'Live') { throw 'API key entry is only needed for Live mode.' }
        $secureKey = Read-Host 'Grading provider API key (hidden; process environment only)' -AsSecureString
        $keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
        try { [Environment]::SetEnvironmentVariable('AI_PROMPT_API_KEY', [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer), 'Process') }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer); $secureKey.Dispose() }
    }
    Push-Location -LiteralPath $repositoryRoot
    try { & dotnet @runnerArgs; $runnerExitCode = $LASTEXITCODE }
    finally { Pop-Location }
}
finally {
    [Environment]::SetEnvironmentVariable('AI_PROMPT_API_KEY', $previousKey, 'Process')
    [Environment]::SetEnvironmentVariable('AI_PROMPT_PROVIDER', $previousProvider, 'Process')
}
exit $runnerExitCode
