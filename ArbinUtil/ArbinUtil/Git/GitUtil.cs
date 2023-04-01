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
            powerShell.Commands.Clear();
            if (!string.IsNullOrEmpty(filter))
            {
                powerShell.AddScript($"git tag -l {filter}");
            }
            else
            {
                powerShell.AddScript("git tag");
            }

            var result = powerShell.Invoke();
            powerShell.Commands.Clear();
            foreach (var item in result)
            {
                if (!ArbinVersion.Parse(item.ToString(), out ArbinVersion temp))
                    continue;
                yield return temp;
            }
        }

        public static string GetCurrentBranch(PowerShell powerShell)
        {
            powerShell.Commands.Clear();
            powerShell.AddScript($"git branch --show-current");
            var result = powerShell.Invoke();
            powerShell.Commands.Clear();
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
            powerShell.Commands.Clear();
            powerShell.AddScript("git symbolic-ref --short refs/remotes/origin/HEAD");
            var result = powerShell.Invoke();
            powerShell.Commands.Clear();
            if (result.Count == 0)
                return "";
            return result[0].ToString().Replace("origin/", "").Trim(TrimSpaces);
        }

        public static string GetBranch(PowerShell powershell, ArbinVersion findVersion)
        {
            powershell.Commands.Clear();
            powershell.AddScript($"git branch --contains tags/{findVersion}");
            var result = powershell.Invoke();
            powershell.Commands.Clear();
            if (result.Count == 0)
                return "";
            return result[0].ToString().Replace("origin/", "").Trim(TrimSpaces);
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

        public static bool CheckBranchTextNeedStop(string line, ArbinVersion referenceVersion, bool ignoreEqualPathPrefix)
        {
            string tagText = "tag: ";
            bool isNormalVersion = referenceVersion.IsNormalVersion;
            if (line.IndexOf(referenceVersion.ToString(false, false)) != -1)
            {
                return false;
            }

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
                    if (!arbinVersion.SameSuffix(referenceVersion.Suffix))
                        continue;
                    if (!ignoreEqualPathPrefix && !arbinVersion.SamePathPrefix(referenceVersion.PathPrefix))
                        continue;
                    if (isNormalVersion)
                    {
                        if (arbinVersion.Build != 0)
                            continue;
                    }
                    return true;
                }
                else
                {

                }
            }
            return false;
        }



    }
}
