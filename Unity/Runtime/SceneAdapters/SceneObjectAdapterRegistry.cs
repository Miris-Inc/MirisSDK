// Copyright © 2024 Miris. All rights reserved.

using UnityEngine;

// Standard library
using System;
using System.Collections;
using System.Collections.Generic;

namespace Aqua.Runtime
{
    public class SceneObjectAdapterRegistry
    {
        public static SceneObjectAdapterRegistry s_instance = new SceneObjectAdapterRegistry();
        Dictionary<SceneObjectType, Func<BaseObjectAdapter>> m_typeToConstructor = new Dictionary<SceneObjectType, Func<BaseObjectAdapter>>();

        // TODO: change to take a constructor function as opposed to a instance of the adapter 
        public void RegisterAdapter(SceneObjectType type, Func<BaseObjectAdapter> constructor)
        {
            if (!m_typeToConstructor.ContainsKey(type))
            {
                m_typeToConstructor.Add(type, constructor);
            }
        }

        public Dictionary<SceneObjectType, BaseObjectAdapter> CreateAdapters()
        {
            Dictionary<SceneObjectType, BaseObjectAdapter> adapters = new Dictionary<SceneObjectType, BaseObjectAdapter>();
            foreach (KeyValuePair<SceneObjectType, Func<BaseObjectAdapter>> adapterPair in m_typeToConstructor)
            {
                if(m_typeToConstructor.TryGetValue(adapterPair.Key, out Func<BaseObjectAdapter> constructor)){
                   adapters.Add(adapterPair.Key, constructor());
                }
            }
            return adapters;
        }

        public Func<BaseObjectAdapter> GetAdapter(SceneObjectType type)
        {
            return m_typeToConstructor.TryGetValue(type, out var adapter) ? adapter : null;
        }
        
    }
}
