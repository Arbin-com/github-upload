/*
* ==============================================================================
* Filename: NeedRemoveCommitVersionCommand
* Description: 
*
* Version: 1.0
* Created: 2023/4/8 15:26:03
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;

namespace ArbinUtil.PSCommand
{
    [Cmdlet(VerbsCommon.Remove, "OldCommitVersion")]
    [OutputType(typeof(ArbinVersion[]))]
    public class OldCommitVersionCommand : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public string Commit { get; set; } = "";

        [Parameter(Mandatory = true)]
        public ArbinVersion ReferenceVersion { get; set; }

        [Parameter()]
        public bool IgnoreEqualPathPrefix { get; set; } = true;

        private void FindVersion(string line, List<ArbinVersion> versions)
        {
            if (!ArbinVersion.Parse(line, out ArbinVersion arbinVersion))
                return;
            if (arbinVersion.SpecialNumber != ReferenceVersion.SpecialNumber)
                return;
            if (!arbinVersion.SameSuffix(ReferenceVersion.Suffix))
                return;
            if (!IgnoreEqualPathPrefix && !arbinVersion.SamePathPrefix(ReferenceVersion.PathPrefix))
                return;
            if (arbinVersion.MaxMajorMinorBuild(ReferenceVersion) >= 0)
                return;
            versions.Add(arbinVersion);
        }

        protected override void ProcessRecord()
        {
            List<ArbinVersion> versions = new List<ArbinVersion>();
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                runspace.SessionStateProxy.Path.SetLocation(SessionState.Path.CurrentFileSystemLocation.Path);
                using (PowerShell powershell = PowerShell.Create())
                {
                    powershell.Runspace = runspace;
                    powershell.AddScript($"git tag --contains {Commit}");
                    foreach (PSObject result in powershell.Invoke())
                    {
                        FindVersion(result.ToString(), versions);
                    }
                }
                runspace.Close();
            }
            WriteObject(versions.ToArray());
        }

    }
}
