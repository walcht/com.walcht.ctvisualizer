using System;

namespace UnityCTVisualizer {
    public static class ProgressHandlerEvents {
        /// <summary>
        ///     Requests the progress handler UI to be enabled/disabled
        /// </summary>
        public static Action<bool> OnRequestActivate;

        /// <summary>
        ///     Requests setting the maximum progress value. Subsequent calls to OnRequestProgressValueIncrement will
        ///     update the progress by 1.0f/maxProgressValue
        /// </summary>
        public static Action<int> OnRequestMaxProgressValueUpdate;

        /// <summary>
        ///     Requests an increment of the progress value by 1. This will update the actual progress bar by
        ///     1.0f/maxProgressValue
        /// </summary>
        public static Action OnRequestProgressValueIncrement;

        /// <summary>
        ///     Requests an update of the progress bar's message to the provided string.
        /// </summary>
        public static Action<string> OnRequestProgressMessageUpdate;
    }
}

