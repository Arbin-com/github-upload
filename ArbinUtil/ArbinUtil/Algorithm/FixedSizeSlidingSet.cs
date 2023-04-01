/*
* ==============================================================================
* Filename: FixedSizeSlidingSet
* Description: 
* not implement ISet<>
*
* Version: 1.0
* Created: 2023/6/19 9:43:34
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ArbinUtil.Algorithm
{
    public class FixedSizeSlidingSet<TValue>
    {
        private Queue<TValue> m_queue;
        private HashSet<TValue> m_check;

        public int FixedSize { get; }
        public int Count => m_check.Count;

        public FixedSizeSlidingSet() : this(1000)
        {

        }

        public FixedSizeSlidingSet(int fixedSize)
        {
            FixedSize = fixedSize;
            m_queue = new Queue<TValue>(FixedSize);
            m_check = new HashSet<TValue>(FixedSize);
        }

        public void Add(TValue value)
        {
            if (m_queue.Count >= FixedSize)
            {
                TValue first = m_queue.Dequeue();
                m_check.Remove(first);
            }
            m_check.Add(value);
            m_queue.Enqueue(value);
        }

        public bool Contains(TValue value)
        {
            return m_check.Contains(value);
        }


    }
}
