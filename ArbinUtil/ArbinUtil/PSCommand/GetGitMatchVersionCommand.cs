/*
* ==============================================================================
* Filename: GetGitPrevVersionCommand
* Description: 
* 
* Version: 1.0
* Created: 2023-03-31 21:13:04
*
* Author: RuiSen
* ==============================================================================
*/

using ArbinUtil.Git;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Transactions;
using static System.Net.Mime.MediaTypeNames;

namespace ArbinUtil.PSCommand
{
    [Cmdlet(VerbsCommon.Get, "GitMatchVersion")]
    [OutputType(typeof(MatchVersion))]
    public class GetGitMatchVersionCommand : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public string ReferenceVersion { get; set; }
        [Parameter(Mandatory = true)]
        public string Owner { get; set; }
        [Parameter(Mandatory = true)]
        public string Repo { get; set; }
        [Parameter(Mandatory = true)]
        public string Token { get; set; }

        [Parameter()]
        public string RelativePath { get; set; } = "";

        [Parameter()]
        public string StoreReleasePath { get; set; } = "";

        [Parameter(HelpMessage = "example: '{0}', 'prefix.{0}'")]
        public string TagFormat { get; set; } = "{0}";

        private bool m_isArbinVersion = false;
        private ArbinVersion m_referenceVersion;

        private List<uint> GetNumberPath(string basePath, uint value)
        {
            List<uint> result = new List<uint>();
            bool isAny = value == ArbinVersion.AnyNumber;
            var dirs = Directory.EnumerateDirectories(basePath);

            if(isAny)
            {
                foreach(string dir in dirs)
                {
                    if(!uint.TryParse(Path.GetFileName(dir), out uint dirNumber))
                        continue;
                    result.Add(dirNumber);
                }
            }
            else
            {
                string temp = Path.Combine(basePath, value.ToString());
                if(Directory.Exists(temp))
                {
                    result.Add(value);
                }
            }
            result.Sort((x, y) => -x.CompareTo(y));
            return result;
        }

        private bool VersionMatch(ArbinVersion version)
        {
            if(m_referenceVersion.Major != ArbinVersion.AnyNumber && m_referenceVersion.Major != version.Major)
                return false;
            if(m_referenceVersion.Minor != ArbinVersion.AnyNumber && m_referenceVersion.Minor != version.Minor)
                return false;
            if(m_referenceVersion.Build != ArbinVersion.AnyNumber && m_referenceVersion.Build != version.Build)
                return false;
            if(!m_referenceVersion.SameSuffix(version.Suffix))
                return false;
            if(m_referenceVersion.SpecialNumber != ArbinVersion.AnyNumber && m_referenceVersion.SpecialNumber != version.SpecialNumber)
                return false;
            return true;
        }

        private string GetMatchVersionFileName(string basePath)
        {
            ArbinVersion find = null;
            string path = "";
            foreach(var file in Directory.EnumerateFileSystemEntries(basePath))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if(!ArbinVersion.Parse(fileName, out ArbinVersion temp))
                    continue;
                if(!VersionMatch(temp))
                    continue;
                if(find == null || Util.AzurePiplineGoodVersionCompareTo(temp, find) >= 0)
                {
                    find = temp;
                    path = file;
                }
            }
            return path;
        }

        private string GetPath(string basePath)
        {
            var nexts = GetNumberPath(basePath, m_referenceVersion.Major);
            foreach(var major in nexts)
            {
                string path = Path.Combine(basePath, major.ToString());
                var minors = GetNumberPath(path, m_referenceVersion.Minor);
                foreach(var minor in minors)
                {
                    string filePath = Path.Combine(path, minor.ToString());
                    string fullFilePath = GetMatchVersionFileName(filePath);
                    if(!string.IsNullOrEmpty(fullFilePath))
                        return fullFilePath;
                }
            }
            return basePath;
        }

        private CodeData GetCodeData(string filePath)
        {
            CodeData result = new CodeData();
            string text = File.ReadAllText(filePath);
            Match match = new Regex(@"(?<=```c)([\s\S]*)(?=```)", RegexOptions.Multiline).Match(text);
            if(!match.Success || match.Groups.Count <= 0)
                return result;
            string jsonText = match.Groups[0].Value;
            if(string.IsNullOrWhiteSpace(jsonText))
                return result;
            result = JsonSerializer.Deserialize<CodeData>(jsonText);
            return result;
        }

        private MatchVersion DoWork()
        {
            const int MaxCount = 3;
            int counter = 0;
            while(true)
            {
                try
                {
                    return DoWorkCore();
                }
                catch(Exception e)
                {
                    if(counter++ > MaxCount)
                        throw e;
                    WriteVerbose(e.ToString());
                }
                Thread.Sleep(30 * 1000);
            }
        }

        private MatchVersion DoWorkCore()
        {
            MatchVersion result = new MatchVersion();
            string basePath = Path.Combine(SessionState.Path.CurrentFileSystemLocation.Path, RelativePath);
            string filePath = basePath;
            string tag = null;
            if(m_isArbinVersion)
            {
                filePath = GetPath(basePath);
            }
            else
            {
                filePath = Path.Combine(basePath, ReferenceVersion + ".md");
                tag = ReferenceVersion;
            }
            if(string.IsNullOrEmpty(filePath))
                return result;
            WriteVerbose($"try get file: {filePath}");
            result.CodeData = GetCodeData(filePath);
            string findVersion = Path.GetFileNameWithoutExtension(filePath);
            result.Version = findVersion;
            if(string.IsNullOrEmpty(tag))
            {
                tag = string.Format(TagFormat, findVersion);
            }
            WriteVerbose($"GetReleaseByTag: '{tag}'");
            var releaseResult = GitUtil.GetReleaseByTag(Owner, Repo, Token, tag);
            if(!releaseResult.TryGetPropertyValue("assets", out JsonNode node) || !(node is JsonArray arr))
                return result;
            List<string> storePaths = new List<string>();
            foreach(var assets in arr)
            {
                string url = assets["browser_download_url"].GetValue<string>();
                string id = assets["id"].ToString();
                string fileName = Path.GetFileName(url);
                string storePath = Path.Combine(StoreReleasePath, fileName);
                GitUtil.DownloadByGithub(Owner, Repo, Token, id, storePath);
                storePaths.Add(storePath);
            }
            result.ReceivePath = storePaths.ToArray();
            return result;
        }

        protected override void ProcessRecord()
        {
            List<ArbinVersion> prevs = new List<ArbinVersion>();
            GitCommitVersion commitVersion = new GitCommitVersion();
            commitVersion.Previous = prevs;
            using(Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                m_isArbinVersion = ArbinVersion.Parse(ReferenceVersion, out m_referenceVersion);
                runspace.SessionStateProxy.Path.SetLocation(SessionState.Path.CurrentFileSystemLocation.Path);
                using(PowerShell powershell = PowerShell.Create())
                {
                    powershell.Runspace = runspace;
                    var result = DoWork();
                    WriteObject(result);
                }
                runspace.Close();
            }
        }

    }

}
