/*
* ==============================================================================
* Filename: JiraUtil
* Description: 
* 
* Version: 1.0
* Created: 2023-07-20 15:54:24
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.PowerShell.Commands;
using System.Web;
using System.Collections.Specialized;

namespace ArbinUtil.Jira
{
    public static class JiraUtil
    {

        public struct JiraIssueRange
        {
            public string Prefix { get; set; }
            public uint Number { get; set; }
            public int Count { get; set; }

            public string FromKey => $"{Prefix}-{Number}";
            public string ToKey => $"{Prefix}-{Number + Count - 1}";
        }

        public static IEnumerable<JiraIssueRange> GetJiraIssueKeyRange(IEnumerable<JiraIssueKeys> keys)
        {
            foreach (var key in keys)
            {
                var numbers = key.Numbers;
                int len = numbers.Length;
                int count = 0;
                uint baseNumber = 0;
                for (int i = 0; i < len; i++)
                {
                    uint number = numbers[i];
                    if (count == 0)
                    {
                        baseNumber = number;
                        count = 1;
                    }
                    else if ((baseNumber + count) != number)
                    {
                        yield return new JiraIssueRange { Prefix = key.Name, Number = baseNumber, Count = count };
                        baseNumber = (uint)number;
                        count = 1;
                    }
                }
                if (count > 0)
                {
                    yield return new JiraIssueRange { Prefix = key.Name, Number = baseNumber, Count = count };
                }
            }
        }

        public static IEnumerable<string> GetJiraIssueKeys(IEnumerable<JiraIssueKeys> keys)
        {
            foreach(var key in keys)
            {
                foreach(var number in key.Numbers)
                {
                    yield return $"{key.Name}-{number}";
                }
            }
        }

        public static string CreateJiraAuthorization(string userName, string apiToken)
        {
            var authData = System.Text.Encoding.UTF8.GetBytes($"{userName}:{apiToken}");
            return Convert.ToBase64String(authData);
        }

        private static async Task<JsonNode> Get(string url, string authorization)
        {
            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(new HttpMethod("GET"), url))
                {
                    request.Headers.UserAgent.TryParseAdd("request");
                    request.Headers.TryAddWithoutValidation("Accept", "application/json");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authorization);

                    var response = httpClient.SendAsync(request).Result;
                    string text = await response.Content.ReadAsStringAsync();
                    return JsonNode.Parse(text);
                }
            }
        }

        public static async Task<JsonNode> GetJiraFields(string jiraBaseURL, string authorization)
        {
            return await Get($"{jiraBaseURL}/rest/api/latest/field", authorization);
        }

        public static async Task<JsonNode> GetJQL(string jiraBaseURL, string authorization, string jql, NameValueCollection others)
        {
            others["jql"] = jql;
            string url = $"{jiraBaseURL}/rest/api/3/search?{others}";
            return await Get(url, authorization);
        }

        public static async Task<JsonNode> GetJiraIssueRange(string jiraBaseURL, string authorization, string issueKeyFrom, string issueKeyTo, string fields)
        {
            var query = HttpUtility.ParseQueryString("");
            query["fields"] = fields;
            return await GetJQL(jiraBaseURL, authorization, $"key >= {issueKeyFrom} AND key <= {issueKeyTo}", query);
        }

        public static async Task<JsonNode> GetJiraIssueFields(string jiraBaseURL, string authorization, string issueKey, string fields)
        {
            return await Get($"{jiraBaseURL}/rest/api/latest/issue/{issueKey}?fields={fields}", authorization);
        }

        public static async Task<JsonNode> GetJiraAllProjects(string jiraBaseURL, string authorization)
        {
            return await Get($"{jiraBaseURL}/rest/api/3/project", authorization);
        }

    }
}
