# Define the URL for the CosmosDB Emulator MSI
$cosmosDbEmulatorUrl = "https://aka.ms/cosmosdb-emulator"

# Define the path where the MSI will be downloaded
$downloadPath = "$env:TEMP\CosmosDBEmulator.msi"

# Check if CosmosDB Emulator is installed
$emulatorPath = "C:\Program Files\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe"
if (-Not (Test-Path $emulatorPath)) {
    Write-Output "CosmosDB Emulator not found. Downloading and installing..."

    if (-Not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator"))
    {
        Write-Output "This script requires administrative privileges. Please run bootemulator.ps1 as admin."
        exit
    }

    # Download the MSI
    Invoke-WebRequest -Uri $cosmosDbEmulatorUrl -OutFile $downloadPath
    
    # Install the CosmosDB Emulator MSI and emit and errors and warnings to a log file.
    $logFile = "$env:TEMP\CosmosDBEmulatorInstall.log"
    Start-Process msiexec.exe -ArgumentList "/i $downloadPath /quiet /norestart /lew $logFile" -Wait

    Get-Content -Path $logFile
} else {
    Write-Output "CosmosDB Emulator is already installed."
}

# Check if the CosmosDB Emulator is already running
$emulatorProcess = Get-Process -Name "CosmosDB.Emulator" -ErrorAction SilentlyContinue
if ($null -ne $emulatorProcess) {
    Write-Output "CosmosDB Emulator is already running."
} else {
    # Start the CosmosDB Emulator
    Write-Output "Starting CosmosDB Emulator..."
    Start-Process $emulatorPath -ArgumentList "/NoFirewall /Port=8081 
        /overrides=""enableVectorEmbedding:true;enableVectorIndexQuantizedIndexType:true;enableVectorIndexDiskANNIndexType:true;enableBinaryEncodingOfContent:true;maxResourceSize:2097152"""

    # Wait for the emulator to start
    $emulatorUri = "https://localhost:8081/_explorer/index.html"
    $timeoutInSeconds = 60
    $startTime = Get-Date
    $isRunning = $false

    Write-Output "Waiting for CosmosDB Emulator to start..."
    while (-not $isRunning) {
        try {
            # Send a request to check if the emulator is running
            Invoke-WebRequest -Uri $emulatorUri -UseBasicParsing -ErrorAction Stop | Out-Null
            $isRunning = $true
            Write-Output "CosmosDB Emulator has started successfully."
        } catch {
            # Check if timeout has been reached
            $elapsedTime = (Get-Date) - $startTime
            if ($elapsedTime.TotalSeconds -ge $timeoutInSeconds) {
                Write-Output "Timeout reached. CosmosDB Emulator did not start within $timeoutInSeconds seconds."
                exit 1
            }

            # Wait for a short interval before retrying
            Start-Sleep -Seconds 2
        }
    }    
}