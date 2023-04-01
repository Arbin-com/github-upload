/*
* ==============================================================================
* Filename: CommitLogMessages
* Description: 
*
* Version: 1.0
* Created: 2023/7/13 11:53:16
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace ArbinUtil
{
    public class CommitMessages
    {
        int m_currentListIndex = 0;
        public StringBuilder Build { get; set; } = new StringBuilder(4096);

        public void AddListHead()
        {
            Build.Append($"1. ");
            ++m_currentListIndex;
        }
    }


    public class ReleaseNoteMessages
    {
        public Dictionary<string, CommitMessages> Build { get; set; } = new Dictionary<string, CommitMessages>(StringComparer.OrdinalIgnoreCase);

    }
}
