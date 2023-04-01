/*
* ==============================================================================
* Filename: GitCommitVersion
* Description: 
* Find the last matching version
* 
* Version: 1.0
* Created: 2023/3/31 17:10:21
*
* Author: RuiSen
* ==============================================================================
*/

using System.Collections.Generic;

namespace ArbinUtil
{
    public class GitCommitVersion
    {
        public string Commit { get; set; }
        public ArbinVersion Version { get; set; }
        public List<ArbinVersion> Previous { get; set; }
    }

}
