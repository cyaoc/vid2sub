$supportedExtensions = @(".mp4", ".avi", ".mkv", ".mov", ".wmv", ".webm", ".mp3", ".wav", ".m4a")
# Prompt the user for a file path
do {
    $fileAddress = Read-Host "Enter file path or drag and drop the file"
    $fileExists = Test-Path $fileAddress
    $extensionIsValid = $supportedExtensions -contains [System.IO.Path]::GetExtension($fileAddress).ToLower()

    if (-not $fileExists) {
        Write-Host "File not found. Please try again."
    } elseif (-not $extensionIsValid) {
        Write-Host "Unsupported file type. Please try again."
    }
} while (-not $fileExists -or -not $extensionIsValid)

# Ask the user to choose a priority option
$options = [System.Management.Automation.Host.ChoiceDescription[]]@(
    (New-Object System.Management.Automation.Host.ChoiceDescription "&Speed", "For faster processing with medium model."),
    (New-Object System.Management.Automation.Host.ChoiceDescription "&Performance", "For best quality with large model, but slower.")
)
$userChoice = $host.ui.PromptForChoice("Priority", "Speed or Performance?", $options, 1)
$model = "large-v3"
switch ($userChoice) {
    0 { 
        $model = "medium"
        Write-Host "Speed selected. Using medium model." 
    }
    1 { 
        Write-Host "Performance selected. Using large model." 
    }
}

# Default language option
$language = Read-Host "Enter language code (e.g., 'en', 'ja', 'zh') or leave blank for 'auto'"
if ([string]::IsNullOrWhiteSpace($language)) {
    $language = "auto"
}

# Ask if OpenVINO acceleration is desired
$cpuInfo = Get-WmiObject Win32_Processor
$isIntelCPU = $cpuInfo.Manufacturer -match "Intel"
$mainProgramPath = Join-Path $PSScriptRoot "bin\cpu\main"
if ($isIntelCPU) {
    $accelerate = Read-Host "Intel CPU detected. Enable OpenVINO? (y/n, default 'y')"
    if ([string]::IsNullOrWhiteSpace($accelerate)) {
        $accelerate = "y"
    }
    if ($accelerate -eq "y") {
        $setupvarsPath = Join-Path $PSScriptRoot "bin\openvino\setupvars.ps1"
        & $setupvarsPath
        $mainProgramPath = Join-Path $PSScriptRoot "bin\openvino\main"
    }
} else {
    Write-Host "Non-Intel CPU. OpenVINO not enabled."
}
# Convert the video file to an audio file (WAV format).
$ffmpegPath = Get-Command ffmpeg -ErrorAction SilentlyContinue
if ($null -eq $ffmpegPath) {
    $ffmpegPath = Join-Path $PSScriptRoot "bin\ffmpeg"
} else {
    $ffmpegPath = $ffmpegPath.Source
}
$tmpDir = Join-Path $PSScriptRoot "tmp"
if (-not (Test-Path $tmpDir)) {
    New-Item -ItemType Directory -Path $tmpDir
    Write-Host "tmp directory created."
}
$taskName = "Task_" + (Get-Date -Format "yyyy-MM-dd_HH-mm-ss")
$outputFileAddress = "$tmpDir\" + $taskName + ".wav"
& $ffmpegPath -y -i $fileAddress -acodec pcm_s16le -ac 1 -ar 16000 -vn $outputFileAddress

if ($?) {
    if (Test-Path $outputFileAddress) {
        Write-Host "Audio file created. Continuing..."
        $modelsDir = Join-Path $PSScriptRoot "models"
        if (-not (Test-Path $modelsDir)) {
            New-Item -ItemType Directory -Path $modelsDir
            Write-Host "models directory created."
        }
        function Download-ModelFile {
            param (
                [string]$Url,
                [string]$DestinationPath
            )
            if (-not (Test-Path $DestinationPath)) {
                Write-Host "Downloading $DestinationPath..."
                Start-BitsTransfer -Source $Url -Destination $DestinationPath
                Write-Host "Download completed: $DestinationPath"
            }
        }
        $baseRepoUrl = "https://huggingface.co/cyaoc/whisper-ggml/resolve/main/models/"
        $baseModelFileName = "ggml-$model.bin"
        $baseModelFilePath = Join-Path $modelsDir $baseModelFileName
        $baseModelUrl = $baseRepoUrl + $baseModelFileName
        Download-ModelFile -Url $baseModelUrl -DestinationPath $baseModelFilePath
        if ($accelerate -eq "y") {
            $openvinoXmlFileName = "ggml-$model-encoder-openvino.xml"
            $openvinoBinFileName = "ggml-$model-encoder-openvino.bin"
            
            $openvinoXmlFilePath = Join-Path $modelsDir $openvinoXmlFileName
            $openvinoBinFilePath = Join-Path $modelsDir $openvinoBinFileName
            $openvinoXmlUrl = $baseRepoUrl + $openvinoXmlFileName
            $openvinoBinUrl = $baseRepoUrl + $openvinoBinFileName
            
            Download-ModelFile -Url $openvinoXmlUrl -DestinationPath $openvinoXmlFilePath
            Download-ModelFile -Url $openvinoBinUrl -DestinationPath $openvinoBinFilePath
        }
        & $mainProgramPath -m $baseModelFilePath -l $language -f $outputFileAddress -ovtt
        if ($?) {
            $outputsDir = Join-Path $PSScriptRoot "outputs"
            if (-not (Test-Path $outputsDir)) {
                New-Item -ItemType Directory -Path $outputsDir
                Write-Host "outputs directory created."
            }
            $extension = ".vtt"
            $fileNameWithoutExtension = [System.IO.Path]::GetFileNameWithoutExtension($fileAddress)
            $index = 0
            $newName = $fileNameWithoutExtension + $extension
            while (Test-Path $newName) {
                $index++
                $newName = $fileNameWithoutExtension + "(" + $index + ")" + $extension
            }
            $vttFileAddress = $outputFileAddress + $extension
            if (Test-Path $vttFileAddress) {
                $newVttPath = "$outputsDir\$newName"
                Move-Item $vttFileAddress -Destination $newVttPath -Force
                Write-Host "VTT file moved and renamed to $newVttPath"
            }
        }
    } else {
        Write-Host "Error: Audio file missing post-conversion. Check path and permissions."
    }
}

if (Test-Path $outputFileAddress) {
    Remove-Item $outputFileAddress -Force
    Write-Host "Temporary audio file has been deleted."
}