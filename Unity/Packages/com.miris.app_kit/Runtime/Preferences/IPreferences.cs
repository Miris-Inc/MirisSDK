// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Threading.Tasks;
using UnityEngine;

namespace Miris.Runtime
{

    // Defines a common interface for managing the lifecycle of preferences in a robust manner.
    public interface IPreferences
    {
        abstract void SavePreferences();
        abstract Task LoadPreferences();
        abstract void ClearPreferences();
        abstract void RestoreDefaultPreferences();
        abstract void SaveDefaultPreferences();
    }
}
