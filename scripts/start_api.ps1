Get-Content .env | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
        $idx = $line.IndexOf("=")
        $key = $line.Substring(0, $idx).Trim()
        $val = $line.Substring($idx + 1).Trim()
        [System.Environment]::SetEnvironmentVariable($key, $val)
    }
}
$env:ASPNETCORE_ENVIRONMENT = "Development"
if ($env:ConnectionStrings__Default) {
    $env:ConnectionStrings__Default = "Server=127.0.0.1;Port=3307;Database=edutwin;User=root;Password=$($env:MYSQL_ROOT_PASSWORD);"
}
dotnet run --no-build --project src\EduTwin.API\EduTwin.API.csproj --urls "http://localhost:5000"
