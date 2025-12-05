// Copyright © 2025 Miris. All rights reserved.

using System;
using UnityEngine;

namespace Miris.Runtime
{
    /// <summary>
    /// An RAII object to provide a scope for content to be sync'ed from the aqua scene model
    /// to Unity
    /// </summary>
    public class SceneChangeTracker : IDisposable
    {
        private bool m_sceneLocked;
        private Client m_client;

        public class Changes
        {
            public SceneChangeIds m_changeIds;
        }

        public SceneChangeTracker(Client client)
        {
            m_client = client;
            m_sceneLocked = m_client.LockScene();
        }

        public bool IsSceneLocked()
        {
            return m_sceneLocked;
        }

        public Changes GetSceneChanges()
        {
            Debug.Assert(m_sceneLocked);
            Changes changes = new Changes { m_changeIds = new SceneChangeIds() };
            m_client.GetSceneChangesCounts(ref changes.m_changeIds);
            changes.m_changeIds.AllocateArrays();
            m_client.GetSceneChanges(ref changes.m_changeIds);
            return changes;
        }

        public void Dispose()
        {
            if (m_sceneLocked)
            {
                m_client.UnlockScene();
            }
        }
    }
}
