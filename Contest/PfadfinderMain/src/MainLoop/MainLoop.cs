using System.Security.Cryptography.X509Certificates;
using Iot.Device.Nmea0183.Sentences;
using RpiGPIO;
using UartCommunication;

namespace PfadfinderMain
{
    partial class Program
    {

        public static Thread musicThread = new Thread(() => GPIO.music.PlayImperialMarch());
        public static string? receivedMessage = null; // Variable to store the received message from UART
        public static MessageHandler messageHandler = new MessageHandler(); // Message handler for command routing
        public static bool isTinyKReady = false; // Flag to indicate if TinyK is ready
        public static DateTime timestamp;
        private static void RunMainLoop(CancellationToken cancellationToken)
        {
            // Path selection state
            int selectedPath = -1; // -1 means no path selected
            const int TIMEOUT = 100000; // 100 seconds timeout for path selection

            Thread statusLedThread = new Thread(() => GPIO.ledD.Blink(TIMEOUT, 200)); // Blink LED D

            int nodeCount = 0; // Counter for nodes

            messageHandler.RegisterCommand("ready", () =>
            {
                isTinyKReady = true;
                Console.WriteLine("TinyK ist bereit.");
                statusLedThread = new Thread(() => GPIO.ledD.Blink(TIMEOUT, 400)); // Signal readiness with LED D
                statusLedThread.Start();
            });

            messageHandler.RegisterCommand("success", () =>
            {
                Console.WriteLine("Erfolgsmeldung empfangen.");
                if (!DUMMY_MODE)
                {
                    GPIO.music.PlayBombTicking();
                }
            });

            messageHandler.RegisterCommand("node", () =>
            {
                nodeCount++;
                Console.WriteLine($"Knoten empfangen: {nodeCount}");
            });

            messageHandler.RegisterCommand("follow_line", () =>
            {
                Console.WriteLine("Linie wird verfolgt.");
            });

            messageHandler.RegisterCommand("lost_line", () =>
            {
                Console.WriteLine("Linie verloren - Führe Soft-Reset durch.");
                if (!DUMMY_MODE)
                {
                    // Create "womp womp" sound effect with two descending tones
                    GPIO.buzzer.TurnOn(1000); // First tone (higher)
                    Thread.Sleep(300);
                    GPIO.buzzer.TurnOff();
                    Thread.Sleep(150);
                    GPIO.buzzer.TurnOn(600);  // Second tone (lower)
                    Thread.Sleep(600);
                    GPIO.buzzer.TurnOff();
                }
                SoftReset(); // Use soft reset instead of hard reset
                statusLedThread = new Thread(() => GPIO.ledD.Blink(TIMEOUT, 1000)); // Blink LED D to indicate lost line
                statusLedThread.Start();
            });

            messageHandler.RegisterCommand("path_error", () =>
            {
                Console.WriteLine("Pfadfehler - Führe Soft-Reset durch.");
                if (!DUMMY_MODE)
                {
                    // Create "womp womp" sound effect with two descending tones
                    GPIO.buzzer.TurnOn(1000); // First tone (higher)
                    Thread.Sleep(300);
                    GPIO.buzzer.TurnOff();
                    Thread.Sleep(150);
                    GPIO.buzzer.TurnOn(600);  // Second tone (lower)
                    Thread.Sleep(600);
                    GPIO.buzzer.TurnOff();
                }
                SoftReset(); // Use soft reset instead of hard reset
            });

            messageHandler.RegisterCommand("obstacle", () =>
            {
                Console.WriteLine("Hindernis erkannt.");
            });

            try
            {
                Console.WriteLine("Hauptprogrammschleife gestartet. Drücken Sie Ctrl+C zum Beenden.");
                statusLedThread.Start();
                // Record the start time for timeout tracking
                DateTime startTime = DateTime.Now;
                Console.WriteLine($"Start time: {startTime:HH:mm:ss}");

                Thread receiveUartThread = new Thread(() =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        receivedMessage = uart.ReceiveMessage();
                        messageHandler.ProcessMessage(receivedMessage);
                        // Removed unnecessary sleep - ReceiveMessage already has timeout handling
                        // Thread.Sleep(1000); // This was causing significant delays
                    }
                });
                receiveUartThread.IsBackground = true;
                receiveUartThread.Start();

                // Check for timeout during path selection
                void CheckTimeout()
                {
                    if (selectedPath == -1 && (DateTime.Now - startTime).TotalMilliseconds > TIMEOUT)
                    {
                        Console.WriteLine("Timeout: No path selected within time limit.");
                        if (programCts != null && !programCts.IsCancellationRequested)
                        {
                            if (statusLedThread.IsAlive)
                            {
                                statusLedThread.Interrupt();
                            }

                            programCts.Cancel();
                        }
                    }
                }

                // Start a background thread to check for timeout
                Thread timeoutThread = new Thread(() =>
                {
                    while (!cancellationToken.IsCancellationRequested && selectedPath == -1)
                    {
                        CheckTimeout();
                        Thread.Sleep(1000); // Check every second
                    }
                });
                timeoutThread.IsBackground = true;
                timeoutThread.Start();
                // Register for console cancellation (Ctrl+C)
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true; // Prevent immediate termination
                    if (programCts != null && !programCts.IsCancellationRequested)
                    {
                        Console.WriteLine("Programm wird beendet...");
                        // Stop the status LED thread
                        statusLedThread.Interrupt();
                        programCts.Cancel();
                    }
                };

                // Main loop logic
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();

                        // Check button inputs
                        if (DUMMY_MODE)
                        {
                            // In dummy mode, read from console for testing
                            try
                            {
                                if (Console.KeyAvailable)
                                {
                                    var key = Console.ReadKey(true).KeyChar.ToString().ToUpper();

                                    // Special case for ESC key - exit program
                                    if (key == ((char)27).ToString())
                                    {
                                        Console.WriteLine("ESC gedrückt - Programm wird beendet");
                                        programCts?.Cancel();
                                        break;
                                    }

                                    InputHandling.HandleInput(key, ref selectedPath);
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                // Ignore console input errors when console is redirected or not available
                                // This can happen when running as a service or with redirected input
                            }
                        }
                        else
                        {
                            try
                            {
                                if (Console.KeyAvailable)
                                {
                                    var key = Console.ReadKey(true).KeyChar.ToString().ToUpper();

                                    // Special case for ESC key - exit program
                                    if (key == ((char)27).ToString())
                                    {
                                        Console.WriteLine("ESC gedrückt - Programm wird beendet");
                                        programCts?.Cancel();
                                        break;
                                    }

                                    InputHandling.HandleInput(key, ref selectedPath);
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                // Ignore console input errors when console is redirected or not available
                                // This can happen when running as a service or with redirected input
                            }

                            if (GPIO.buttonZiel.IsPressed())
                            {
                                InputHandling.HandleInput("Z", ref selectedPath);
                                Thread.Sleep(100); // Simple debounce
                            }
                            else if (GPIO.buttonExtra.IsPressed())
                            {
                                InputHandling.HandleInput("X", ref selectedPath);
                                Thread.Sleep(100);
                            }
                            else if (GPIO.buttonEnter.IsPressed())
                            {
                                InputHandling.HandleInput("E", ref selectedPath);
                                Thread.Sleep(100);
                            }

                        }

                        // Small delay to prevent CPU hogging
                        Thread.Sleep(50);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Operation abgebrochen.");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler in der Hauptschleife: {ex.Message}");
            }
            finally
            {
                // Perform cleanup when exiting the loop
                if (!cleanupDone)
                {
                    Console.WriteLine("Programmende. Ressourcen werden freigegeben...");
                    GPIO.CleanupResources();
                    cleanupDone = true;
                    if (isResetting)
                    {
                        Console.WriteLine("Programm wurde zurückgesetzt.");
                        isResetting = false;
                        new Thread(() => FreshRun(DUMMY_MODE)).Start(); // Restart the program
                    }
                    else
                    {
                        Console.WriteLine("Programm beendet.");
                    }
                }
            }
        }
    }
}