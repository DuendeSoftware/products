$copyrightHeaderLine1 = "// Copyright (c) Duende Software. All rights reserved."
$copyrightHeaderLine2 = "// See LICENSE in the project root for license information."

$path = "..\"
$excludedDirectories = @("bin", "obj", "migrations", "clients", "hosts")

# Get all *.cs files recursively and filter out the excluded directories
$csFiles = Get-ChildItem -Path $path -Filter "*.cs" -Recurse |
    Where-Object {
        $exclude = $false
        foreach ($directory in $excludedDirectories) {
            if ($_.FullName -like "*\$directory\*") {
                $exclude = $true
                break
            }
        }
        -not $exclude
    }

foreach ($file in $csFiles) {
    $updated = $false
    $content = Get-Content $file.FullName -Raw
    
    if($content -match "^\s")
    {
        Write-Host "Leading whitespace detected in $file.FullName"
        $updated = $true
        $content = $content.TrimStart()
    }
    
    $lines = Get-Content $file.FullName 
    $firstLine = $lines | Select-Object -First 1
    $secondLine = $lines | Select-Object -Skip 1 -First 1

   if ($firstLine -ne $copyrightHeaderLine1 -or $secondLine -ne $copyrightHeaderLine2) {
        $updated = $true
        $content = $copyrightHeaderLine1 + "`r`n" + $copyrightHeaderLine2 + "`r`n`r`n`r`n" + $content
    } else  {
        $nonCopyrightContent = $lines | Select-Object -skip 2

        for ($i = 0; $i -lt $nonCopyrightContent.Count; $i++) {
            if (-not [string]::IsNullOrWhiteSpace($nonCopyrightContent[$i])) {
                break
            } 
        }
        if($i -gt 2) {
            Write-Host "Removing blank lines between copyright header and content"
            $updated = $true
        }
        if($i -lt 2) {
            Write-Host "Adding blank lines between copyright header and content"
            $updated = $true
        }
        $nonCopyrightContent = $nonCopyrightContent[$i..($nonCopyrightContent.Count - 1)]
        $content = @($copyrightHeaderLine1, $copyrightHeaderLine2, "", "") + $nonCopyrightContent
    }

    if($updated)
    {
        $content | Set-Content -Path $file.FullName
        Write-Host "Updated $($file.FullName)"
    }
}

Write-Host "Copyright header check completed."