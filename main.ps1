
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
    remove-item $binPath/empty.txt -ErrorAction SilentlyContinue
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
        [string[]] $assetses
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