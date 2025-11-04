// Copyright © 2024 Miris. All rights reserved.

// Standard library
using System;

// Unity engine
using UnityEngine;

// Unity packages
using Unity.Collections;
using System.Collections.Generic;

namespace Aqua.Runtime
{

    /// <summary>
    /// GaussianSplatAquaDataSource extracts data from an C++ Aqua Scene Object to
    /// feed into the renderer.
    /// </summary>
    public class GaussianSplatAquaDataSource : MonoBehaviour
    {
        public GaussianSplatDataSource m_data = new();

        public float m_opacity
        {
            get => m_data.m_opacity;
            set
            {
                m_data.m_opacity = value;
            }
        }

        // --------------------------------------------------------------------
        // GaussianSplatDataSource overrides
        // --------------------------------------------------------------------

        public int GetSplatCount()
        {
            return m_data.GetSplatCount();
        }

        public bool HasBuffer(AttributeSemantic semantic)
        {
            return m_data.HasBuffer(semantic);
        }

        unsafe public AttributeBuffer GetBuffer(AttributeSemantic semantic)
        {
            return m_data.GetBuffer(semantic);
        }

        public Bounds GetObjectBounds()
        {
            return m_data.GetObjectBounds();
        }

        public bool IsValid()
        {
            return m_data.IsValid();
        }

        public int GetLodIndex()
        {
            return m_data.GetLodIndex();
        }
    }
}
