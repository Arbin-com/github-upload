using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.IO;

namespace ArbinUtil.Git
{
    public static class GitUtil
    {
        public const int DefaultSearchLogCount = 100000;

        public static char[] EndChars = new char[] { '*', '(', ' ', ')', ',' };
        private static char[] TrimSpaces = new char[] { ' ', '*' };

        public static bool IsEnd(char ch)
        {
            int index = Array.FindIndex(EndChars, (c) => c == ch);
            return index != -1;
        }

        #region http


        public static void DownloadByGithub(string owner, string repo, string token, string assetID, string fullPath)
        {
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(new HttpMethod("GET"), $"https://api.github.com/repos/{owner}/{repo}/releases/assets/{assetID}"))
                {
                    request.Headers.UserAgent.TryParseAdd("request");
                    request.Headers.TryAddWithoutValidation("Accept", "application/octet-stream");
                    request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = httpClient.SendAsync(request).Result;
                    response.EnsureSuccessStatusCode();
                    using (var sm = response.Content.ReadAsStreamAsync().Result)
                    {
                        using (var fs = File.Create(fullPath))
                        {
                            sm.CopyTo(fs);
                        }
                    }
                }
            }
        }

        public static JsonObject GetReleaseByTag(string owner, string repo, string token, string tag)
        {
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(new HttpMethod("GET"), $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}"))
                {
                    request.Headers.UserAgent.TryParseAdd("request");
                    request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
                    request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var response = httpClient.SendAsync(request).Result;
                    response.EnsureSuccessStatusCode();
                    var content = response.Content;
                    return JsonNode.Parse(content.ReadAsStringAsync().Result).AsObject();
                }
            }
        }


        #endregion

        public static IEnumerable<ArbinVersion> FindAllVersions(PowerShell powerShell, string filter = "")
        {
            string script;
            if (!string.IsNullOrEmpty(filter))
            {
                script = $"git tag -l {filter}";
            }
            else
            {
                script = "git tag";
            }
            var result = powerShell.ExecOneScript(script);
            foreach (var item in result)
            {
                if (!ArbinVersion.Parse(item.ToString(), out ArbinVersion temp))
                    continue;
                yield return temp;
            }
        }

        public static string GetCurrentBranch(PowerShell powerShell)
        {
            var result = powerShell.ExecOneScript("git branch --show-current");
            if (result.Count == 0)
                return "";
            return result[0].ToString().Trim();
        }

        public static ArbinVersion GetPrevVersion(PowerShell powerShell, ArbinVersion referenceVersion)
        {
            ArbinVersion findVersion = null;
            foreach (var version in GitUtil.FindAllVersions(powerShell))
            {
                if (Util.AzurePiplineGoodVersionCompareTo(version, referenceVersion) >= 0)
                    continue;
                if(!version.SameSuffix(referenceVersion.Suffix))
                    continue;
                if (findVersion == null)
                    findVersion = version;
                else if (Util.AzurePiplineGoodVersionCompareTo(version, findVersion) > 0)
                {
                    findVersion = version;
                }
            }
            return findVersion;
        }

        public static string GetDefaultBranch(PowerShell powerShell)
        {
            //var result = powerShell.ExecOneScript("git symbolic-ref --short refs/remotes/origin/HEAD");
            var result = powerShell.ExecOneScript("git rev-parse --abbrev-ref origin/HEAD");
            if (result.Count == 0)
                return "master";
            return result[0].ToString().Replace("origin/", "").Trim(TrimSpaces);
        }

        private static string RemovePrefixBranchName(string branch)
        {
            if(string.IsNullOrEmpty(branch))
                return branch;
            branch = branch.Trim(TrimSpaces);
            return branch;
            //const string remote = "remotes/";
            //if(branch.StartsWith(remote))
            //{
            //    branch = branch[remote.Length..];
            //}
            //const string origin = "origin/";
            //if(branch.StartsWith(origin))
            //{
            //    branch = branch[origin.Length..];
            //}
            //return branch;
        }

        public static string GetCommitByTag(PowerShell powerShell, string tag)
        {
            var result = powerShell.ExecOneScript($"git rev-list -1 {tag}");
            if(result.Count == 0)
                return "";
            string select = result[0].ToString();
            return select;
        }

        public static string GetBranch(PowerShell powerShell, ArbinVersion findVersion)
        {
            var result = powerShell.ExecOneScript($"git branch -a --contains tags/{findVersion}");
            if (result.Count == 0)
                return "";
            string select = result[0].ToString();
            return RemovePrefixBranchName(select);
        }

        public static string GetBaseCommit(PowerShell powershell, string branch1, string branch2)
        {
            powershell.AddScript($"git merge-base {branch1} {branch2}");
            var result = powershell.Invoke();
            if (result.Count == 0)
                return "";
            powershell.Commands.Clear();
            return result[0].ToString().Trim();
        }

        public static (bool, string) CheckBranchTextNeedStop(string line, ArbinVersion referenceVersion, bool isStableOrPatch, bool ignoreEqualPathPrefix)
        {
            string tagText = "tag: ";
            bool isNormalVersion = referenceVersion.IsNormalVersion;

            foreach (string block in line.Split(','))
            {
                int index = block.IndexOf(tagText);
                bool findTag = index != -1;

                string trim = "";
                int start = block.LastIndexOf(' ');
                int len = block.Length;
                if (start != -1 && start + 1 < len)
                {
                    trim = block[(start + 1)..(block[len - 1] == ')' ? len - 1 : len)];
                }
                trim = trim.Trim();
                string version = trim;
                if (!ArbinVersion.Parse(version, out ArbinVersion arbinVersion))
                    continue;
                if (findTag)
                {
                    if(isStableOrPatch)
                    {
                        if(!arbinVersion.IsPatchVersion && !arbinVersion.IsStableVersion)
                            continue;
                        if (Util.MaxMajorMinorBuild(arbinVersion, referenceVersion) >= 0)
                            continue;
                    }
                    else
                    {
                        if (!arbinVersion.SameSuffix(referenceVersion.Suffix))
                            continue;
                        if (!ignoreEqualPathPrefix && !arbinVersion.SamePathPrefix(referenceVersion.PathPrefix))
                            continue;
                        if (Util.MaxMajorMinorBuild(arbinVersion, referenceVersion) >= 0)
                            continue;
                        if (isNormalVersion)
                        {
                            if (arbinVersion.Build != 0)
                                continue;
                        }
                    }
                    return (true, version);
                }
                else
                {

                }
            }
            return (false, "");
        }



    }
}
