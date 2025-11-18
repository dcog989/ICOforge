# Builder Toolbox - Maintenance Actions

function Remove-BuildOutput {
    param([switch]$NoConfirm)

    if (-not $NoConfirm) {
        if (-not (Confirm-IdeShutdown -Action "Clean Solution")) { return }
        if (-not (Confirm-ProcessTermination -Action "Clean")) { return }
    }

    Write-Log "Cleaning build files..." "CONSOLE"

    # Method 1: Use dotnet clean for proper .NET cleanup
    Write-Log "Running 'dotnet clean'..." 
    $cleanResult = Invoke-DotnetCommand -Command "clean" -Arguments "`"$Script:SolutionFile`" -c Release -v minimal" -IgnoreErrors
    if ($cleanResult.Success) {
        Write-Log "Dotnet clean completed" "SUCCESS"
    }

    # Method 2: Manual cleanup for comprehensive removal
    $searchPaths = @($Script:SolutionRoot)
    
    # Also include repo root if different from solution root
    if ($Script:RepoRoot -ne $Script:SolutionRoot) {
        $searchPaths += $Script:RepoRoot
    }

    $buildDirs = @()
    foreach ($searchPath in $searchPaths) {
        Write-Log "Searching for build directories in: $searchPath"
        $foundDirs = Get-ChildItem -Path $searchPath -Include "bin", "obj" -Directory -Recurse -ErrorAction SilentlyContinue
        $buildDirs += $foundDirs
    }

    # Remove duplicates and exclude any paths that might be in use
    $buildDirs = $buildDirs | Sort-Object -Property FullName -Unique | Where-Object {
        try {
            # Test if directory can be accessed (not locked)
            Get-ChildItem $_.FullName -ErrorAction Stop | Out-Null
            $true
        }
        catch {
            Write-Log "Skipping locked directory: $($_.FullName)" "WARN"
            $false
        }
    }

    if ($buildDirs.Count -gt 0) {
        Write-Log "Found $($buildDirs.Count) build directories to remove"
        $counter = 0
        $total = $buildDirs.Count
        
        foreach ($dir in $buildDirs) {
            $counter++
            $percentComplete = ($counter / $total) * 100
            Write-Progress -Activity "Cleaning build directories" -Status "Removing $($dir.Name)" -PercentComplete $percentComplete -CurrentOperation $dir.FullName
            
            try {
                Remove-Item -Path $dir.FullName -Recurse -Force -ErrorAction Stop
                Write-Log "Removed: $($dir.FullName)" "DEBUG"
            }
            catch {
                Write-Log "Failed to remove: $($dir.FullName) - $($_.Exception.Message)" "WARN"
            }
        }
        
        Write-Progress -Activity "Cleaning build directories" -Completed
        Start-Sleep -Milliseconds 100  # Ensure progress bar clears
        Write-Log "Removed $($buildDirs.Count) build directories" "SUCCESS"
    }
    else {
        Write-Log "No 'bin' or 'obj' directories found to clean."
    }

    # Cleanup Velopack releases if enabled
    if ($Script:UseVelopack) {
        $releaseDir = Join-Path $Script:SolutionRoot "Releases"
        if (Test-Path $releaseDir) {
            Write-Log "Removing Velopack releases directory..." 
            try {
                Remove-Item -Path $releaseDir -Recurse -Force -ErrorAction Stop
                Write-Log "Removed Velopack releases directory." "SUCCESS"
            }
            catch {
                Write-Log "Failed to remove Velopack releases: $($_.Exception.Message)" "WARN"
            }
        }
    }

    # Cleanup any packages directories
    $packageDirs = Get-ChildItem -Path $Script:SolutionRoot -Include "packages", "package", "publish" -Directory -Recurse -ErrorAction SilentlyContinue
    foreach ($pkgDir in $packageDirs) {
        try {
            Remove-Item -Path $pkgDir.FullName -Recurse -Force -ErrorAction Stop
            Write-Log "Removed package directory: $($pkgDir.FullName)" "DEBUG"
        }
        catch {
            Write-Log "Failed to remove package directory: $($pkgDir.FullName)" "WARN"
        }
    }

    Clear-BuildVersionCache
    Write-Log "Cleanup completed." "SUCCESS"
}

function Update-VersionNumber {
    if (-not (Test-ProjectFilesExist)) { return }
    if (-not (Confirm-IdeShutdown -Action "Change Version Number")) { return }

    if (-not (Test-Path $Script:MainProjectFile)) {
        Write-Log "Project file not found at: $($Script:MainProjectFile)" "ERROR"
        return
    }

    $currentVersion = Get-CachedBuildVersion
    Write-Log "Current version: $currentVersion" "CONSOLE"

    $newVersion = Read-Host "Enter new version, or 'X' to cancel"
    if ([string]::IsNullOrWhiteSpace($newVersion) -or $newVersion.ToLower() -eq 'x') {
        Write-Log "Operation cancelled." "INFO"
        return
    }

    try {
        if ($newVersion -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$') {
            throw "Invalid version format. Use Semantic Versioning (e.g., 1.2.3 or 1.2.3-beta1)."
        }

        $csprojContent = Get-Content $Script:MainProjectFile -Raw
        $csproj = [xml]$csprojContent
        $propertyGroup = $csproj.SelectSingleNode("//PropertyGroup[Version]")

        if ($null -eq $propertyGroup) {
            $propertyGroup = $csproj.SelectSingleNode("//PropertyGroup[1]")
        }

        if ($null -ne $propertyGroup.Version) {
            $propertyGroup.Version = $newVersion
        }
        else {
            $versionElement = $csproj.CreateElement("Version")
            $versionElement.InnerText = $newVersion
            $propertyGroup.AppendChild($versionElement) | Out-Null
        }

        # Detect existing indentation to preserve file formatting.
        $indentChars = "    " # Default to 4 spaces
        $match = $csprojContent | Select-String -Pattern '(?m)^(\s+)<PropertyGroup>'
        if ($null -ne $match) {
            $indentChars = $match.Matches[0].Groups[1].Value
        }
        else {
            Write-Log "Could not detect project file indentation. Defaulting to 4 spaces." "WARN"
        }

        # Use a custom XmlWriter to preserve indentation.
        $writerSettings = New-Object System.Xml.XmlWriterSettings
        $writerSettings.Indent = $true
        $writerSettings.IndentChars = $indentChars
        $writerSettings.Encoding = [System.Text.UTF8Encoding]::new($false) # UTF-8 without BOM

        $xmlWriter = $null
        try {
            $xmlWriter = [System.Xml.XmlWriter]::Create($Script:MainProjectFile, $writerSettings)
            $csproj.Save($xmlWriter)
        }
        finally {
            if ($null -ne $xmlWriter) {
                $xmlWriter.Close()
            }
        }

        $Script:BuildVersion = $newVersion
        Write-Log "Version updated to $newVersion in $($Script:MainProjectFile)" "SUCCESS"
    }
    catch {
        Write-Log "Failed to update version: $_" "ERROR"
    }
}

function Invoke-UnitTests {
    if (-not (Test-ProjectFilesExist)) { return }
    Write-Log "Running unit tests for solution..." "CONSOLE"

    $result = Invoke-DotnetCommand -Command "test" -Arguments "`"$Script:SolutionFile`""

    if ($result.Success) {
        Write-Log "Test run successful. Check log for details (e.g., if no tests were found)." "SUCCESS"
    }
    else {
        Write-Log "One or more tests failed. Check the log file for details." "ERROR"
        Invoke-ItemSafely -Path (Get-LogFile) -ItemType "Log file"
    }
}