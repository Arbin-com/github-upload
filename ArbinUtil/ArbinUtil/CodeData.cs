/*
* ==============================================================================
* Filename: CodeData
* Description: 
*
* Version: 1.0
* Created: 2023/5/6 11:08:52
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArbinUtil
{
    public class CodeData
    {
        public string CommitID { get; set; } = "";

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
