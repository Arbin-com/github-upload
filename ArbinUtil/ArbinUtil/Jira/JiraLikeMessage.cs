/*
* ==============================================================================
* Filename: JiraLikeMessage
* Description: 
* 
* Version: 1.0
* Created: 2023-07-20 17:12:23
*
* Author: RuiSen
* ==============================================================================
*/

using System;

namespace ArbinUtil.Jira
{
    public class JiraLikeMessage
    {
        public string Key { get; set; }
        public string SolveUserName { get; set; } = "";
        public string ReleaseNote { get; set; }
        public string Title { get; set; } = "";
        public string[] Labels { get; set; } = Array.Empty<string>();
    }
}
