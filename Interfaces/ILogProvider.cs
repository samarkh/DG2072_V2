namespace DG2072_USB_Control
{
    /// <summary>
    /// Interface for classes that provide logging functionality
    /// </summary>
    public interface ILogProvider
    {
        /// <summary>
        /// Logs a message
        /// </summary>
        /// <param name="message">The message to log</param>
        void Log(string message);
    }
}