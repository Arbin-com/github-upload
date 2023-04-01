/*
* ==============================================================================
* Filename: RemoveRemoteTagByVersionCommand
* Description: 
*
* Version: 1.0
* Created: 2023/4/13 11:50:42
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;

namespace ArbinUtil.PSCommand
{

    [Cmdlet(VerbsCommon.Remove, "RemoteTagByVersion")]
    [OutputType(typeof(GitCommitVersion))]
    public class RemoveRemoteTagByVersionCommand : PSCmdlet
    {
        [Parameter()]
        public int Skip { get; set; } = 5;

        [Parameter(Mandatory = true)]
        public IEnumerable<ArbinVersion> Versions { get; set; }

        protected override void ProcessRecord()
        {
            IEnumerable<ArbinVersion> versions;
            if (Skip > 0)
            {
                versions = Versions.Skip(Skip);
            }
            else
            {
                versions = Versions;
            }

            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                runspace.SessionStateProxy.Path.SetLocation(SessionState.Path.CurrentFileSystemLocation.Path);
                using (PowerShell powershell = PowerShell.Create())
                {
                    powershell.Runspace = runspace;
                    foreach (var version in versions)
                    {
                        powershell.Commands.Clear();
                        powershell.AddScript($"git push origin :refs/tags/{version}");

                        WriteObject($"{powershell.Commands.Commands[0]}");
                        var result = powershell.Invoke();
                        foreach (var item in result)
                        {
                            WriteObject(item.ToString());
                        }
                        foreach (var item in powershell.Streams.Error.ReadAll())
                        {
                            WriteObject(item.ToString());
                        }
                    }
                }
                runspace.Close();
            }
        }
    }

}
