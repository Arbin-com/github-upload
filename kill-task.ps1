# copy https://stackoverflow.com/questions/958123/powershell-script-to-check-an-application-thats-locking-a-file
Function Get-LockingProcess {
    [cmdletbinding()]
    Param(
        [Parameter(Position=0, Mandatory=$True,
        HelpMessage="What is the path or filename? You can enter a partial name without wildcards")]
        [Alias("name")]
        [ValidateNotNullorEmpty()]
        [string]$path,
        [string]$handleExePath
    )
    
    if([string]::IsNullOrWhiteSpace($handleExePath)) {
        $handleExePath = "$PSScriptRoot/handle/handle.exe"
    }

    $data = &$handleExePath -u $path -nobanner
    $result = New-Object 'Collections.Generic.List[System.Object]'
    $pids = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach($item in $data) {
        $match = $item | Select-String -Pattern '(?<Name>\w+\.\w+)\s+pid:\s+(?<PID>\d+)\s+type:\s+(?<Type>\w+)\s+(?<User>.+?)\s+\w+\s+(?<Path>.*)'
        if($null -eq $match -or $match.Matches.Success -eq $false) {
            continue
        }   
        $match = $match.Matches[0]
        $pidValue = [int]$match.groups["PID"].value
        if(!$pids.Add($pidValue)) {
            continue 
        }
        $obj = [pscustomobject]@{
            FullName = $match.groups["Name"].value
            Name = $match.groups["Name"].value.split(".")[0]
            PID = $pidValue
            Type = $match.groups["Type"].value
            User = $match.groups["User"].value.trim()
            Path = $match.groups["Path"].value
        }
        $result.Add($obj)
    }
    return $result.ToArray()
}

Function set-KillLockingProcess {
    Param(
        [string]$path,
        [string]$handleExePath
    )

    $killTasks = Get-LockingProcess -path $path -handleExePath $handleExePath
    foreach($item in $killTasks) {
        Stop-Process -Id $item.PID -PassThru -Force
    }
    
    if($killTasks.length -gt 0) {
        Start-Sleep -Seconds 10
    }

    return $killTasks
}
