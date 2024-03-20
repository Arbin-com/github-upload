

class UploadModiftyNotify {
    [string] GetZipFile([string]$zipPrefix, [string]$branchFullName, [string[]]$partTags) {
        return $zipPrefix + $partTags[$partTags.length - 1];
    }

    [string] GetZipSuffix([string]$zipFileName) {
        return "zip"
    }

    [string] GetProjectPrefixPath([string]$setPrefix, [string[]]$partTags) {
        return $setPrefix;
    }


}