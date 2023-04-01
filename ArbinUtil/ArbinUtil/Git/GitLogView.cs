using System;
using System.Collections.Generic;
using System.Management.Automation.Language;
using System.Text;

namespace ArbinUtil.Git
{
    internal abstract class GitLogView
    {
        private TextSection m_section = TextSection.End;
        protected bool m_stopParse = false;

        public enum TextSection
        {
            Commit,
            Branch,
            Message,
            End
        }

        protected TextSection Section
        {
            get { return m_section; }
            set
            {
                if (m_section != value)
                {
                    m_section = value;
                }
            }
        }
        

        public GitLogView()
        {

        }


        abstract protected void SectionEnterCommit();
        abstract protected void SectionEnterBranch();
        abstract protected void SectionEnterMessage();
        abstract protected void SectionEnterEnd();
        abstract protected void Parse(IEnumerable<string> lines);
        abstract protected void ParseText(string text);
    }
}
