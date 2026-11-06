$bin = Join-Path $env:LOCALAPPDATA "GymDesk\mariadb\mariadb-11.4.13-winx64\bin\mysqld.exe"
$ini = Join-Path $env:LOCALAPPDATA "GymDesk\mariadb-data\my.ini"

if (-not (Test-Path $bin)) {
    Write-Error "MariaDB was not found at $bin"
    exit 1
}

$listening = Get-NetTCPConnection -LocalPort 3306 -State Listen -ErrorAction SilentlyContinue
if ($listening) {
    Write-Host "Database is already listening on port 3306."
    exit 0
}

Start-Process -FilePath $bin -ArgumentList "--defaults-file=`"$ini`"" -WindowStyle Hidden
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 1
    if (Get-NetTCPConnection -LocalPort 3306 -State Listen -ErrorAction SilentlyContinue) {
        Write-Host "MariaDB is listening on port 3306."
        exit 0
    }
}

Write-Error "MariaDB did not start. Check $env:LOCALAPPDATA\GymDesk\mariadb-data for the error log."
exit 1
