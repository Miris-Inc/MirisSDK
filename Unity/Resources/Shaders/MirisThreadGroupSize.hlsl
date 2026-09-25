// Copyright © 2026 Miris, Inc. All rights reserved.

// THREADS_PER_GROUP is size of a linear compute threadgroup,
// and MIRIS_SMALL_THREADGROUPS is the keyword that shrinks it.
//
// 1024 is what we ship.
// 256 is what the visionOS simulator's Metal caps to.

#ifndef MIRIS_THREAD_GROUP_SIZE_INCLUDED
#define MIRIS_THREAD_GROUP_SIZE_INCLUDED

#pragma multi_compile _ MIRIS_SMALL_THREADGROUPS

#if defined(MIRIS_SMALL_THREADGROUPS)
    #define THREADS_PER_GROUP 256
#else
    #define THREADS_PER_GROUP 1024
#endif

#endif // MIRIS_THREAD_GROUP_SIZE_INCLUDED
