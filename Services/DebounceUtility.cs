using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace DG2072_USB_Control.Services
{
    /// <summary>
    /// Utility for debouncing UI actions
    /// </summary>
    public static class DebounceUtility
    {
        private static readonly Dictionary<string, DispatcherTimer> Timers = new Dictionary<string, DispatcherTimer>();

        /// <summary>
        /// Debounce an action with a delay
        /// </summary>
        /// <param name="key">Unique identifier for this debounce operation</param>
        /// <param name="action">Action to execute after debounce</param>
        /// <param name="delayMs">Delay in milliseconds</param>
        public static void Debounce(string key, Action action, int delayMs = 500)
        {
            if (!Timers.ContainsKey(key))
            {
                Timers[key] = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
                Timers[key].Tick += (s, args) =>
                {
                    Timers[key].Stop();
                    action();
                };
            }

            Timers[key].Stop();
            Timers[key].Start();
        }
    }
}