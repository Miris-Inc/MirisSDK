// Copyright © 2025 Miris, Inc. All rights reserved.

// This is a valid C++ and C# file :)

#if __cplusplus
#define public 
#else
#define USING_CSHARP
#endif 

#if USING_CSHARP

using System;
using System.Runtime.InteropServices;

namespace Miris.Runtime
{
#endif

#if __cplusplus
public enum class SceneObjectModifyFlag
#else
[Flags] //enables easy flag output
public enum SceneObjectModifyFlag
#endif
{ 
    NONE = 0,
    ARRAYS = 1 << 0,
    TRANSFORM = 1 << 1
};

#if USING_CSHARP
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
#endif

    public struct SceneObjectModifyFlagState
    {

#if USING_CSHARP
        public SceneObjectModifyFlag m_flags;
#else 
        int m_flags = static_cast<int>(SceneObjectModifyFlag::NONE);
#endif


#if __cplusplus

    SceneObjectModifyFlagState() {
        m_flags = static_cast<int>(SceneObjectModifyFlag::NONE);
    }

    SceneObjectModifyFlagState(SceneObjectModifyFlag flag) {
        m_flags = static_cast<int>(flag);
    }

    void SetFlag(SceneObjectModifyFlag flag)
    {
        m_flags |= static_cast<int>(flag);
    }

    void ClearFlag(SceneObjectModifyFlag flag)
    {
        m_flags &= ~static_cast<int>(flag);
    }

    bool HasFlag(SceneObjectModifyFlag flag) const
    {
        int flagInt = static_cast<int>(flag);
        return (m_flags & flagInt) == flagInt;
    }

    void Reset()
    {
        m_flags = static_cast<int>(SceneObjectModifyFlag::NONE);
    }

#endif 

#if USING_CSHARP
    public void SetFlag(SceneObjectModifyFlag flag)
    {
        m_flags |= flag;
    }

    public void ClearFlag(SceneObjectModifyFlag flag)
    {
        m_flags &= ~flag;
    }

    public bool HasFlag(SceneObjectModifyFlag flag)
    {
        return (m_flags & flag) == flag;
    }

    public override string ToString()
    {
        return m_flags.ToString();
    }

    public void Reset()
    {
        m_flags = SceneObjectModifyFlag.NONE;
    }

#endif

    }
#if USING_CSHARP
} // Miris.Runtime
#else
;
#undef public
#endif
