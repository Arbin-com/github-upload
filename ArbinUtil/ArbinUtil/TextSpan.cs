/*
* ==============================================================================
* Filename: TextSpan
* Description: 
*
* Version: 1.0
* Created: 2023/4/14 14:04:57
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace ArbinUtil
{
    public struct TextSpan
    {
        public static TextSpan Empty => new TextSpan("", 0, 0);
        public bool IsEmpty => Length == 0;
        public string Text { get; }
        public int Start { get; }
        public int To => Start + Length - 1;
        public int Length { get; }


        public TextSpan(string text, int start, int length)
        {
            Text = text;
            Start = start;
            Length = length;
        }

        public override string ToString()
        {
            return Text.Substring(Start, Length);
        }

    }
}
