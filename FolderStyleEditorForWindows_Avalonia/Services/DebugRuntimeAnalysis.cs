using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FolderStyleEditorForWindows.Services
{
    public static class DebugRuntimeAnalysis
    {
        private static bool _pauseAnimations;

        public static event EventHandler? PauseAnimationsChanged;

        public static bool PauseAnimations
        {
            get => _pauseAnimations;
            set
            {
                if (_pauseAnimations == value)
                {
                    return;
                }

                _pauseAnimations = value;
                PauseAnimationsChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
