
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

    if(!([string]::IsNullOrEmpty($suffix)))
    {
        $maxNumber = "-n $maxNumber"
    }
    else
    {
        $maxNumber = "-n $global:commit_log_file_default_number"
    }

    git log $commitID $maxNumber --date=format-local:'%Y-%m-%d %H:%M:%S' --pretty=format:'%ad <%ce> %s' | Out-File -FilePath $fullPath
}

function get-dest-suffix {
    param (
        [string] $srcBranch
    )


    $ignoreCase = 'CurrentCultureIgnoreCase'
    $isTag = $srcBranch.StartsWith('refs/tags/', $ignoreCase) 
    $isMaster = $srcBranch.StartsWith('refs/heads/master', $ignoreCase) #only check prefix

    $srcBranch = $srcBranch.ToUpper()
    if($isTag)
    {
        [string[]] $stableVersions = @("_PV_", "_ZY_", "_RD_")
        for ($j = 0; $j -lt ($stableVersions.length); $j++) {
            $content = $stableVersions[$j];
            if($srcBranch.Contains($content))
            {
                return "stable-tag"
            }
        }

        if($srcBranch.Contains("_TY_"))
        {
            return "hotfix-tag"
        }

        return "hotfix-branch"
    }

    if(!($isMaster)) { return "" }

    return "hotfix-branch"
}

function need-upload {
    param (
        [string] $srcBranch
    )

    $suffix = get-dest-suffix -srcBranch $srcBranch
    return -not ([string]::IsNullOrEmpty($suffix))
}

function github-upload {
    param (
        [string] $commitID,
        [string] $srcBranch,
        [string] $userAndRepo,
        [string] $token,
        [string] $existTagName,
        [string] $tagMessage,
        [string] $wrapPathName
    )
    # ensure root path.
    $oldPath = resolve-path ./
    
    $ignoreCase = 'CurrentCultureIgnoreCase'
    $suffix = get-dest-suffix -srcBranch $srcBranch
    $repoSuffix = ""
    $tagSuffix = ""
    $isTag = $srcBranch.StartsWith('refs/tags/', $ignoreCase) 
    switch ($suffix){
        'hotfix-branch'   
        {
            $tagSuffix = "branch"
            if($isTag) {
                $headBranchs = git branch -a --contains $commitID
                $tagByBranchs = $headBranchs.split("`n")    
                
                $hasMasterBranch = $false
                $solveBranchName = ""
                for ($j = 0; $j -lt ($tagByBranchs.length); $j++) {
                    $tempParts = $tagByBranchs[$j];
                    if($tempParts.Contains("detached at")){ continue }
                    $branchNames = $tempParts.split(' ')
                    $branchName = $branchNames[$branchNames.length - 1]
                    $branchNames = $branchName.split('/')
                    $branchName = $branchNames[$branchNames.length - 1]
                    if($branchName -eq "master") { $hasMasterBranch = $true }
                    elseif($solveBranchName -eq "") { $solveBranchName = $branchName }
                    elseif($hasMasterBranch) { break; }
                }
                if($hasMasterBranch -and ($solveBranchName -eq "")) {
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
        'hotfix-tag'
        {
            $repoSuffix = "hotfix"
            $tagSuffix = "tag"
        }
        'stable-tag' 
        {
            $repoSuffix = "stable"    
            $tagSuffix = "tag"
        }
        default 
        {
            echo "No resources need to be uploaded"
            return 
        }
    }

    $userAndRepo = $userAndRepo + "-" + $repoSuffix
    $repoName =  $userAndRepo.Split("/")[1]

    $changedZipName = "$existTagName"
    if($repoSuffix -eq "hotfix")
    {
        $timeValue = ((get-date).ToUniversalTime()).ToString("yyyyMMdd-HH-mm")
        $changedZipName = "$changedZipName-$timeValue"
    }

    Remove-Item $existTagName -Force -Recurse -ErrorAction SilentlyContinue
    rename-item "$wrapPathName" -newname "$changedZipName" -PassThru
    $assetses = "$changedZipName.zip"
    Compress-Archive -Path $changedZipName -DestinationPath $assetses

    git clone https://$token@github.com/$userAndRepo

    cd ./github-upload
    npm update

    cd ../
    cd ./$repoName

    git config --global user.email "test@arbin.com"
    git config --global user.name "arbin-test"

    # git tag -f $existTagName -m $tagMessage
    # git push --force origin :refs/tags/$existTagName

    node ../github-upload/main.js $srcBranch $userAndRepo $token $existTagName $tagMessage $assetses "$oldPath" $tagSuffix
}
