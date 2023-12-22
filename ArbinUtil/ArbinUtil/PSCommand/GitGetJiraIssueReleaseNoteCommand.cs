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
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ArbinUtil.PSCommand
{
    [Cmdlet(VerbsCommon.Get, "GitGetJiraIssueReleaseNote")]
    [OutputType(typeof(string))]
    public partial class GitGetJiraIssueReleaseNoteCommand : PSCmdlet
    {
        private const string LabelName = "labels";
        private const string AssignName = "assignee";
        private const string TitleName = "summary";


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
        [AllowEmptyCollection]
        public JiraIssueKeys[] JiraIssueKeys { get; set; }

        public class SolveJiraResult
        {
            public JiraLikeMessage[] Messages { get; set; } = Array.Empty<JiraLikeMessage>();
            public string[] ErrorSearchs { get; set; } = Array.Empty<string>();

            public string ShowAllErrorSearch()
            {
                return string.Join("\n", ErrorSearchs);
            }
        }

        private async Task GetKeyContent(string search, ConcurrentBag<JiraLikeMessage> result, ConcurrentBag<string> errorSearchs, ConcurrentBag<string> errorExpTexts)
        {
            var jsonObject = (await JiraUtil.GetJiraIssueRange(JiraHostURL, m_auth, search, m_jiraFields)).AsObject();
            if (!jsonObject.TryGetPropertyValue("issues", out JsonNode issuesNode) || issuesNode == null)
            {
                errorSearchs.Add(search);
                errorExpTexts.Add("not get issues: \n" + jsonObject.ToString());
                return;
            }

            if (jsonObject.TryGetPropertyValue("warningMessages", out JsonNode warnNode) && warnNode != null)
            {
                errorExpTexts.Add($"warnMessage get issues {search}:\n" + warnNode.ToString());
            }

            var issues = issuesNode.AsArray();
            foreach (var issue in issues)
            {
                var fields = issue["fields"].AsObject();
                string jiraKey = issue["key"].GetValue<string>();
                JiraLikeMessage message = new JiraLikeMessage
                {
                    Key = jiraKey
                };

                if (fields.TryGetPropertyValue(TitleName, out JsonNode titleNode) && titleNode != null)
                {
                    message.Title = titleNode.ToString();
                }

                if (fields.TryGetPropertyValue(AssignName, out JsonNode assignNode) && assignNode != null)
                {
                    message.SolveUserName = assignNode["displayName"].GetValue<string>();
                }

                if (fields.TryGetPropertyValue(LabelName, out JsonNode nodeLabel) && nodeLabel != null)
                {
                    string[] labels = nodeLabel.AsArray().Select(x => x.GetValue<string>()).ToArray();
                    message.Labels = labels;
                }

                if (fields.TryGetPropertyValue(m_releaseNoteID, out JsonNode node) && node != null)
                {
                    string releaseNote = node.GetValue<string>()?.Trim();
                    message.ReleaseNote = releaseNote;
                }
                result.Add(message);
            }
        }

        private SolveJiraResult SolveJiraIssueFields()
        {
            ConcurrentBag<JiraLikeMessage> store = new ConcurrentBag<JiraLikeMessage>();
            ConcurrentBag<string> errorSearchs = new ConcurrentBag<string>();
            ConcurrentBag<string> errorExpTexts = new ConcurrentBag<string>();

            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 16
            };

            int MaxTryCount = 3;

            var block = new ActionBlock<string>(async (input) =>
            {
                int tryCounter = 0;
                SortedDictionary<string, JiraLikeMessage> nullTextKeys = new SortedDictionary<string, JiraLikeMessage>();
                while (true)
                {
                    try
                    {
                        await GetKeyContent(input, store, errorSearchs, errorExpTexts);
                    }
                    catch (Exception ex)
                    {
                        if(tryCounter++ >= MaxTryCount)
                        {
                            errorSearchs.Add(input);
                            errorExpTexts.Add(ex.ToString());
                            break;
                        }
                        Thread.Sleep(2 * 1000);
                        continue;
                    }
                    break;
                }
            }, options);

            const int BatchCount = 85;
            foreach (var range in JiraUtil.GetJiraBatchKeyRange(JiraUtil.GetJiraIssueKeyRange(JiraIssueKeys), BatchCount))
            {
                if (!block.Post(range))
                {
                    WriteVerbose($"Post Range error: {range}");

                }
            }

            block.Complete();
            block.Completion.Wait();

            if (errorExpTexts.Count > 0)
            {
                WriteVerbose("\n\nfind jira Exception:");
                foreach (var errorText in errorExpTexts)
                {
                    WriteVerbose(errorText);
                }
            }

            var result = new SolveJiraResult();
            result.Messages = store.ToArray();
            result.ErrorSearchs = errorSearchs.ToArray();
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

            m_jiraFields = string.Join(',', m_releaseNoteID, LabelName, AssignName, TitleName);
            return true;
        }

        protected override void ProcessRecord()
        {
            if(JiraIssueKeys == null || JiraIssueKeys.Length == 0)
            {
                WriteObject(new SolveJiraResult());
                return;
            }

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
                    var messages = SolveJiraIssueFields();
                    WriteObject(messages);
                }

                runspace.Close();
            }


        }

    }
}
