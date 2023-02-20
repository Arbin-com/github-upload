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

const args = process.argv;

let cmdOffset = 2;

const srcBranch = args[0 + cmdOffset];
const userAndRepo = args[1 + cmdOffset];
const token = args[2 + cmdOffset];
const existTagName = args[3 + cmdOffset];
const tagMessage = args[4 + cmdOffset];
const assetses = args[5 + cmdOffset];
const assetsePath = args[6 + cmdOffset].trim();


const octokit = new Octokit({
    auth: token
})


// const isUploadVersion = srcBranch.startsWith("refs/tags/".toLocaleLowerCase());
// const isMaster = srcBranch.startsWith("refs/heads/master".toLocaleLowerCase());
// const isDev = srcBranch.startsWith("refs/heads/dev".toLocaleLowerCase());
// const isUAT = srcBranch.startsWith("refs/heads/uat".toLocaleLowerCase());

// if (!isUploadVersion && (isMaster || isDev || isUAT)) {
//     return;
// }

// if (isUploadVersion) {
//     userAndRepo = userAndRepo + "-ver";
// }


let mainTask = (async () => {
    arrUserAndRepo = userAndRepo.split('/');
    let reposPrefix = `/repos/${userAndRepo}/releases`;

    await execCommand(`git tag -f ${existTagName} -m ${tagMessage}`)
    await execCommand(`git push --force origin :refs/tags/${existTagName}`).then(() => {
        console.log("update git tag end.");
    })

    let getTagResult = await octokit.request('GET ' + reposPrefix + '/tags/{tag}', {
        tag: existTagName
    }).catch((reason) => {
        console.log("check release failed:")
        console.log(reason)
        console.log("\n\n")
    })

    if (getTagResult !== undefined) {
        console.log("get tag result:")
        console.log(getTagResult)
        console.log("\n\n")
        getTagResult = getTagResult.data
    }

    let nowDate = new Date(Date.now()).toUTCString();
    let newReleaseData = {
        tag_name: existTagName,
        name: existTagName,
        body: `${nowDate}\n` + tagMessage,
        draft: false,
        prerelease: false
    }


    let newReleaseResult;
    if (getTagResult !== undefined && getTagResult.id !== undefined) {
        // let deleteTagResult = await octokit.request('DELETE ' + reposPrefix + '/{release_id}', {
        //     release_id: getTagResult.id
        // })
        // console.log("remove old tag result:")
        // console.log(deleteTagResult)
        // console.log("\n\n")

        let arrAssets = getTagResult.assets;
        let arrAssetsLen = arrAssets.length
        for (let i = 0; i < arrAssetsLen; i++) {
            await octokit.rest.repos.deleteReleaseAsset({
                owner: arrUserAndRepo[0],
                repo: arrUserAndRepo[1],
                asset_id: arrAssets[i].id,
            }).catch((reason) => {
                console.log("delete old assets failed:")
                console.log(reason)
                console.log("\n\n")
            })
        }

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

    let arrAssets = assetses.split(' ');

    const payloadUploadReleaseAsset = (id, dataName) => {
        let dataPath = dataName
        if (Boolean(assetsePath)) {
            dataPath = path.join(assetsePath, dataName)
        }
        let stream = fs.createReadStream(dataPath)
        return {
            owner: arrUserAndRepo[0],
            repo: arrUserAndRepo[1],
            release_id: id,
            contentLength: fs.statSync(dataPath).size,
            file: stream,
            name: dataName
        }
    };



    let arrAssetsLen = arrAssets.length
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

        const uploadAsset = await octokit.rest.repos.uploadReleaseAsset(
            payloadUploadReleaseAsset(newReleaseResult.id, assetsName)
        ).catch((reason) => {
            console.log(`upload assets ${assetsName} failed:`)
            console.log(reason)
            console.log("\n\n")
        })

        if (uploadAsset !== undefined) {
            console.log(`upload assets ${assetsName}:\n`)
            console.log(uploadAsset)
            console.log("\n\n")
        }
    }


})();
