/*
* ==============================================================================
* Filename: GetJiraFieldCommand
* Description: 
*
* Version: 1.0
* Created: 2023/7/13 16:25:28
*
* Author: RuiSen
* ==============================================================================
*/

using ArbinUtil.Jira;
using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace ArbinUtil.PSCommand
{
    [Cmdlet(VerbsCommon.Get, "JiraFieldCommand")]
    [OutputType(typeof(MatchVersion))]
    public class GetJiraFieldCommand : PSCmdlet
    {
        [Parameter(Mandatory = true)]
        public string UserName { get; set; }
        [Parameter(Mandatory = true)]
        public string APIToken { get; set; }
        [Parameter(Mandatory = true)]
        public string JiraHostURL { get; set; }
        [Parameter(Mandatory = true)]
        public string IssueKey { get; set; }

        [Parameter(HelpMessage = "example: \"description,labels\"")]
        public string Fields { get; set; } = "description,labels";

        protected override void ProcessRecord()
        {
            string auth = JiraUtil.CreateJiraAuthorization(UserName, APIToken);
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                runspace.SessionStateProxy.Path.SetLocation(SessionState.Path.CurrentFileSystemLocation.Path);
                using (PowerShell powershell = PowerShell.Create())
                {
                    powershell.Runspace = runspace;

                    var jsonObject = JiraUtil.GetJiraIssueFields(JiraHostURL, auth, IssueKey, Fields).Result;
                    WriteObject(jsonObject);
                }
            }
        }

    }
}
