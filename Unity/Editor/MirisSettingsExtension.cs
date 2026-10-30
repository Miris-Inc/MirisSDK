// Copyright © 2026 Miris, Inc. All rights reserved.

namespace Miris.Editor
{
    /// <summary>
    /// Fields another package adds to the Miris > Settings window. The window creates one of every
    /// concrete subclass it finds, so the fields appear exactly when their package is installed.
    /// Each hook draws with EditorGUILayout.
    /// </summary>
    public abstract class MirisSettingsExtension
    {
        /// <summary>Drawn at the end of the Project block.</summary>
        public abstract void DrawProjectFields();

        /// <summary>
        /// Drawn at the end of the Scene block, for the saved scene at <paramref name="scenePath"/>.
        /// An extension keeps its own per-scene asset and applies its own edits.
        /// </summary>
        public virtual void DrawSceneFields(string scenePath) { }
    }
}
