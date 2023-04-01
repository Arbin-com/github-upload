/*
* ==============================================================================
* Filename: MatchVersion
* Description: 
*
* Version: 1.0
* Created: 2023/5/5 14:31:51
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;

namespace ArbinUtil
{
    public class MatchVersion
    {
        public string Version { get; set; } = "";
        public CodeData CodeData { get; set; } = new CodeData();
        public string[] ReceivePath = Array.Empty<string>();
    }
}
