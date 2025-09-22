// Copyright © 2024 Miris.All rights reserved.

// Standard library
using System;
using System.Collections;
using System.Collections.Generic;

// Unity engine
using UnityEngine;


namespace Aqua.Runtime
{

    // BatchRoutineObject provides a simple way to group and manage coroutines
    public class BatchRoutineManager
    {

        private Queue<Coroutine> m_coroutines = new();
        private Action m_onBatchCompleted;

        public void Reset()
        {
            m_coroutines.Clear();
        }

        public int GetCount()
        {
            return m_coroutines.Count;
        }

        public void AddCoroutine(MonoBehaviour mainClass, IEnumerator enumerator)
        {
            m_coroutines.Enqueue(mainClass.StartCoroutine(enumerator));
        }

        public void SetOnBatchCompleted(Action callback)
        {
            m_onBatchCompleted = callback;
        }

        public void CompleteCoroutine()
        {
            if (m_coroutines.Count > 0)
            {
                m_coroutines.Dequeue();
            }

            if (m_coroutines.Count == 0)
            {
                m_onBatchCompleted.Invoke();
            }
        }
    }
}