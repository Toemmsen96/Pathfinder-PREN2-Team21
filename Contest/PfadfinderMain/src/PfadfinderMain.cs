using System;
using System.Drawing;
using System.Threading;
using RpiGPIO;
using UARTCommunication;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Linq;

namespace PfadfinderMain
{
    partial class Program
    {

        private static bool DUMMY_MODE = false; // Set to true for dummy run, false for real run
        internal static bool MUSIC_MODE = false; // Set to true for music mode, false for normal mode

        // State variables
        private static CancellationTokenSource? programCts = null;
        private static bool cleanupDone = false;

        private static bool isResetting = false;

        public static UartCommunicator uart = null!;

        // PID file management
        private static readonly string PID_FILE_PATH = "/tmp/pfadfinder_main.pid";
        private static readonly string RESTART_FLAG_FILE = "/tmp/pfadfinder_restart.flag";

        static void Main(string[] args)
        {
            // Check if another instance is running and handle restart logic
            if (!EnsureSingleInstance())
            {
                Console.WriteLine("Another instance is already running. Exiting.");
                return;
            }

            // Check if this is a restart
            bool isRestart = File.Exists(RESTART_FLAG_FILE);
            if (isRestart)
            {
                File.Delete(RESTART_FLAG_FILE);
                Console.WriteLine("Restarting fresh after X button press...");
                // Add a small delay to ensure previous instance has fully cleaned up
                Thread.Sleep(1000);
            }

            ParseArguments(args);
            using var interruptHandler = new InterruptHandling(CleanupAndExit);

            // Set up cancellation token for clean program termination
            programCts = new CancellationTokenSource();
            Console.WriteLine("Pfadfinder Hauptprogramm gestartet");
            Console.WriteLine("Dummy-Modus: " + (DUMMY_MODE ? "Aktiv" : "Inaktiv"));
            Console.WriteLine("Drücke Taste Ziel (Z), um den Pfad zu wählen.");
            Console.WriteLine("Drücke Taste Enter (E), um den Pfad zu starten.");
            Console.WriteLine("Drücke Taste Extra (X), um das Programm neu zu starten.");

            new Thread(() =>
            {
                ImageRecognition.TakePicture.SetUpPython();
            }).Start();
            
            GPIO.InitGpio();
            if (!DUMMY_MODE)
            {
                uart = new UartCommunicator(); 
                uart.Initialize();
            }            // TODO: Add logic to handle button presses and path selection
            // TODO: Add logic to handle path calculation and execution
            // TODO: Add logic to handle Uart communication

            // Run the main program loop with error recovery
            RunMainLoopWithRecovery(programCts.Token);

            Console.WriteLine("Programm beendet.");
        }

        /// <summary>
        /// Ensures only one instance of the program is running using a PID file
        /// </summary>
        /// <returns>True if this is the only instance, false otherwise</returns>
        private static bool EnsureSingleInstance()
        {
            try
            {
                if (File.Exists(PID_FILE_PATH))
                {
                    string pidStr = File.ReadAllText(PID_FILE_PATH).Trim();
                    if (int.TryParse(pidStr, out int existingPid))
                    {
                        try
                        {
                            // Check if the process is still running
                            Process existingProcess = Process.GetProcessById(existingPid);
                            if (existingProcess != null && !existingProcess.HasExited)
                            {
                                // Check if it's actually our process
                                string currentProcessName = Process.GetCurrentProcess().ProcessName;
                                if (existingProcess.ProcessName == currentProcessName)
                                {
                                    Console.WriteLine($"Another instance (PID: {existingPid}) is already running.");
                                    return false;
                                }
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Process doesn't exist anymore, we can continue
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error checking existing process: {ex.Message}");
                        }
                    }
                }

                // Write our PID to the file
                int currentPid = Process.GetCurrentProcess().Id;
                File.WriteAllText(PID_FILE_PATH, currentPid.ToString());
                Console.WriteLine($"PID file created: {PID_FILE_PATH} (PID: {currentPid})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error managing PID file: {ex.Message}");
                return true; // Continue anyway
            }
        }

        /// <summary>
        /// Cleanup resources and remove PID file
        /// </summary>
        private static void CleanupAndExit()
        {
            try
            {
                if (File.Exists(PID_FILE_PATH))
                {
                    File.Delete(PID_FILE_PATH);
                    Console.WriteLine("PID file removed.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing PID file: {ex.Message}");
            }
            
            GPIO.CleanupResources();
        }

        /// <summary>
        /// Restart the program fresh - called when X button is pressed
        /// </summary>
        public static void RestartProgram()
        {
            Console.WriteLine("Programm wird neu gestartet...");
            
            try
            {
                // Create restart flag file
                File.WriteAllText(RESTART_FLAG_FILE, DateTime.Now.ToString());
                
                // Get the current executable path
                string currentExecutable = Process.GetCurrentProcess().MainModule?.FileName ?? 
                                         Path.Combine(AppContext.BaseDirectory, Process.GetCurrentProcess().ProcessName);
                
                // Get command line arguments (excluding the executable name)
                string[] args = Environment.GetCommandLineArgs();
                string arguments = string.Join(" ", args.Skip(1));
                
                // Start new instance
                var startInfo = new ProcessStartInfo
                {
                    FileName = currentExecutable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                
                Console.WriteLine($"Starting new instance: {currentExecutable} {arguments}");
                Process.Start(startInfo);
                
                // Exit current instance
                cleanupDone = false;
                programCts?.Cancel();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Neustart: {ex.Message}");
                // Fallback to normal exit
                ExitProgram();
            }
        }

        private static void ParseArguments(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.Equals("dummy", StringComparison.OrdinalIgnoreCase))
                {
                    DUMMY_MODE = true;
                    Console.WriteLine("Dummy-Modus aktiviert.");
                }
                else if (arg.Equals("music", StringComparison.OrdinalIgnoreCase))
                {
                    MUSIC_MODE = true;
                    Console.WriteLine("Musik-Modus aktiviert.");
                }
            }
        }

        public static bool getIsDummyRun()
        {
            return DUMMY_MODE; // Set to true for dummy run, false for real run
        }

        public static void ResetProgram()
        {
            Console.WriteLine("Programm wird zurückgesetzt...");
            cleanupDone = false;
            isResetting = true;
            
            // Reset application state instead of canceling
            InputHandling.isRunning = false;
            InputHandling.selectedPath = -1;
            
            // Don't cancel the main token, just signal reset
            Console.WriteLine("Zustand zurückgesetzt. Bereit für neue Eingaben.");
        }

        public static void ExitProgram()
        {
            Console.WriteLine("Programm wird beendet...");
            cleanupDone = false;
            programCts?.Cancel();
            isResetting = false;
            Environment.Exit(0);
        }

        /// <summary>
        /// Soft reset - resets application state without restarting the process
        /// This keeps the main loop running so X button restart always works
        /// </summary>
        public static void SoftReset()
        {
            Console.WriteLine("Führe Soft-Reset durch...");
            
            // Reset application state
            InputHandling.isRunning = false;
            InputHandling.selectedPath = -1;
            isResetting = false;
            
            // Reset LEDs to initial state
            if (!DUMMY_MODE)
            {
                GPIO.ledA.TurnOff();
                GPIO.ledB.TurnOff();
                GPIO.ledC.TurnOff();
                GPIO.ledD.TurnOff();
                
                // Short feedback sound
                GPIO.buzzer.TurnOn(2000);
                Thread.Sleep(100);
                GPIO.buzzer.TurnOff();
            }
            
            Console.WriteLine("Soft-Reset abgeschlossen. Programm läuft weiter.");
            Console.WriteLine("Drücke Taste Ziel (Z), um den Pfad zu wählen.");
            Console.WriteLine("Drücke Taste Enter (E), um den Pfad zu starten.");
            Console.WriteLine("Drücke Taste Extra (X), um das Programm neu zu starten.");
        }

        public static void FreshRun(bool isDummy)
        {
            Console.WriteLine("Programm wird frisch gestartet...");
            cleanupDone = false;
            isResetting = false;
            DUMMY_MODE = isDummy;
            using var interruptHandler = new InterruptHandling(GPIO.CleanupResources);

            // Set up cancellation token for clean program termination
            programCts = new CancellationTokenSource();
            Console.WriteLine("Pfadfinder Hauptprogramm gestartet");
            Console.WriteLine("Dummy-Modus: " + (DUMMY_MODE ? "Aktiv" : "Inaktiv"));
            Console.WriteLine("Drücke Taste A, B oder C, um den Pfad zu wählen.");
            Console.WriteLine("Drücke Taste D, um den Pfad zu starten.");

            // Initialize GPIO
            GPIO.InitGpio();

            // Take initial picture
            ImageRecognition.TakePicture.CaptureImage();
            ImageRecognition.TakePicture.DetectAndSave("imgrec/startimage.jpg");


            // TODO: Add logic to handle button presses and path selection
            // TODO: Add logic to handle path calculation and execution
            // TODO: Add logic to handle Uart communication



            // Run the main program loop
            RunMainLoop(programCts.Token);
        }

        /// <summary>
        /// Runs the main loop with error recovery - keeps the program running even if errors occur
        /// This ensures the X button restart functionality always works
        /// </summary>
        private static void RunMainLoopWithRecovery(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine("Starte Hauptprogrammschleife...");
                    RunMainLoop(cancellationToken);
                    
                    // If we reach here and it's not a cancellation, something unexpected happened
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Hauptschleife beendet unerwartet. Starte neu in 3 Sekunden...");
                        Thread.Sleep(3000);
                        SoftReset();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    Console.WriteLine("Hauptschleife wurde abgebrochen.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unerwarteter Fehler in der Hauptschleife: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    Console.WriteLine("Programm wird in 5 Sekunden automatisch fortgesetzt...");
                    
                    // Play error sound if not in dummy mode
                    if (!DUMMY_MODE)
                    {
                        try
                        {
                            GPIO.buzzer.TurnOn(500); // Low error tone
                            Thread.Sleep(500);
                            GPIO.buzzer.TurnOff();
                        }
                        catch
                        {
                            // Ignore GPIO errors during error recovery
                        }
                    }
                    
                    Thread.Sleep(5000);
                    SoftReset();
                    
                    // Create new cancellation token source if the old one was disposed
                    if (programCts?.IsCancellationRequested == true)
                    {
                        programCts = new CancellationTokenSource();
                    }
                }
            }
        }

        /// <summary>
        /// Execute a critical operation with error handling
        /// If it fails, the program continues running instead of crashing
        /// </summary>
        public static bool ExecuteSafely(Action operation, string operationName)
        {
            try
            {
                operation();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei {operationName}: {ex.Message}");
                Console.WriteLine("Programm läuft weiter...");
                
                // Play error sound
                if (!DUMMY_MODE)
                {
                    try
                    {
                        GPIO.buzzer.TurnOn(800);
                        Thread.Sleep(200);
                        GPIO.buzzer.TurnOff();
                        Thread.Sleep(100);
                        GPIO.buzzer.TurnOn(800);
                        Thread.Sleep(200);
                        GPIO.buzzer.TurnOff();
                    }
                    catch
                    {
                        // Ignore GPIO errors during error recovery
                    }
                }
                
                return false;
            }
        }

        // ...existing code...
    }
}
        


/*
        private static void StartPath(int pathIndex)
        {
            Console.WriteLine($"Führe Pfad {(char)('A' + pathIndex)} aus...");

            // Take another picture if needed
            ImageRecognition.TakePicture.CaptureImage();
            ImageRecognition.TakePicture.DetectAndSave("imgrec/pathimage.jpg");

            // Path logic would go here
            // ...

            Console.WriteLine("Pfad abgeschlossen.");
        }
    }
}
*/