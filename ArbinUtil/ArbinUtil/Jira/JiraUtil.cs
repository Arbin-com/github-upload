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
using static ArbinUtil.PSCommand.GitGetJiraIssueReleaseNoteCommand;
using System.Linq;
using ArbinUtil.EnumHelp.Attributes;
using ArbinUtil.EnumHelp;

namespace ArbinUtil.Jira
{
    public static class JiraUtil
    {
        public const string NewFeatureLabel = "New Feature";
        public const string FixLabel = "Fix";
        public const string UILabel = "UI";

        private static string[][] JiraLabelElements = new string[(int)LikeLabelName.MaxCount][] ;

        public enum LikeLabelName
        {
            None = -1,
            [EnumDescription(Desc = NewFeatureLabel)]
            NewFeatures,
            [EnumDescription(Desc = UILabel)]
            UI,
            [EnumDescription(Desc = FixLabel)]
            Fix,
            MaxCount
        }


        public struct JiraIssueRange
        {
            public string Prefix { get; set; }
            public uint Number { get; set; }
            public int Count { get; set; }

            public string FromKey => $"{Prefix}-{Number}";
            public string ToKey => $"{Prefix}-{Number + Count - 1}";
        }

        static JiraUtil()
        {
            var likeLabels = EnumUtil.GetDescriptions<LikeLabelName>();
            for (int i = 0; i < (int)LikeLabelName.MaxCount; i++)
            {
                JiraLabelElements[i] = new string[] { likeLabels[i] };
            }
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
                    else
                        ++count;
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
            others["validateQuery"] = "warn";
            others["maxResults"] = "10000";
            string url = $"{jiraBaseURL}/rest/api/3/search?{others}";
            return await Get(url, authorization);
        }

        public static async Task<JsonNode> GetJiraIssueRange(string jiraBaseURL, string authorization, string search, string fields)
        {
            var query = HttpUtility.ParseQueryString("");
            query["fields"] = fields;
            return await GetJQL(jiraBaseURL, authorization, search, query);
        }

        public static async Task<JsonNode> GetJiraIssueRange(string jiraBaseURL, string authorization, string issueKeyFrom, string issueKeyTo, string fields)
        {
            var query = HttpUtility.ParseQueryString("");
            query["fields"] = fields;
            return await GetJQL(jiraBaseURL, authorization, $"key >= {issueKeyFrom} AND key <= {issueKeyTo}", query);
        }

        public static string JoinOR(IEnumerable<string> strings)
        {
            return string.Join(" OR ", strings);
        }

        public static IEnumerable<string> GetJiraBatchKeyRange(IEnumerable<JiraIssueRange> src, int batchCount)
        {
            StringBuilder sbInKeys = new StringBuilder();
            int addCounter = 0;
            foreach (JiraIssueRange range in src)
            {
                int keyCount = range.Count;
                //Don't use a range search, there is a chance that the key value is not supported.
                for (int i = 0; i < keyCount; i++)
                {
                    if (addCounter == 0)
                    {
                        sbInKeys.Append("key in (");
                    }
                    else
                    {
                        sbInKeys.Append(",");
                    }
                    sbInKeys.Append($"{range.Prefix}-{range.Number + i}");
                    ++addCounter;

                    if (addCounter >= batchCount)
                    {
                        addCounter = 0;
                        sbInKeys.Append(")");
                        string searchText = sbInKeys.ToString();
                        sbInKeys.Clear();
                        yield return searchText;
                    }
                }
            }

            if (addCounter > 0)
            {
                sbInKeys.Append(")");
                yield return sbInKeys.ToString();
            }
        }

        public static async Task<JsonNode> GetJiraIssueFields(string jiraBaseURL, string authorization, string issueKey, string fields)
        {
            return await Get($"{jiraBaseURL}/rest/api/latest/issue/{issueKey}?fields={fields}", authorization);
        }

        public static async Task<JsonNode> GetJiraAllProjects(string jiraBaseURL, string authorization)
        {
            return await Get($"{jiraBaseURL}/rest/api/3/project", authorization);
        }

        public static string GetKeyURL(string jiraHost, string issueKey)
        {
            return $"{jiraHost}/browse/{issueKey}";
        }

        public static string ShowAllKeyText(IEnumerable<JiraIssueKeys> issueKeys)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var keys in issueKeys)
            {
                foreach (var number in keys.Numbers)
                    sb.AppendLine($"{keys.Name}-{number}");
            }
            return sb.ToString();
        }

        public static string GetJiraLikeMessageMarkdownTable(JiraLikeMessage[] messages)
        {
            return Util.ToMarkDownTable(messages, (propName) =>
            {
                switch(propName)
                {
                case nameof(JiraLikeMessage.Key):
                {
                    return Util.Aligin.Center;
                }
                default:
                {
                    return Util.Aligin.Left;
                }
                }
            });
        }

        private static void Add(Dictionary<string, SortedSet<uint>> append, JiraIssueKeys[] keys)
        {
            foreach (var key in keys)
            {
                if (!append.TryGetValue(key.Name, out SortedSet<uint> value))
                {
                    value = new SortedSet<uint>();
                    append[key.Name] = value;
                }
                var numbers = key.Numbers;
                int len = numbers.Length;
                for (int i = 0; i < len; i++)
                {
                    value.Add(numbers[i]);
                }
            }
        }

        private static LikeLabelName GetLikeJiraLabel(string label)
        {
            switch (label.ToLower())
            {
            case "newfeatures":
            case "newfeature":
            case "new feature":
            case "new features":
            return LikeLabelName.NewFeatures;

            case "fix":
            case "bug":
            return LikeLabelName.Fix;

            case "ui":
            return LikeLabelName.UI;

            default:
            return LikeLabelName.None;
            }
        }

        public static JiraIssueKeys[] Union(IEnumerable<JiraIssueKeys[]> source)
        {
            Dictionary<string, SortedSet<uint>> result = new Dictionary<string, SortedSet<uint>>();
            foreach (var item in source)
            {
                if (item != null)
                    Add(result, item);
            }
            return result.Select(x => new JiraIssueKeys() { Name = x.Key, Numbers = x.Value.ToArray() }).ToArray();
        }

        public static int JiraKeyComp(string a, string b)
        {
            var span1 = a.AsSpan();
            var span2 = b.AsSpan();
            int split1 = span1.IndexOf('-');
            int split2 = span2.IndexOf('-');
            if(split1 == -1 || split2 == -1 || split1 >= (a.Length - 1) || split2 >= (b.Length - 1))
            {
                return a.CompareTo(b);
            }

            int comp = span1[..split1].CompareTo(span2[..split2], StringComparison.OrdinalIgnoreCase);
            if(comp != 0)
                return comp;

            int.TryParse(span1[(split1 + 1)..], out int value1);
            int.TryParse(span2[(split2 + 1)..], out int value2);
            return value1.CompareTo(value2);
        }

        public static JiraLikeMessage[] ApplySortJiraLikeMessageAndChangeData(IEnumerable<JiraLikeMessage> source)
        {
            var likeLabels = EnumUtil.GetDescriptions<LikeLabelName>();
            List<JiraLikeMessage>[] groupMessage = new List<JiraLikeMessage>[likeLabels.Count];

            for (int i = 0; i < (int)LikeLabelName.MaxCount; i++)
            {
                groupMessage[i] = new List<JiraLikeMessage>();
            }

            foreach (var message in source)
            {
                bool has = false;
                LikeLabelName likeLabelName = LikeLabelName.Fix;
                foreach(var label in message.Labels)
                {
                    likeLabelName = GetLikeJiraLabel(label);
                    if (likeLabelName != LikeLabelName.None)
                    {
                        has = true;
                        break;
                    }
                }
                if(!has)
                {
                    likeLabelName = LikeLabelName.Fix;
                }
                message.Labels = JiraLabelElements[(int)likeLabelName];
                groupMessage[(int)likeLabelName].Add(message);
            }

            foreach(var group in groupMessage)
            {
                group.Sort((x, y) => JiraKeyComp(x.Key, y.Key));
            }

            List<JiraLikeMessage> result = new List<JiraLikeMessage>();
            groupMessage.Aggregate(result, (cur, value) =>
            {
                cur.AddRange(value);
                return cur;
            });
            return result.ToArray();
        }

        public class JiraKeyLineShowWe
        {
            private readonly string m_hostURL;

            public string Title { get; set; } = "";
            public string Key { get; set; }
            public string AssignName { get; set; }
            public string URL => JiraUtil.GetKeyURL(m_hostURL, Key);
            public string ReleaseNote { get; set; }
            public string Label { get; set; }

            public JiraKeyLineShowWe(JiraLikeMessage message, string hostURL)
            {
                Title = message.Title;
                Key = message.Key;
                AssignName = message.SolveUserName;
                ReleaseNote = message.ReleaseNote;
                Label = string.Join(", ", message.Labels); 
                m_hostURL = hostURL;
            }
        }

        public class JiraKeyLineShowUser
        {
            public string Key { get; set; }
            public string ReleaseNote { get; set; }
            public string Label { get; set; }

            public JiraKeyLineShowUser(JiraLikeMessage message)
            {
                Key = message.Key;
                ReleaseNote = message.ReleaseNote;
                Label = string.Join(", ", message.Labels);
            }
        }

        public static JiraKeyLineShowWe[] ToJiraKeyLineShowWe(IEnumerable<JiraLikeMessage> source, string hostURL)
        {
            if(source == null)
                return Array.Empty<JiraKeyLineShowWe>();
            List<JiraKeyLineShowWe> result = new List<JiraKeyLineShowWe>();
            foreach (var message in source)
            {
                result.Add(new JiraKeyLineShowWe(message, hostURL));
            }
            return result.ToArray();
        } 

        public static JiraKeyLineShowUser[] ToJiraKeyLineShowUser(IEnumerable<JiraLikeMessage> source)
        {
            if (source == null)
                return Array.Empty<JiraKeyLineShowUser>();
            List<JiraKeyLineShowUser> result = new List<JiraKeyLineShowUser>();
            foreach (var message in source)
            {
                result.Add(new JiraKeyLineShowUser(message));
            }
            return result.ToArray();
        }

        public static string ToMarkdownTable(JiraKeyLineShowUser[] users)
        {
            if (users == null || users.Length == 0)
                return "";
            return Util.ToMarkDownTable(users, (name) =>
            {
                switch (name)
                {
                case nameof(JiraKeyLineShowUser.Key):
                return Util.Aligin.Center;
                default:
                return Util.Aligin.Left;
                }
            });
        }

        public static JiraLikeMessage[] FilterByNeedShowUser(IEnumerable<JiraLikeMessage> source)
        {
            return source.Where(x => !string.IsNullOrEmpty(x.ReleaseNote)).ToArray();
        }

        public static JiraLikeMessage[] FilterByNeedFill(IEnumerable<JiraLikeMessage> source)
        {
            return source.Where(x => string.IsNullOrEmpty(x.ReleaseNote)).ToArray();
        }

    }
}
