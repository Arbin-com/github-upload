
function dotnet-remove-other-file {
    param (
        [string] $binPath
    )

    [string[]] $removePaths = @("de", "es", "ja", "ru")
    for ($j = 0; $j -lt ($removePaths.length); $j++) {
        $pathName = $removePaths[$j];
        $remotePath = "$binPath/$pathName"
        Remove-Item $remotePath -Force -Recurse -ErrorAction SilentlyContinue
    }
    remove-item $binPath/DevExpress*.xml
    remove-item $binPath/*.pdb
    remove-item $binPath/empty.txt -ErrorAction SilentlyContinue
}

$global:commit_log_file_default_number = "1000"

function add-commit-log-file {
        param (
        [string] $fullPath,
        [string] $maxNumber
    )

    if(!([string]::IsNullOrEmpty($suffix)))
    {
        $maxNumber = "-n $maxNumber"
    }
    else
    {
        $maxNumber = "-n $global:commit_log_file_default_number"
    }

    git log $maxNumber --date=format-local:'%Y-%m-%d %H:%M:%S' --pretty=format:'%ad <%ce> %s' > $fullPath
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
        for ($j = 0; $j -lt ($stableVersions.length); $j++) 
        {
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

function get-release

function need-upload {
    param (
        [string] $srcBranch
    )

    $suffix = get-dest-suffix -srcBranch $srcBranch
    return -not ([string]::IsNullOrEmpty($suffix))
}

function github-upload {
    param (
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
            $headBranch = git rev-parse --abbrev-ref HEAD
            if($headBranch -eq "master" -and $isTag) {
                $tagSuffix = "tag"
            }
            else {
                $existTagName = $headBranch
                $tagSuffix = "branch"
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

    Remove-Item $existTagName -Force -Recurse -ErrorAction SilentlyContinue
    rename-item "$wrapPathName" -newname "$existTagName" -PassThru
    $assetses = "$existTagName.zip"
    Compress-Archive -Path $existTagName -DestinationPath $assetses

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