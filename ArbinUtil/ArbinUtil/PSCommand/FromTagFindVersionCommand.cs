/*
* ==============================================================================
* Filename: FromTagFindVersionCommand
* Description: 
*
* Version: 1.0
* Created: 2023/4/21 11:15:43
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
    [Cmdlet(VerbsCommon.Get, "FromTagFindVersion")]
    [OutputType(typeof(ArbinVersion))]
    public class FromTagFindVersionCommand : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public ArbinVersion ReferenceVersion { get; set; }

        private ArbinVersion MaxVersion(ArbinVersion a, ArbinVersion b)
        {
            int comp = Util.AzurePiplineGoodVersionCompareTo(a, b);
            return comp >= 0 ? a : b;
        }

        protected override void ProcessRecord()
        {
            string versionText = ReferenceVersion.ToString();
            bool hasSuffix = ReferenceVersion.HasSuffix;
            uint specialNumber = ReferenceVersion.SpecialNumber;
            bool anySpecialNumber = specialNumber == ArbinVersion.AnyNumber;
            if (!ReferenceVersion.HasPathPrefix && ReferenceVersion.Major != ArbinVersion.AnyNumber)
            {
                versionText = "*" + versionText;
            }

            ArbinVersion maxVersion = null;
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                runspace.SessionStateProxy.Path.SetLocation(SessionState.Path.CurrentFileSystemLocation.Path);
                using (PowerShell powershell = PowerShell.Create())
                {
                    powershell.Runspace = runspace;
                    powershell.AddScript($"git tag -l \"{versionText}\"");
                    foreach (var item in powershell.Invoke())
                    {
                        var line = item.ToString();
                        if (!ArbinVersion.Parse(line, out ArbinVersion tempVersion))
                            continue;
                        if (hasSuffix != tempVersion.HasSuffix)
                            continue;
                        if (!anySpecialNumber && specialNumber != tempVersion.SpecialNumber)
                            continue;
                        maxVersion = maxVersion == null ? tempVersion : MaxVersion(maxVersion, tempVersion);
                    }
                }
                runspace.Close();
            }
            if (maxVersion != null)
            {
                WriteObject(maxVersion);
            }
        }

    }

}
