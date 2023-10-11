

Function set-MySerialize {
    param(
        [System.Object] $value
    )

    return [System.Management.Automation.PSSerializer]::Serialize($value)
}

Function set-MyDeserialize {
    param(
        [System.Object] $value
    )

    return [System.Management.Automation.PSSerializer]::Deserialize($value)
}

function set-PiplineAutoVersion {

    param (
        [string] $currentCommitID,
        [string] $codeVersion
    )

    $hsaForwarded = git status | Select-String -Pattern 'behind.*can.*fast-forwarded' -Quiet
    if ($hsaForwarded -eq $true) {
        Write-Output "already has new commit"
        return
    }

    Write-Host "code version: $codeVersion"
    $prevVersion = Get-GitPrevVersion
    $currentSetVersion = $null
    if ($null -eq $prevVersion) {
        $currentSetVersion = [ArbinUtil.ArbinVersion]::CreateDefault()
    }
    else {
        Remove-RemoteTagByVersion -Versions $prevVersion.Previous
        $currentSetVersion = $prevVersion.Version

        if (![string]::IsNullOrWhiteSpace($prevVersion.Commit)) {
            if ($currentCommitID.StartsWith($prevVersion.Commit)) {
                Write-Output "already set tag '$currentSetVersion'"
                return
            }
        }

        ++$currentSetVersion.Build
    }
    
    $parseCodeVersion = $null
    if (![string]::IsNullOrWhiteSpace($codeVersion) -and 
        [ArbinUtil.ArbinVersion]::Parse($codeVersion, [ref] $parseCodeVersion) -and
        !$parseCodeVersion.HasSuffix
    ) {
        if ($currentSetVersion.Major -ne $parseCodeVersion.Major -or $currentSetVersion.Minor -ne $parseCodeVersion.Minor) {
            $currentSetVersion.Major = $parseCodeVersion.Major
            $currentSetVersion.Minor = $parseCodeVersion.Minor
            $currentSetVersion.Build = $parseCodeVersion.Build
            Write-Host "code version changed: $parseCodeVersion`n"
        }
    }
    
    $versionText = "$currentSetVersion"

    git config --global user.email "arbin-test@arbin.com"
    git config --global user.name "arbin-test"

    git tag -a $versionText -m "#auto"
    git push origin refs/tags/$versionText
}


function dotnet-remove-other-file {
    param (
        [string] $binPath
    )

    [string[]] $removePaths = @("de", "es", "ja", "ru", "tr", "fr", "hu", "it", "pt", "sk", "sv", "zh-tw", "zh-cn")
    for ($j = 0; $j -lt ($removePaths.length); $j++) {
        $pathName = $removePaths[$j];
        $remotePath = "$binPath/$pathName"
        Remove-Item $remotePath -Force -Recurse -ErrorAction SilentlyContinue
    }
    remove-item $binPath/DevExpress*.xml
    remove-item $binPath/*.pdb
    remove-item $binPath/empty.txt -ErrorAction SilentlyContinue

    $tempItems = Get-ChildItem $binPath/*.dll -Recurse
    for ($j = 0; $j -lt ($tempItems.length); $j++) {
        $item = $tempItems[$j];
        $directoryName = $item.DirectoryName;
        $baseName = $item.BaseName;
        $removePath = "$directoryName/$baseName"
        Remove-Item "$removePath.xml" -Force -Recurse -ErrorAction SilentlyContinue
    }
}

$global:commit_log_file_default_number = "1000"

function add-commit-log-file {
    param (
        [string] $fullPath,
        [string] $maxNumber,
        [string] $commitID
    )

    if (!([string]::IsNullOrEmpty($suffix))) {
        $maxNumber = "-n $maxNumber"
    }
    else {
        $maxNumber = "-n $global:commit_log_file_default_number"
    }

    git log $commitID $maxNumber --date=format-local:'%Y-%m-%d %H:%M:%S' --pretty=format:'%ad <%ce> %s' | Out-File -FilePath $fullPath
}

function get-dest-suffix {
    param (
        [string] $srcBranch,
        [string[]] $stableVersions
    )


    $ignoreCase = 'CurrentCultureIgnoreCase'
    $isTag = $srcBranch.StartsWith('refs/tags/', $ignoreCase) 
    $isMaster = $srcBranch.StartsWith('refs/heads/master', $ignoreCase) #only check prefix

    $srcBranch = $srcBranch.ToUpper()
    if ($isTag) {
        if ($stableVersions -eq $null) {
            $stableVersions = @("_PV_", "_ZY_", "_RD_")
        }        
        for ($j = 0; $j -lt ($stableVersions.length); $j++) {
            $content = $stableVersions[$j];
            if ($srcBranch.Contains($content.ToUpper())) {
                return "stable-tag"
            }
        }

        if ($srcBranch.Contains("_TY_")) {
            return "hotfix-tag"
        }

        return "hotfix-branch"
    }

    if (!($isMaster)) { return "" }

    return "hotfix-branch"
}

function need-upload {
    param (
        [string] $srcBranch
    )

    $suffix = get-dest-suffix -srcBranch $srcBranch
    return -not ([string]::IsNullOrEmpty($suffix))
}

function set-EmptyJiraKey {
    param (
        [object] $csvText,
        [string] $projName,
        [ArbinUtil.ArbinVersion] $version
    )

    $oldPath = resolve-path ./    
    cd EmptyJiraKey

    git config --global user.email "arbin-test@arbin.com"
    git config --global user.name "arbin-test"

    git pull origin main --rebase
    
    $relativePrefix = $projName + '/' + [ArbinUtil.Util]::GetRelativePathPrefix($version)
    $file = "$relativePrefix/$($version.ToString($false)).csv"
    if (!(Test-Path $file)) { New-Item -Path $file -Force }

    Set-Content -path $file -value $csvText -Encoding utf8BOM

    $notChanged = git status | Select-String -Pattern 'working tree clean' -Quiet
    if ($notChanged -ne $true) {
        git add .
        git commit -m "🤖#auto"
        git pull origin main --rebase
        git push origin main --force
    }

    cd $oldPath
}

function set-ProjectUrl {
    param (
        [string] $commitID,
        [string] $fileNameNotExt,
        [string] $relativePrefix,
        [string] $tag
    )

    git pull origin main --rebase

    $versionPath = "$relativePrefix/$fileNameNotExt.md"
    if ((Test-Path $versionPath)) {
        Remove-Item -Path $versionPath -Force
    }

    $codeData = @{CommitID = "$commitID"; } | ConvertTo-Json

    New-Item -Path $versionPath -Force
    Add-Content -Path $versionPath -Value "## URL"
    Add-Content -Path $versionPath -Value "🔗https://github.com/$userAndRepo/releases/tag/$tag"
    Add-Content -Path $versionPath -Value "`n"
    Add-Content -Path $versionPath -Value "## Repository Information"
    Add-Content -Path $versionPath -Value '```c'
    Add-Content -Path $versionPath -Value $codeData
    Add-Content -Path $versionPath -Value '```'

    $notChanged = git status | Select-String -Pattern 'working tree clean' -Quiet
    if ($notChanged -eq $true) {
        return
    }

    git add .
    git commit -m "🤖#auto, commitID $commitID"
    git pull origin main --rebase
    git push origin main --force    
}

function github-upload-new-otehrs {
    param (
        [string] $commitID,
        [string] $srcBranch,
        [string] $userAndRepo,
        [string] $token,
        [string] $existTagName,
        [string] $tagMessage,
        [string] $wrapPathName,
        [string] $projName
    )

    $oldPath = resolve-path ./    

    $branchFullName = $srcBranch
    if ($branchFullName.StartsWith('refs/tags/')) {
        $branchFullName = $branchFullName.Replace('refs/tags/', '')
    }
    else {
        $branchFullName = $branchFullName.Replace('refs/heads/', '')
    }
    
    $repoName = $userAndRepo.Split("/")[1]

    $partTags = $existTagName.Split("/")
    $fullTagName = $projName + '.' + $existTagName
    $changedZipName = $projName + '.' + $partTags[$partTags.length - 1]

    Remove-Item $changedZipName -Force -Recurse -ErrorAction SilentlyContinue
    rename-item "$wrapPathName" -newname "$changedZipName" -PassThru
    $assetses = "$changedZipName.zip"
    Remove-Item $assetses -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path $changedZipName -DestinationPath $assetses

    git clone https://$token@github.com/$userAndRepo


    cd ./github-upload
    npm update

    cd ../
    cd ./$repoName

    git config --global user.email "arbin-test@arbin.com"
    git config --global user.name "arbin-test"

    $parseVersion = $null
    if ([ArbinUtil.ArbinVersion]::Parse($branchFullName, [ref] $parseVersion)) {
        $relativePrefix = "$projName/" + ([ArbinUtil.Util]::GetRelativePathPrefix($parseVersion))
        set-ProjectUrl -tag $fullTagName -commitID $commitID -fileNameNotExt $parseVersion.ToString($false) -relativePrefix $relativePrefix
    }

    node ../github-upload/main.js $srcBranch $userAndRepo $token $fullTagName " $tagMessage " $assetses " $oldPath " " $tagSuffix "
}


function github-upload-new {
    param (
        [string] $commitID,
        [string] $srcBranch,
        [string] $userAndRepo,
        [string] $suffix,
        [string] $zipPrefix,
        [string] $token,
        [string] $existTagName,
        [string] $tagMessage,
        [string] $wrapPathName
    )

    $oldPath = resolve-path ./    


    $branchFullName = $srcBranch
    if ($branchFullName.StartsWith('refs/tags/')) {
        $branchFullName = $branchFullName.Replace('refs/tags/', '')
    }
    else {
        $branchFullName = $branchFullName.Replace('refs/heads/', '')
    }
    
    $userAndRepo = $userAndRepo + "-" + $suffix
    $repoName = $userAndRepo.Split("/")[1]

    $partTags = $existTagName.Split("/")
    $changedZipName = $zipPrefix + $partTags[$partTags.length - 1]

    Remove-Item $changedZipName -Force -Recurse -ErrorAction SilentlyContinue
    rename-item "$wrapPathName" -newname "$changedZipName" -PassThru
    $assetses = "$changedZipName.zip"
    Remove-Item $assetses -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path $changedZipName -DestinationPath $assetses

    git clone https://$token@github.com/$userAndRepo

    cd ./github-upload
    npm update

    cd ../
    cd ./$repoName

    git config --global user.email "arbin-test@arbin.com"
    git config --global user.name "arbin-test"

    $parseVersion = $null
    if ([ArbinUtil.ArbinVersion]::Parse($branchFullName, [ref] $parseVersion)) {
        $relativePrefix = [ArbinUtil.Util]::GetRelativePathPrefix($parseVersion)
        set-ProjectUrl -tag $parseVersion -commitID $commitID -fileNameNotExt $parseVersion.ToString($false) -relativePrefix $relativePrefix
    }

    node ../github-upload/main.js $srcBranch $userAndRepo $token $existTagName " $tagMessage " $assetses " $oldPath " " $tagSuffix "
}

function github-upload {
    param (
        [string] $commitID,
        [string] $srcBranch,
        [string] $userAndRepo,
        [string] $token,
        [string] $existTagName,
        [string] $tagMessage,
        [string] $wrapPathName,
        [string[]] $stableVersions
    )
    # ensure root path.
    $oldPath = resolve-path ./
    
    $ignoreCase = 'CurrentCultureIgnoreCase'
    $suffix = get-dest-suffix -srcBranch $srcBranch -stableVersions $stableVersions
    $repoSuffix = ""
    $tagSuffix = ""
    $isTag = $srcBranch.StartsWith('refs/tags/', $ignoreCase) 
    switch ($suffix) {
        'hotfix-branch' {
            $tagSuffix = "branch"
            if ($isTag) {
                $headBranchs = git branch -a --contains $commitID
                $tagByBranchs = $headBranchs.split("`n")    
                
                $hasMasterBranch = $false
                $solveBranchName = ""
                echo "tagByBranchs:"
                echo $tagByBranchs
                echo "`n"
                for ($j = 0; $j -lt ($tagByBranchs.length); $j++) {
                    $tempParts = $tagByBranchs[$j];
                    if ($tempParts.Contains("detached at")) { continue }
                    $branchNames = $tempParts.split(' ')
                    $branchName = $branchNames[$branchNames.length - 1]
                    $branchNames = $branchName.split('/')
                    $branchName = $branchNames[$branchNames.length - 1]
                    if ($branchName -eq "master") { $hasMasterBranch = $true }
                    elseif ($solveBranchName -eq "") { $solveBranchName = $branchName }
                    elseif ($hasMasterBranch) { break; }
                }
                if ($hasMasterBranch -and ($solveBranchName -eq "")) {
                    $tagSuffix = "tag"
                }
                else {
                    $tagMessage = "$tagMessage`n" + "tag $existTagName`n"
                    $existTagName = $solveBranchName
                }
            }
            else {
                
            }
            
            $repoSuffix = "hotfix"
        }
        'hotfix-tag' {
            $repoSuffix = "hotfix"
            $tagSuffix = "tag"
        }
        'stable-tag' {
            $repoSuffix = "stable"    
            $tagSuffix = "tag"
        }
        default {
            echo "No resources need to be uploaded"
            return 
        }
    }

    $userAndRepo = $userAndRepo + "-" + $repoSuffix
    $repoName = $userAndRepo.Split("/")[1]

    $partTags = $existTagName.Split("/")
    $changedZipName = $partTags[$partTags.length - 1]
    if ($repoSuffix -eq "hotfix") {
        $timeValue = ((get-date).ToUniversalTime()).ToString("yyyyMMdd-HH-mm")
        $changedZipName = "$changedZipName-$timeValue"
    }

    Remove-Item $changedZipName -Force -Recurse -ErrorAction SilentlyContinue
    rename-item "$wrapPathName" -newname "$changedZipName" -PassThru
    $assetses = "$changedZipName.zip"
    Compress-Archive -Path $changedZipName -DestinationPath $assetses

    git clone https://$token@github.com/$userAndRepo

    cd ./github-upload
    npm update

    cd ../
    cd ./$repoName

    git config --global user.email "arbin-test@arbin.com"
    git config --global user.name "arbin-test"

    # git tag -f $existTagName -m $tagMessage
    # git push --force origin :refs/tags/$existTagName

    node ../github-upload/main.js $srcBranch $userAndRepo $token $existTagName $tagMessage $assetses "$oldPath" $tagSuffix
}
