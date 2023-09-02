using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace ArbinUtil.Git
{
    internal abstract class PSGitLogView : GitLogView
    {
        const string Commit = "#_cm_";
        const string Branch = "#_bh_";
        const string Message = "#_ms_";
        const string End = "#_end_";

        public PSCmdlet PSCmdlet { get; set; }

        protected override void Parse(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (m_stopParse)
                    return;

                switch (line)
                {
                case Commit:
                Section = TextSection.Commit;
                SectionEnterCommit();
                break;

                case Branch:
                Section = TextSection.Branch;
                SectionEnterBranch();
                break;

                case Message:
                Section = TextSection.Message;
                SectionEnterMessage();
                break;

                case End:
                Section = TextSection.End;
                SectionEnterEnd();
                break;

                default:
                ParseText(line);
                break;
                }
            }
        }

        public void ParseLogByPowerShell(PSCmdlet cmdlet, PowerShell powershell, int searchCommitCount)
        {
            PSCmdlet = cmdlet;
            powershell.Commands.Clear();
            string logCmd = $"git log --pretty=\"{Commit}%n%h%n{Branch}%n%d%n{Message}%n%B%n{End}\"";
            if(searchCommitCount > 0)
            {
                logCmd += $" -n {searchCommitCount}";
            }
            powershell.AddScript(logCmd);
            Parse(powershell.Invoke().Select(x => x.ToString()));
        }


    }
}
