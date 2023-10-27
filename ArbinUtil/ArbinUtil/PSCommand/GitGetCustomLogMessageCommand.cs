/*
* ==============================================================================
* Filename: GitGetCustomLogMessageCommand
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Text.RegularExpressions;

namespace ArbinUtil.PSCommand
{
    [Cmdlet(VerbsCommon.Get, "GitGetCustomLogMessage")]
    [OutputType(typeof(string))]
    public class GitGetCustomLogMessageCommand : PSCmdlet
    {
        private static Regex RegexReporter = new Regex("(?i)(?<=\\(+.*)[,\\s]*Reporter:.*(?=\\))");

        const string BugfixName = "Bug fix";
        const string NewFeatureName = "New features";
        const string StyleName = "Style";

        const string MessageTypePrefix = "--";

        [Parameter()]
        public int SearchCommitCount { get; set; } = GitUtil.DefaultSearchLogCount;

        [Parameter(Mandatory = true)]
        public ArbinVersion ReferenceVersion { get; set; }

        [Parameter()]
        public string MessageTypeMarkdownHead { get; set; } = "### ";

        [Parameter()]
        public bool IgnoreEqualPathPrefix { get; set; } = true;

        [Parameter()]
        public string StopCommit { get; set; } = "";

        public bool CurrentBranchIsMaster { get; set; } = false;

        private string m_defaultBranch = "";
        private BuildData m_buildData = new BuildData();
        private FixedSizeSlidingSet<string> m_checkCommitDesc = new FixedSizeSlidingSet<string>(1000);

        internal class BuildData
        {
            public ReleaseNoteMessages BuildMessage { get; set; } = new ReleaseNoteMessages();
            public string Commit { get; set; }
            public string Branch { get; set; }
            public CommitMessages CurrentMessages { get; set; }
        }

        private string GetGoodMessgeTypeName(string text)
        {
            switch (text.ToLower())
            {
                case "newfeature":
                case "newfeatures":
                    return NewFeatureName;

                case "fix":
                case "bug":
                case "hotfix":
                    return BugfixName;

                case "ui":
                case "style":
                    return StyleName;
            }
            return text;
        }

        bool ExecBranch(string line)
        {
            return !GitUtil.CheckBranchTextNeedStop(line, ReferenceVersion, IgnoreEqualPathPrefix);
        }

        private bool CheckFilterMessage(string message)
        {
            if (message.StartsWith("Reverted commit "))
                return true;
            return false;
        }

        bool ExecMessage(string line)
        {
            var text = line.AsSpan().Trim();
            CommitMessages messages;
            if (text.StartsWith(MessageTypePrefix) && text.Length > MessageTypePrefix.Length)
            {
                string typeName = GetGoodMessgeTypeName(text.Slice(MessageTypePrefix.Length).ToString());

                if (!m_buildData.BuildMessage.Build.TryGetValue(typeName, out messages))
                {
                    messages = new CommitMessages();
                    m_buildData.BuildMessage.Build[typeName] = messages;
                }
                m_buildData.CurrentMessages = messages;
                return true;
            }

            messages = m_buildData.CurrentMessages;
            if (messages == null)
                return true;

            string content = text.ToString().Replace("�", "");
            if (content.Length <= 0)
                return true;
            if (CheckFilterMessage(content))
                return true;

            content = ReplaceMessage(content);
            if (m_checkCommitDesc.Contains(content))
                return true;
            m_checkCommitDesc.Add(content);

            messages.AddListHead();
            messages.Build.Append(content);
            messages.Build.AppendLine();
            return true;
        }

        private string ReplaceMessage(string content)
        {
            content = RegexReporter.Replace(content, "");
            return content;
        }

        bool ExecCommitID(string line)
        {
            if (string.IsNullOrEmpty(StopCommit))
                return true;
            return !StopCommit.StartsWith(line);
        }

        bool ExecEmpty(string line)
        {
            return true;
        }

        private void Append(StringBuilder sb, ReleaseNoteMessages buildMessage, string key)
        {
            if (!buildMessage.Build.TryGetValue(key, out CommitMessages messages))
                return;
            buildMessage.Build.Remove(key);
            Append(sb, key, messages);
        }

        private void Append(StringBuilder sb, string key, CommitMessages messages)
        {
            if (messages.Build.Length == 0)
                return;
            sb.Append(MessageTypeMarkdownHead);
            sb.AppendLine(key);
            sb.Append(messages.Build);
        }

        private void Output(ReleaseNoteMessages buildMessages)
        {
            if (buildMessages.Build.Count == 0)
                return;
            StringBuilder sb = new StringBuilder(buildMessages.Build.Count * 4096);
            Append(sb, buildMessages, BugfixName);
            Append(sb, buildMessages, NewFeatureName);
            Append(sb, buildMessages, StyleName);
            foreach (var item in buildMessages.Build)
            {
                Append(sb, item.Key, item.Value);
            }
            WriteObject(sb.ToString());
        }

        private void LoadBranch(PowerShell powershell)
        {
            m_defaultBranch = GitUtil.GetDefaultBranch(powershell);
            if (string.IsNullOrEmpty(m_defaultBranch))
                return;

            if (!string.IsNullOrEmpty(StopCommit))
                return;

            var findVersion = Util.StableOrPathTryFindPrevVersion(powershell, ReferenceVersion);
            if (findVersion == null)
                return;

            string prevBranch = GitUtil.GetBranch(powershell, findVersion);
            if (string.IsNullOrEmpty(prevBranch))
                return;

            StopCommit = GitUtil.GetBaseCommit(powershell, m_defaultBranch, prevBranch);
        }

        internal class GetCustomLogMessageView : PSGitLogView
        {
            public GitGetCustomLogMessageCommand Cmd { get; }
            Func<string, bool> m_exec;

            public GetCustomLogMessageView(GitGetCustomLogMessageCommand cmd)
            {
                Cmd = cmd;
                m_exec = Cmd.ExecEmpty;
            }

            protected override void ParseText(string text)
            {
                if (!m_exec(text))
                {
                    m_stopParse = true;
                }
            }

            protected override void SectionEnterBranch()
            {
                m_exec = Cmd.ExecBranch;
            }

            protected override void SectionEnterCommit()
            {
                m_exec = Cmd.ExecCommitID;
            }

            protected override void SectionEnterEnd()
            {
                Cmd.m_buildData.CurrentMessages = null;
                m_exec = Cmd.ExecEmpty;
            }

            protected override void SectionEnterMessage()
            {
                m_exec = Cmd.ExecMessage;
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
                    string branch = GitUtil.GetCurrentBranch(powershell);
                    CurrentBranchIsMaster = branch.Equals(m_defaultBranch, StringComparison.OrdinalIgnoreCase);
                    var view = new GetCustomLogMessageView(this);
                    view.ParseLogByPowerShell(this, powershell, SearchCommitCount);
                    Output(m_buildData.BuildMessage);
                }
                
                runspace.Close();
            }


        }

    }
}
