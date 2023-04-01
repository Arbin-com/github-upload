/*
* ==============================================================================
* Filename: GitGetLogJiraIssueKeyCommand
* Description: 
*
* Version: 1.0
* Created: 2023/4/14 10:51:26
*
* Author: RuiSen
* ==============================================================================
*/

using ArbinUtil.Algorithm;
using ArbinUtil.Git;
using ArbinUtil.Jira;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Text.RegularExpressions;

namespace ArbinUtil.PSCommand
{
    [Cmdlet(VerbsCommon.Get, "GitGetLogJiraIssueKey")]
    [OutputType(typeof(string))]
    public class GitGetLogJiraIssueKeyCommand : PSCmdlet
    {
        [Parameter()]
        public int SearchCommitCount { get; set; } = GitUtil.DefaultSearchLogCount;

        [Parameter(Mandatory = true)]
        public ArbinVersion ReferenceVersion { get; set; }

        [Parameter()]
        public bool IgnoreEqualPathPrefix { get; set; } = true;

        [Parameter()]
        public string StopCommit { get; set; } = "";

        [Parameter()]
        public string[] JiraIssueKeyPrefixs { get; set; }

        [Parameter(Mandatory = true)]
        public string UserName { get; set; }

        [Parameter(Mandatory = true)]
        public string APIToken { get; set; }

        [Parameter(Mandatory = true)]
        public string JiraHostURL { get; set; }

        public bool CurrentBranchIsMaster { get; set; } = false;

        private string m_defaultBranch = "";
        private StringFind m_search;
        private Dictionary<string, SortedSet<uint>> m_searchAllKeys = new Dictionary<string, SortedSet<uint>>();

        private void LoadBranch(PowerShell powershell)
        {
            m_defaultBranch = GitUtil.GetDefaultBranch(powershell);
            if (string.IsNullOrEmpty(m_defaultBranch))
                return;

            if (!string.IsNullOrEmpty(StopCommit))
                return;

            bool isstableVersion = ReferenceVersion.IsStableVersion;
            if (!isstableVersion)
                return;

            ArbinVersion findVersion = GitUtil.GetPrevVersion(powershell, ReferenceVersion);
            if (findVersion == null)
                return;

            string prevBranch = GitUtil.GetBranch(powershell, findVersion);
            if (string.IsNullOrEmpty(prevBranch))
                return;

            StopCommit = GitUtil.GetBaseCommit(powershell, m_defaultBranch, prevBranch);
        }

        private void BuildSearchIssueKeyPreifxs()
        {
            if(JiraIssueKeyPrefixs == null || JiraIssueKeyPrefixs.Length == 0)
            {
                string auth = JiraUtil.CreateJiraAuthorization(UserName, APIToken);
                var array = JiraUtil.GetJiraAllProjects(JiraHostURL, auth).Result.AsArray();
                JiraIssueKeyPrefixs = array.Select(x => x["key"].GetValue<string>()).ToArray();
            }
            StringFind.StringFindBuilder builder = new StringFind.StringFindBuilder();
            foreach(var prefix in JiraIssueKeyPrefixs)
            {
                builder.AddString($"{prefix.ToUpper()}-");
            }
            m_search = builder.Build();
        }

        private void AddJiraKey(string text, int keyPrefixIndex, int len)
        {
            int textLength = text.Length;
            int beginNumberIndex = keyPrefixIndex + len;
            int lastnumberIndex = beginNumberIndex;
            while (lastnumberIndex < textLength && char.IsDigit(text[lastnumberIndex]))
            {
                ++lastnumberIndex;
            }

            if(beginNumberIndex >= lastnumberIndex)
                return;
            if (!uint.TryParse(text.AsSpan()[beginNumberIndex..lastnumberIndex], out uint number))
                return;

            string prefix = text.Substring(keyPrefixIndex, len - 1);
            if(!m_searchAllKeys.TryGetValue(prefix, out SortedSet<uint> numbers))
            {
                numbers = new SortedSet<uint>();
                m_searchAllKeys[prefix] = numbers;
            }
            numbers.Add(number);
        }

        internal class GetJiraIssueKeyLogMessageView : PSGitLogView
        {
            public GitGetLogJiraIssueKeyCommand Cmd { get; }
            private bool m_firstMessage = true;

            public GetJiraIssueKeyLogMessageView(GitGetLogJiraIssueKeyCommand cmd)
            {
                Cmd = cmd;
            }

            protected override void SectionEnterCommit()
            {
                
            }

            protected override void SectionEnterBranch()
            {
                
            }

            protected override void SectionEnterMessage()
            {
                
            }

            protected override void SectionEnterEnd()
            {
                
            }

            private void ExecMessage(string text)
            {
                if (!m_firstMessage)
                    return;

                text = text.Trim();
                if (string.IsNullOrEmpty(text))
                    return;
                m_firstMessage = true;

                Cmd.m_search.Search(text, new StringFind.SearchTextDelegate((parseText, index, len) => {
                    Cmd.AddJiraKey(parseText, index, len);
                }));
            }

            protected override void ParseText(string text)
            {
                switch (Section)
                {
                case TextSection.Commit:
                {
                    if (!string.IsNullOrEmpty(Cmd.StopCommit) && Cmd.StopCommit.StartsWith(text))
                    {
                        m_stopParse = true;
                    }
                }
                break;
                case TextSection.Branch:
                {
                    if(GitUtil.CheckBranchTextNeedStop(text, Cmd.ReferenceVersion, Cmd.IgnoreEqualPathPrefix))
                    {
                        m_stopParse = true;
                    }
                }
                break;
                case TextSection.Message:
                {
                    ExecMessage(text);
                }
                break;
                case TextSection.End:
                {
                    m_firstMessage = true;
                }
                break;
                }
            }
        }

        protected override void ProcessRecord()
        {
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                runspace.SessionStateProxy.Path.SetLocation(SessionState.Path.CurrentFileSystemLocation.Path);
                using (PowerShell powershell = PowerShell.Create())
                {
                    powershell.Runspace = runspace;
                    LoadBranch(powershell);
                    BuildSearchIssueKeyPreifxs();
                    string branch = GitUtil.GetCurrentBranch(powershell);
                    CurrentBranchIsMaster = branch.Equals(m_defaultBranch, StringComparison.OrdinalIgnoreCase);
                    var view = new GetJiraIssueKeyLogMessageView(this);
                    view.ParseLogByPowerShell(powershell, SearchCommitCount);

                    var result = m_searchAllKeys.Select(x => new JiraIssueKeys() { Name = x.Key, Numbers = x.Value.ToArray() }).ToArray();
                    WriteObject(result);
                }

                runspace.Close();
            }


        }

    }
}
