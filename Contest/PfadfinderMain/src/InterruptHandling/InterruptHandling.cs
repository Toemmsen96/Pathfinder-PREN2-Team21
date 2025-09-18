using System;
using System.Threading;

namespace PfadfinderMain
{
    public class InterruptHandling : IDisposable
    {
        private readonly Action _cleanupAction;
        private bool _isShuttingDown = false;
        private ManualResetEvent? _shutdownEvent = new ManualResetEvent(false);
        
        /// <summary>
        /// Creates a new instance of the InterruptHandling class
        /// </summary>
        /// <param name="cleanupAction">Action to be performed during cleanup</param>
        public InterruptHandling(Action cleanupAction)
        {
            _cleanupAction = cleanupAction ?? throw new ArgumentNullException(nameof(cleanupAction));
            
            // Register for process termination events
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        /// <summary>
        /// Checks if the application has received a shutdown signal
        /// </summary>
        public bool ShouldContinueRunning => !_isShuttingDown;

        /// <summary>
        /// Waits for a shutdown signal to be received
        /// </summary>
        /// <param name="timeout">Optional timeout in milliseconds</param>
        /// <returns>True if shutdown was signaled, false if timeout occurred</returns>
        public bool WaitForShutdown(int timeout = -1)
        {
            return _shutdownEvent?.WaitOne(timeout) ?? false;
        }

        /// <summary>
        /// Manually trigger a shutdown
        /// </summary>
        public void TriggerShutdown()
        {
            if (!_isShuttingDown)
            {
                _isShuttingDown = true;
                _shutdownEvent?.Set();
                PerformCleanup();
            }
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            Console.WriteLine("\nProcess exit detected.");
            TriggerShutdown();
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            // Prevent immediate termination to allow cleanup
            e.Cancel = true;
            Console.WriteLine("\nCtrl+C detected. Shutting down gracefully...");
            TriggerShutdown();
        }

        private void PerformCleanup()
        {
            try
            {
                _cleanupAction();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Unregister event handlers when the object is no longer needed
        /// </summary>
        public void Dispose()
        {
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            Console.CancelKeyPress -= OnCancelKeyPress;
            
            _shutdownEvent?.Dispose();
            _shutdownEvent = null;
        }
    }
}