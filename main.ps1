
function get-dest-suffix {
    param (
        [string] $srcBranch
    )

    $ignoreCase = 'CurrentCultureIgnoreCase'
    $isTag = $srcBranch.StartsWith('refs/tags/', $ignoreCase) 
    $isMaster = $srcBranch.StartsWith('refs/heads/master', $ignoreCase) #only check prefix

    if($isTag)
    {
        [string[]] $stableVersions = @("_PV_", "_ZY_", "_RD_")
        for ($j = 0; $j -lt ($stableVersions.length); $j++) 
        {
            $content = $stableVersions[$j];
            if($srcBranch.Contains($content, $ignoreCase))
            {
                return "stable-tag"
            }
        }

        if($srcBranch.Contains("_TY_", $ignoreCase))
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
    
    $suffix = get-dest-suffix -srcBranch $srcBranch
    $repoSuffix = ""
    switch ($suffix){
        'hotfix-branch'   
        {
            $existTagName = git rev-parse --abbrev-ref HEAD
            $repoSuffix = "hotfix"
        }
        'hotfix-tag'
        {
            $repoSuffix = "hotfix"
        }
        'stable-tag' 
        {
            $repoSuffix = "stable"    
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

    node ../github-upload/main.js $srcBranch $userAndRepo $token $existTagName $tagMessage $assetses "$oldPath"
}