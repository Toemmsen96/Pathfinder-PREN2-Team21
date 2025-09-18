using System;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace UARTCommunication
{
    public class UartCommunicator : IDisposable
    {
        // IDisposable wird benötigt, um SerialPort-Ressourcen ordnungsgemäß freizugeben,
        // wenn die Klasse nicht mehr benötigt wird (verhindert Ressourcenlecks)

        private SerialPort? _uart;
        private bool _disposed = false;

        // Fixe Konfigurationsparameter
        private readonly string _portName = "/dev/serial0"; // Für Raspberry Pi
        private readonly int _baudRate = 9600;
        private readonly Parity _parity = Parity.None;
        private readonly StopBits _stopBits = StopBits.One;
        private readonly int _dataBits = 8;
        private readonly int _readTimeout = 500; // Reduced from 1000ms to 500ms
        private readonly int _writeTimeout = 500; // Reduced from 1000ms to 500ms
        private readonly int _maxWaitTime = 2; // Reduced from 5 to 2 seconds

        public bool IsConnected => _uart != null && _uart.IsOpen;

        /// <summary>
        /// Initialisiert die UART-Verbindung
        /// </summary>
        /// <returns>True wenn erfolgreich, False bei Fehler</returns>
        public bool Initialize()
        {
            Console.WriteLine("Initializing UART...");
            try
            {
                _uart = new SerialPort(_portName)
                {
                    BaudRate = _baudRate,
                    Parity = _parity,
                    StopBits = _stopBits,
                    DataBits = _dataBits,
                    ReadTimeout = _readTimeout,
                    WriteTimeout = _writeTimeout,
                    // Performance optimizations
                    ReceivedBytesThreshold = 1, // Trigger DataReceived event on single byte
                    NewLine = "\n", // Set newline character for ReadLine method
                    RtsEnable = true, // Enable Request to Send
                    DtrEnable = true  // Enable Data Terminal Ready
                };

                if (!_uart.IsOpen)
                {
                    _uart.Open();
                    Console.WriteLine("UART port opened successfully.");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error initializing UART: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Testet die UART-Verbindung mit einem einfachen Nachrichtentest
        /// </summary>
        /// <returns>True wenn Test erfolgreich, False bei Fehler</returns>
        public bool UartTest()
        {
            Console.WriteLine("Starting UART Communication Test");
            try
            {
                // Testnachricht senden und empfangen
                string testMessage = "Test";

                if (SendMessage(testMessage))
                {
                    // Antwort empfangen
                    string received = ReceiveMessage();

                    // Prüfen, ob die Antwort der Testnachricht entspricht
                    if (received == testMessage)
                    {
                        Console.WriteLine("UART test successful! Sent and received messages match.");
                        return true;
                    }
                    else if (received == "")
                    {
                        Console.WriteLine("UART test failed. No response received.");
                        return false;
                    }
                    else
                    {
                        Console.WriteLine($"UART test failed. Messages don't match. Received: {received}");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("UART test failed. Could not send message.");
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error during UART test: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sendet eine Nachricht über die UART-Verbindung
        /// </summary>
        /// <param name="message">Die zu sendende Nachricht</param>
        /// <returns>True wenn erfolgreich, False bei Fehler</returns>
        public bool SendMessage(string message)
        {
            if (_uart == null || !_uart.IsOpen)
            {
                Console.WriteLine("Error: UART is not initialized or not open.");
                return false;
            }

            Console.WriteLine($"Sending: {message}");
            try
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message + '\0');
                _uart.Write(messageBytes, 0, messageBytes.Length);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error sending message: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Empfängt eine Nachricht über die UART-Verbindung
        /// Optimized version with reduced delays and better buffering
        /// </summary>
        /// <returns>Die empfangene Nachricht oder einen leeren String bei Timeout</returns>
        public string ReceiveMessage()
        {
            if (_uart == null || !_uart.IsOpen)
            {
                Console.WriteLine("Error: UART is not initialized or not open.");
                return string.Empty;
            }

            StringBuilder messageBuilder = new StringBuilder();
            DateTime startTime = DateTime.Now;
            TimeSpan maxWaitTimeSpan = TimeSpan.FromSeconds(_maxWaitTime);

            try
            {
                // Use more efficient approach with shorter polling intervals
                while (DateTime.Now - startTime < maxWaitTimeSpan)
                {
                    if (_uart.BytesToRead > 0)
                    {
                        // Read available bytes more efficiently
                        byte[] buffer = new byte[_uart.BytesToRead];
                        int bytesRead = _uart.Read(buffer, 0, buffer.Length);
                        
                        for (int i = 0; i < bytesRead; i++)
                        {
                            byte readByte = buffer[i];
                            
                            // Check if we've reached the end of a message
                            if (readByte == '\n' || readByte == '\r' || readByte == 0)
                            {
                                if (messageBuilder.Length > 0)
                                {
                                    string received = messageBuilder.ToString().Trim();
                                    Console.WriteLine($"Received: {received}");
                                    return received;
                                }
                                continue; // Skip empty terminators
                            }
                            
                            // Add the character to our message buffer
                            messageBuilder.Append((char)readByte);
                        }
                        
                        // Reset start time when we receive data to allow for message completion
                        startTime = DateTime.Now;
                    }
                    else
                    {
                        // Much shorter sleep when no data is available
                        if (messageBuilder.Length > 0) 
                        {
                            Thread.Sleep(10); // Very short wait when expecting more data
                        }
                        else 
                        {
                            Thread.Sleep(50); // Reduced from 1000ms to 50ms
                        }
                    }
                }

                // If we have collected any data but timed out before receiving a terminator
                if (messageBuilder.Length > 0)
                {
                    string received = messageBuilder.ToString().Trim();
                    Console.WriteLine($"Received (timeout): {received}");
                    return received;
                }

                return "";
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error receiving message: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// Alternative fast receive method using ReadLine (most efficient for line-based protocols)
        /// Use this if your protocol sends complete lines terminated with \n or \r\n
        /// </summary>
        /// <returns>Die empfangene Nachricht oder einen leeren String bei Timeout</returns>
        public string ReceiveMessageFast()
        {
            if (_uart == null || !_uart.IsOpen)
            {
                Console.WriteLine("Error: UART is not initialized or not open.");
                return string.Empty;
            }

            try
            {
                // Use the built-in ReadLine method which is much more efficient
                // It automatically handles line termination and buffering
                string received = _uart.ReadLine().Trim();
                Console.WriteLine($"Received: {received}");
                return received;
            }
            catch (TimeoutException)
            {
                // Normal timeout, no data received
                return "";
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error receiving message: {e.Message}");
                return "";
            }
        }

        public string SendAndReceive(string message)
        {
            if (SendMessage(message))
            {
                return ReceiveMessage();
            }
            else
            {
                Console.WriteLine("Error: Could not send message.");
                return string.Empty;
            }
        }

        /// <summary>
        /// Schliesst die UART-Verbindung
        /// </summary>
        public void Close()
        {
            Console.WriteLine("Closing communication...");
            try
            {
                if (_uart != null && _uart.IsOpen)
                {
                    _uart.Close();
                    Console.WriteLine("UART port closed.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error closing UART port: {e.Message}");
            }
        }

        public void SendPathToTinyK22(List<int> path)
        {
            Console.WriteLine("Sende Pfad-Array an TinyK22...");

            if (_uart == null || !_uart.IsOpen)
            {
                Console.WriteLine("Error: UART is not initialized or not open.");
                return;
            }
            try
            {
                // Konvertiere den Pfad in ein String-Format
                string pathString = string.Join(",", path);
                string message = $"path:{pathString}";

                // Sende die Nachricht
                if (SendMessage(message))
                {
                    Console.WriteLine("Pfad erfolgreich gesendet.");
                }
                else
                {
                    Console.WriteLine("Fehler beim Senden des Pfades.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error sending path: {e.Message}");
            }
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Close();
                    _uart?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}