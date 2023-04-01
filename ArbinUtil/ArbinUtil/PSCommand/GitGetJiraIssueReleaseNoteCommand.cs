/*
* ==============================================================================
* Filename: GitGetJiraIssueReleaseNoteCommand
* Description: 
* 
* Version: 1.0
* Created: 2023-07-20 17:12:23
*
* Author: RuiSen
* ==============================================================================
*/

using ArbinUtil.Algorithm;
using ArbinUtil.Git;
using ArbinUtil.Jira;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ArbinUtil.PSCommand
{
    [Cmdlet(VerbsCommon.Get, "GitGetJiraIssueReleaseNote")]
    [OutputType(typeof(string))]
    public class GitGetJiraIssueReleaseNoteCommand : PSCmdlet
    {
        private string m_auth;
        private string m_releaseNoteID;
        private string m_jiraFields;

        [Parameter(Mandatory = true)]
        public string UserName { get; set; }

        [Parameter(Mandatory = true)]
        public string APIToken { get; set; }

        [Parameter(Mandatory = true)]
        public string JiraHostURL { get; set; }

        [Parameter(Mandatory = true)]
        public JiraIssueKeys[] JiraIssueKeys { get; set; }

        private class JiraLikeMessage
        {
            public string Key { get; set; }
            public string ReleaseNote { get; set; }
            public string[] Labels { get; set; }
        }

        private async Task<IEnumerable<JiraLikeMessage>> SolveJiraIssueFields()
        {
            ConcurrentBag<JiraLikeMessage> result = new ConcurrentBag<JiraLikeMessage>();

            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 32,
                BoundedCapacity = 64
            };

            var block = new ActionBlock<JiraUtil.JiraIssueRange>(async (input) =>
            {
                try
                {
                    var jsonObject = (await JiraUtil.GetJiraIssueRange(JiraHostURL, m_auth, input.FromKey, input.ToKey, m_jiraFields)).AsObject();
                    var issues = jsonObject["issues"].AsArray();
                    foreach (var issue in issues)
                    {
                        var fields = issue["fields"].AsObject();
                        if (!fields.TryGetPropertyValue(m_releaseNoteID, out JsonNode? node))
                            continue;
                        string jiraKey = issue["key"].GetValue<string>();
                        string releaseNote = node?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(releaseNote))
                            continue;
                        if (!fields.TryGetPropertyValue("labels", out node))
                            continue;
                        JiraLikeMessage message = new JiraLikeMessage();
                        message.ReleaseNote = releaseNote;
                        message.Key = jiraKey;
                        string[] labels = null;
                        if(node != null)
                            labels = node.AsArray().Select(x => x.GetValue<string>()).ToArray();
                        message.Labels = labels == null ? Array.Empty<string>() : labels;
                        result.Add(message);
                    }
                }
                catch (Exception e)
                {
                    
                }
            }, options);

            foreach (var range in JiraUtil.GetJiraIssueKeyRange(JiraIssueKeys))
            {
                await block.SendAsync(range);
            }

            block.Complete();
            await block.Completion;

            return result;
        }

        private bool LoadJiraFields()
        {
            var array = JiraUtil.GetJiraFields(JiraHostURL, m_auth).Result.AsArray();
            JsonNode node = array.FirstOrDefault(x => nameof(JiraLikeMessage.ReleaseNote).Equals(x["name"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase));
            if(node == null)
            {
                WriteVerbose($"not find '{nameof(JiraLikeMessage.ReleaseNote)}' field");
                return false;
            }

            m_releaseNoteID = node["id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(m_releaseNoteID))
                return false;

            m_jiraFields = string.Join(',', m_releaseNoteID, "labels");
            return true;
        }

        protected override void ProcessRecord()
        {
            m_auth = JiraUtil.CreateJiraAuthorization(UserName, APIToken);
            using (Runspace runspace = RunspaceFactory.CreateRunspace())
            {
                runspace.Open();
                runspace.SessionStateProxy.Path.SetLocation(SessionState.Path.CurrentFileSystemLocation.Path);
                using (PowerShell powershell = PowerShell.Create())
                {
                    powershell.Runspace = runspace;
                    if (!LoadJiraFields())
                        return;
                    var messages = SolveJiraIssueFields().Result;
                    WriteObject(messages);
                }

                runspace.Close();
            }


        }

    }
}
