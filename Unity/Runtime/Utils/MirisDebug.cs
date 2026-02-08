// Copyright © 2026 Miris, Inc. All rights reserved.

using UnityEngine;

namespace Miris.Runtime
{
    public class MirisDebug
    {
        static public void Log(object message)
        {
#if MIRIS_INTERNAL
            Debug.Log(message);
#endif
        }
    }
}
