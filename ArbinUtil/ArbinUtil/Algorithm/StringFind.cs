/*
* ==============================================================================
* Filename: StringFind
* Description: 
* 
* Version: 1.0
* Created: 2023-07-20 10:53:14
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace ArbinUtil.Algorithm
{

    public class StringFind
    {
        Node m_root = new Node();

        public class Node
        {
            internal protected char m_ch;
            internal int m_counter;
            internal int m_length;
            internal Node m_failNode;
            internal Dictionary<char, Node> m_next = new Dictionary<char, Node>();
        }

        public class StringFindBuilder
        {
            StringFind m_result = new StringFind();

            public StringFindBuilder() { }
            public void AddString(string word)
            {
                m_result.AddString(word);
            }

            public StringFind Build()
            {
                Queue<Node> queue = new Queue<Node>();
                queue.Enqueue(m_result.m_root);
                while(queue.TryDequeue(out Node current))
                {
                    foreach(var child in current.m_next)
                    {
                        child.Value.m_failNode = SolveFaildNode(current, child.Key, m_result.m_root);
                        queue.Enqueue(child.Value);
                    }
                }

                var old = m_result;
                m_result = new StringFind();
                return old;
            }

            private Node SolveFaildNode(Node current, char next, Node root)
            {
                Node result = root;
                Node each = current.m_failNode;
                while (each != null)
                {
                    if(each.m_next.TryGetValue(next, out Node matchNode))
                    {
                        result = matchNode;
                        break;
                    }
                    each = each.m_failNode;
                }
                return result;
            }
        }

        protected StringFind()
        {
            
        }

        protected void AddString(string word)
        {
            if (string.IsNullOrEmpty(word))
                return;

            Node node = m_root;
            int len = word.Length;
            for (int i = 0; i < len; i++)
            {
                char ch = word[i];
                if (!node.m_next.TryGetValue(ch, out Node nextNode))
                {
                    nextNode = new Node();
                    nextNode.m_ch = ch;
                    nextNode.m_length = node.m_length + 1;
                    node.m_next[ch] = nextNode;
                }
                node = nextNode;
            }
            ++node.m_counter;
        }

        public delegate void SearchTextDelegate(string text, int index, int length);

        public void Search(string text, SearchTextDelegate searchText)
        {
            int index = 0;
            int len = text.Length;
            Node current = m_root;
            while (index < len)
            {
                char ch = text[index++];
                Node next = null;
                while (current != null && !current.m_next.TryGetValue(ch, out next))
                {
                    current = current.m_failNode;
                }
                if (next == null)
                {
                    current = m_root;
                    continue;
                }
                current = next;
                if (current.m_counter > 0)
                {
                    int nodeTextLength = next.m_length;
                    searchText(text, index - nodeTextLength, nodeTextLength);
                }
            }
        }





    }
}
