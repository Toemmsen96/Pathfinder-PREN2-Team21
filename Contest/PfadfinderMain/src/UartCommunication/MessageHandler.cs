using System;
using System.Collections.Generic;

namespace UartCommunication
{
    /// <summary>
    /// Handles message-based command routing by registering command strings and their handler methods
    /// </summary>
    public class MessageHandler
    {
        // Dictionary to store command-handler pairs
        private readonly Dictionary<string, Action> _handlers = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Register a command handler that will be called when the specified command is received
        /// </summary>
        /// <param name="command">The command string to listen for (case insensitive)</param>
        /// <param name="handler">The action to execute when this command is received</param>
        public void RegisterCommand(string command, Action handler)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be null or empty", nameof(command));
            
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            
            // Normalize the command to lowercase for case-insensitive matching
            string normalizedCommand = command.Trim();
            
            // Add or replace the handler
            _handlers[normalizedCommand] = handler;
            Console.WriteLine($"Registered handler for command: '{normalizedCommand}'");
        }
        
        /// <summary>
        /// Unregister a handler for a specific command
        /// </summary>
        /// <param name="command">The command to stop listening for</param>
        /// <returns>True if a handler was removed, false otherwise</returns>
        public bool UnregisterCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return false;
                
            string normalizedCommand = command.Trim();
            bool removed = _handlers.Remove(normalizedCommand);
            
            if (removed)
                Console.WriteLine($"Unregistered handler for command: '{normalizedCommand}'");
                
            return removed;
        }
        
        /// <summary>
        /// Process an incoming message and invoke the appropriate handler if a matching command is found
        /// </summary>
        /// <param name="message">The received message</param>
        /// <returns>True if a handler was found and executed, false otherwise</returns>
        public bool ProcessMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;
            
            // Extract the command from the message
            string messageCommand = message.Trim();
            
            // Check if we have a registered handler for this command
            if (_handlers.TryGetValue(messageCommand, out Action? handler))
            {
                try
                {
                    Console.WriteLine($"Executing handler for command: '{messageCommand}'");
                    handler.Invoke();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in message handler for '{messageCommand}': {ex.Message}");
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Clear all registered command handlers
        /// </summary>
        public void ClearHandlers()
        {
            _handlers.Clear();
            Console.WriteLine("All command handlers cleared");
        }
        
        /// <summary>
        /// Get all registered commands
        /// </summary>
        /// <returns>Array of registered commands</returns>
        public string[] GetRegisteredCommands()
        {
            return _handlers.Keys.ToArray();
        }
    }
}