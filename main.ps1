

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
    $isUploadVersion = $srcBranch.StartsWith('refs/tags/', $ignoreCase) 
    $isMaster = $srcBranch.StartsWith('refs/heads/master', $ignoreCase) #only check prefix
    $isDev = $srcBranch.StartsWith('refs/heads/dev', $ignoreCase)
    $isUAT = $srcBranch.StartsWith('refs/heads/uat', $ignoreCase)

    if(!($isUploadVersion) -and ($isMaster -or $isDev -or $isUAT))
    {
        echo "No resources need to be uploaded"
        return
    }

    if($isUploadVersion)
    {
        $userAndRepo = $userAndRepo + "-version"
    }
    else   
    {
        $userAndRepo = $userAndRepo + "-test"
    }

    $repoName =  $userAndRepo.Split("/")[1]

    git clone https://$token@github.com/$userAndRepo
    dir

    cd ./github-upload
    npm update

    cd ../
    cd ./$repoName

    git config --global user.email "test@arbin.com"
    git config --global user.name "arbin-test"

    git tag -f $existTagName -m $tagMessage
    git push --force origin :refs/tags/$existTagName

    node ../github-upload/main.js $srcBranch $userAndRepo $token $existTagName $tagMessage $assetses "$oldPath"
}