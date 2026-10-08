// Copyright © 2026 Miris, Inc. All rights reserved.

// Unity packages
using UnityEngine;

namespace Miris.Runtime
{
    /// <summary>
    /// One GaussianSplats scene object of a ModelRoot, tracked for its bounds.
    /// </summary>
    internal class MirisAssetDataSource
    {
        public SceneObject m_object = null;

        public int m_ancestorSceneObjectId;

        private Bounds m_bounds = new();

        public bool m_dirty = true;

        public Bounds GetObjectBounds()
        {
            if (m_dirty)
            {
                m_bounds = m_object.GetBoundingBoxRelativeToAncestor(m_ancestorSceneObjectId);
            }
            return m_bounds;
        }

        public bool IsValid()
        {
            // TODO: Need more robust way of checking whether or not a data source is valid.
            return m_object != null && m_object.GetAttributeCount() > 0;
        }
    }
}
