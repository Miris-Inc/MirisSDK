// Copyright © 2026 Miris, Inc. All rights reserved.

using System.Runtime.CompilerServices;

// MirisStreamController exposes some fields (e.g. m_runtimeSettings, m_executionMode) as
// internal rather than public, since they are Miris-internal tuning/debug knobs and should not
// be part of the SDK's public editing surface. These two assemblies still need runtime access:
// app_kit's developer UI and preferences persistence, and this package's own test suite.
[assembly: InternalsVisibleTo("Miris.AppKit.Runtime")]
[assembly: InternalsVisibleTo("Miris.SDK.Core.Tests")]
