// Copyright © 2026 Miris, Inc. All rights reserved.

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Miris.Runtime
{
    /// <summary>
    /// Parameters controlling streaming fidelity, passed to the native client once per
    /// frame.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than SWIG-generated, and the layout must stay byte-compatible
    /// with <c>aqua::RuntimeSettings</c> in
    /// <c>modules/AquaScene/include/AquaScene/RuntimeSettings.h</c> -- same fields, same
    /// order. It crosses the boundary as <c>ref RuntimeSettings</c> via the typemap in
    /// AquaSwigBindings/AquaTypes.swg, so a mismatch corrupts memory rather than failing to
    /// compile. <c>scripts/build/check_blittable_layout.py</c> compares the two.
    ///
    /// It is not generated because app_kit needs what a generated proxy cannot give:
    /// value semantics, so MirisPlayerPreferences can snapshot and restore it and
    /// JsonConvert can persist it; and real fields carrying <see cref="RangeAttribute"/>,
    /// which ReflectionUtils reads to size the developer LOD sliders.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct RuntimeSettings
    {
        // Developer Note:
        //
        // C# structs support neither default field initializers nor a parameterless
        // constructor, so the defaults live in MirisStreamController.m_runtimeSettings and
        // are duplicated in the C++ constructor for unit tests.

        [Range(24.0f, 120.0f)]
        public float m_targetFramesPerSecond;

        [Range(1.0f, 20000000.0f)]
        public int m_splatCountBudget;

        [Range(1.0f, 20000.0f)]
        public int m_nodeCountBudget;

        [Range(256 * 1024, 1024 * 1024 * 100)]
        public int m_congestionMinInflightBytes;

        [Range(1024 * 1024, 1024 * 1024 * 500)]
        public int m_congestionMaxInflightBytes;

        // True when an immersive XR session is active
        public bool m_xrModeActive;

        // Cap for splat count budget (-1 means no cap)
        public int m_splatCountBudgetCap;

        // 0 = Equal, 1 = ValueWeighted (default)
        public int m_budgetSplitMode;
    }
}
