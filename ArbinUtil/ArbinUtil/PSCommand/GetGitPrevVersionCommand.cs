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
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;

namespace ArbinUtil.PSCommand
{
    [Cmdlet(VerbsCommon.Get, "GitPrevVersion")]
    [OutputType(typeof(GitCommitVersion))]
    public class GetGitPrevVersionCommand : PSCmdlet
    {
        [Parameter()]
        public int SearchCommitCount { get; set; } = 1000;

        [Parameter()]
        public string IgnorePrevVersionRegex { get; set; } = @".*\..*\.0.*";

        [Parameter()]
        public string EqualSuffix { get; set; } = "";

        [Parameter()]
        public string EqualPathPrefix { get; set; } = "";

        [Parameter()]
        public bool IgnoreEqualPathPrefix { get; set; } = true;

        [Parameter()]
        public int EqualSpecialNumber { get; set; } = 0;

        [Parameter()]
        public int GetPrevVersionCount { get; set; } = 10;

        private ArbinVersion Max(ArbinVersion a, ArbinVersion b)
        {
            if (a.MaxMajorMinorBuild(b) >= 0)
                return a;
            else
                return b;
        }

        private void FindMaxVersion(string line, GitCommitVersion commitVersion)
        {
            int commitSplitIndex = line.IndexOf(' ');
            if (commitSplitIndex >= line.Length)
                return;

            var span = line.AsSpan().Slice(commitSplitIndex + 1);

            ArbinVersion oldVersion = commitVersion.Version;
            ArbinVersion temp = oldVersion;
            int index;
            ReadOnlySpan<char> tagText = "tag: ";
            Regex regex = string.IsNullOrEmpty(IgnorePrevVersionRegex) ? null : new Regex(IgnorePrevVersionRegex);
            while (true)
            {
                index = span.IndexOf(tagText);
                if (index == -1)
                    break;
                index += tagText.Length;
                if (index >= span.Length)
                    break;
                int find = index;
                while (find < span.Length && !GitUtil.IsEnd(span[find]))
                    ++find;
                string version = span.Slice(index, find - index).ToString();
                span = span.Slice(find);
                if (!ArbinVersion.Parse(version, out ArbinVersion arbinVersion))
                    continue;
                if (arbinVersion.SpecialNumber != EqualSpecialNumber || !arbinVersion.SameSuffix(EqualSuffix))
                    continue;
                if (!IgnoreEqualPathPrefix && !arbinVersion.SamePathPrefix(EqualPathPrefix))
                    continue;
                bool ignore = false;
                if (regex != null)
                {
                    ignore = regex.Match(version).Success;
                }
                if (!ignore)
                    commitVersion.Previous.Add(arbinVersion);
                if (temp == null)
                    temp = arbinVersion;
                else
                    temp = Max(temp, arbinVersion);
            }
            if (temp == null)
                return;

            if (temp != oldVersion)
            {
                commitVersion.Version = temp;
                commitVersion.Commit = line.Substring(0, commitSplitIndex);
            }
        }

        protected override void ProcessRecord()
        {
            List<ArbinVersion> prevs = new List<ArbinVersion>();
            GitCommitVersion commitVersion = new GitCommitVersion();
            commitVersion.Previous = prevs;
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                runspace.SessionStateProxy.Path.SetLocation(SessionState.Path.CurrentFileSystemLocation.Path);
                using (PowerShell powershell = PowerShell.Create())
                {
                    powershell.Runspace = runspace;
                    powershell.AddScript($"git log --tags --pretty=\"%h %d\" -n {SearchCommitCount}");
                    foreach (PSObject result in powershell.Invoke())
                    {
                        FindMaxVersion(result.ToString(), commitVersion);
                        if (prevs.Count >= GetPrevVersionCount)
                            break;
                    }
                }
                runspace.Close();
            }
            if (commitVersion.Version != null)
            {
                commitVersion.Previous.Sort((x, y) => y.MaxMajorMinorBuild(x));
                WriteObject(commitVersion);
            }
        }

    }

}
