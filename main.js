const fs = require('fs');
const { exec } = require("child_process");
const { Octokit, App } = require("octokit");
const { stringify } = require('querystring');
const path = require('path');


const execCommand = (cmd) => {
    return new Promise((resolve, reject) => {
        exec(cmd, (error, stdout, stderr) => {
            if (error) {
                reject(error);
                return;
            }
            resolve(stdout)
        })
    }).then(res => {
        console.log("exec cmd:", res);
    }).catch(err => {
        console.log("exec cmd catch: ", err);
    });
}


const wait = (ms) => {
    return new Promise(resolve => setTimeout(() => resolve(), ms));
};

const args = process.argv;
let cmdOffset = 2;

const srcBranch = args[0 + cmdOffset];
const userAndRepo = args[1 + cmdOffset];
const token = args[2 + cmdOffset];
const existTagName = args[3 + cmdOffset];
const tagMessage = args[4 + cmdOffset];
const assetses = args[5 + cmdOffset];
const assetsePath = args[6 + cmdOffset].trim();
const tagSuffix = args[7 + cmdOffset].trim();

const octokit = new Octokit({
    auth: token
})


let mainTask = (async () => {
    arrUserAndRepo = userAndRepo.split('/');
    let reposPrefix = `/repos/${userAndRepo}/releases`;
    let arrAssets = assetses.split(' ');
    let arrAssetsLen = arrAssets.length
    const assetMap = new Map();
    for (let i = 0; i < arrAssetsLen; i++) {
        assetMap[arrAssets[i]] = "";
    }

    let realTagName = existTagName + (tagSuffix === "" ? "" : `-${tagSuffix}`)
    let getTagResult = await octokit.request('GET ' + reposPrefix + '/tags/{tag}', {
        tag: realTagName
    }).catch((reason) => {
        console.log("check release failed:")
        console.log(reason)
        console.log("\n\n")
    })

    let emojiText = "🚀";
    let isHotfix = userAndRepo.endsWith("hotfix")
    if (isHotfix && tagSuffix === "branch" && existTagName !== "master") {
        emojiText = "🐛"
    }

    let textBody = ""
    if (getTagResult !== undefined) {
        console.log("get tag result:")
        console.log(getTagResult)
        console.log("\n\n")
        getTagResult = getTagResult.data

        let oldArrAssets = getTagResult.assets;
        let oldArrAssetsLen = oldArrAssets.length

        console.log(`check delete assets length: ${oldArrAssetsLen}\n\n`)
        for (let i = 0; i < oldArrAssetsLen; i++) {
            let oldAssetsData = oldArrAssets[i]
            if (!isHotfix && assetMap[oldAssetsData.name] !== "")
                continue;

            console.log(`delete assets ${oldAssetsData.name}`)
            await octokit.rest.repos.deleteReleaseAsset({
                owner: arrUserAndRepo[0],
                repo: arrUserAndRepo[1],
                asset_id: oldAssetsData.id,
            }).catch((reason) => {
                console.log("delete old assets failed:")
                console.log(reason)
                console.log("\n\n")
            })
        }

        let getTagResultBody = getTagResult.body
        if (getTagResultBody !== "" && getTagResultBody !== undefined && !getTagResultBody.startsWith("name by ")) {
            textBody = getTagResultBody
        }
    }
    else {
        await execCommand(`git tag -f ${realTagName} -m "${tagMessage}"`)
        await execCommand(`git push --force origin refs/tags/${realTagName}`).then(() => {
            console.log("update git tag end.");
        })
    }

    let nowDate = new Date(Date.now()).toUTCString()
    emojiText = emojiText.repeat(3)
    if (textBody === "" || textBody.startsWith(emojiText)) {
        textBody = `${emojiText} ${nowDate}\n` + tagMessage + `\n`
    }


    let newReleaseData = {
        tag_name: realTagName,
        name: existTagName,
        body: textBody,
        draft: false,
        prerelease: true
    }

    let newReleaseResult;
    if (getTagResult !== undefined && getTagResult.id !== undefined) {
        // let deleteTagResult = await octokit.request('DELETE ' + reposPrefix + '/{release_id}', {
        //     release_id: getTagResult.id
        // })
        // console.log("remove old tag result:")
        // console.log(deleteTagResult)
        // console.log("\n\n")
        newReleaseResult = await octokit.request('PATCH ' + reposPrefix + '/{release_id}', Object.assign(newReleaseData, {
            release_id: getTagResult.id
        }))
    }
    else {
        newReleaseResult = await octokit.request('POST ' + reposPrefix,
            Object.assign(newReleaseData, {
                generate_release_notes: false
            })).catch((reason) => {
                console.log("new release failed:")
                console.log(reason)
                console.log("\n\n")
            })
    }


    console.log("new release:")
    console.log(newReleaseResult)
    console.log("\n\n")

    newReleaseResult = newReleaseResult.data

    const payloadUploadReleaseAsset = (id, dataName) => {
        let dataPath = dataName
        if (Boolean(assetsePath)) {
            dataPath = path.join(assetsePath, dataName)
        }
        let dataSize = fs.statSync(dataPath).size
        console.log(`before init upload asset ${dataPath}, Size:${dataSize}`)
        return {
            headers: {
                'content-type': 'application/zip',
                'content-length': dataSize,
            },
            owner: arrUserAndRepo[0],
            repo: arrUserAndRepo[1],
            release_id: id,
            data: fs.createReadStream(dataPath),
            name: dataName,
        }
    };
    console.log("start upload asset")

    for (let i = 0; i < arrAssetsLen; i++) {
        let assetsName = arrAssets[i];
        // let uploadAssets = await octokit.request('POST ' + reposPrefix + '{release_id}/assets{?name}', {
        //     release_id: newReleaseResult.id,
        //     data: '@' + assetsName,
        //     name: assetsName
        // }).catch((reason) => {
        //     console.log(`upload assets ${assetsName} failed:`)
        //     console.log(reason)
        //     console.log("\n\n")
        // })
        let tryCount = 3
        while (true) {
            let uploadSuccess = true
            const uploadAsset = await octokit.rest.repos.uploadReleaseAsset(
                payloadUploadReleaseAsset(newReleaseResult.id, assetsName)
            ).catch((reason) => {
                uploadSuccess = false
                console.log(`upload assets ${assetsName} failed:`)
                console.log(reason)
                console.log("\n\n")
            })

            if (uploadAsset !== undefined) {
                console.log(`upload assets ${assetsName}:\n`)
                console.log(uploadAsset)
                console.log("\n\n")
            }

            if (uploadSuccess == true) {
                break;
            }
            --tryCount
            if (tryCount <= 0) {
                throw new Error(`upload assets ${assetsName} failed`);
            }
            await wait(10 * 1000);
        }

    }


})();
