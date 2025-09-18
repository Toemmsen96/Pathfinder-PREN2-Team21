using PathPlaning;
using RpiGPIO;
using UartCommunication;
using UARTCommunication;

namespace PfadfinderMain
{
    public class InputHandling
    {
        public static int selectedPath = -1; // -1 means no path selected
        public static bool isRunning = false; 
        public static void HandleInput(string input, ref int selectedPath)
        {
            switch (input)
            {
                case "Z": // Ziel button pressed
                    
                    selectedPath = (selectedPath + 1) % 3; // Cycle through paths A, B, C
                    Console.WriteLine($"Cycled to path {selectedPath}");
                    
                    if (!Program.getIsDummyRun())
                    {
                        GPIO.buzzer.TurnOn(3000);
                        Thread.Sleep(50);
                        GPIO.buzzer.TurnOff();
                        GPIO.CycleLEDsTo(selectedPath);
                    }
                    else
                    {
                        Console.WriteLine($"LED {(char)('A' + selectedPath)} eingeschaltet");
                    }
                    break;

                case "X": // Extra button pressed
                    Console.WriteLine("X-Taste gedrückt - Programm wird neu gestartet...");
                    if (!Program.getIsDummyRun())
                    {
                        GPIO.buzzer.TurnOn(3000);
                        Thread.Sleep(100);
                        GPIO.buzzer.TurnOff();
                        Thread.Sleep(50);
                        GPIO.buzzer.TurnOn(5000);
                        Thread.Sleep(100);
                        GPIO.buzzer.TurnOff();
                    }
                    Program.RestartProgram();
                    break;

                case "E": // Enter button pressed
                    if (selectedPath >= 0)
                    {
                        Program.timestamp = DateTime.Now; // Reset timestamp for new path calculation
                        if (isRunning)
                        {
                            Console.WriteLine("Pfadberechnung läuft bereits! Setze zurück.");
                            Program.SoftReset();
                            return;
                        }
                        Console.WriteLine($"Starte Pfadberechnung nach {(char)('A' + selectedPath)}");
                        Console.WriteLine("Alle LEDs ausser der gewählten Pfad-LED blinken zweimal auf");
                        Console.WriteLine("Buzzer piept zwei mal");
                        

                        if (!Program.getIsDummyRun())
                        {
                            if (Program.MUSIC_MODE)
                            {
                                Program.musicThread.Interrupt();
                                Program.musicThread = new Thread(() => GPIO.music.PlayPopcorn());
                                Program.musicThread.Start();
                            }

                            // Make other LEDs blink twice
                            if (selectedPath == 0) // Path A selected
                            {
                                // Blink B and C LEDs
                                for (int i = 0; i < 2; i++)
                                {
                                    GPIO.ledB.TurnOn();
                                    GPIO.ledC.TurnOn();
                                    GPIO.buzzer.TurnOn(4000);
                                    Thread.Sleep(200);
                                    GPIO.ledB.TurnOff();
                                    GPIO.ledC.TurnOff();
                                    GPIO.buzzer.TurnOff();
                                    Thread.Sleep(200);
                                }
                            }
                            else if (selectedPath == 1) // Path B selected
                            {
                                // Blink A and C LEDs
                                for (int i = 0; i < 2; i++)
                                {
                                    GPIO.ledA.TurnOn();
                                    GPIO.ledC.TurnOn();
                                    GPIO.buzzer.TurnOn(4000);
                                    Thread.Sleep(200);
                                    GPIO.ledA.TurnOff();
                                    GPIO.ledC.TurnOff();
                                    GPIO.buzzer.TurnOff();
                                    Thread.Sleep(200);
                                }
                            }
                            else if (selectedPath == 2) // Path C selected
                            {
                                // Blink A and B LEDs
                                for (int i = 0; i < 2; i++)
                                {
                                    GPIO.ledA.TurnOn();
                                    GPIO.ledB.TurnOn();
                                    GPIO.buzzer.TurnOn(4000);
                                    Thread.Sleep(200);
                                    GPIO.ledA.TurnOff();
                                    GPIO.ledB.TurnOff();
                                    GPIO.buzzer.TurnOff();
                                    Thread.Sleep(200);
                                }
                            }

                        }

                        // Take initial picture
                        bool pictureSuccess = Program.ExecuteSafely(() => {
                            ImageRecognition.TakePicture.CaptureImage();
                        }, "Bildaufnahme");
                        
                        if (!pictureSuccess)
                        {
                            Console.WriteLine("Bildaufnahme fehlgeschlagen. Pfadberechnung abgebrochen.");
                            isRunning = false;
                            Program.SoftReset();
                            return;
                        }

                        // Process the image separately for better error handling
                        bool processingSuccess = Program.ExecuteSafely(() => {
                            ImageRecognition.TakePicture.DetectAndSave("imgrec/startimage.jpg");
                        }, "Bildverarbeitung");
                        
                        if (!processingSuccess)
                        {
                            Console.WriteLine("Bildverarbeitung fehlgeschlagen. Pfadberechnung abgebrochen.");
                            Console.WriteLine("Überprüfe, ob alle erforderlichen Verzeichnisse und Dateien vorhanden sind.");
                            isRunning = false;
                            Program.SoftReset();
                            return;
                        }

                        // Verify that the processing created the expected output file
                        string expectedOutputFile = "imgrec/output/final/startimage_detected.json";
                        if (!File.Exists(expectedOutputFile))
                        {
                            Console.WriteLine($"Bildverarbeitung unvollständig: Ausgabedatei {expectedOutputFile} nicht gefunden.");
                            Console.WriteLine("Möglicherweise wurden keine Objekte erkannt oder die Verarbeitung ist fehlgeschlagen.");
                            isRunning = false;
                            Program.SoftReset();
                            return;
                        }

                        Console.WriteLine("✓ Bildverarbeitung erfolgreich abgeschlossen.");
                        if (Program.MUSIC_MODE){
                            Program.musicThread.Interrupt();
                        }


                        isRunning = true;
                        Console.WriteLine("Starte Pfadberechnung...");
                        
                        List<int>? path = null;
                        int currentSelectedPath = selectedPath; // Copy to avoid ref parameter issue
                        bool pathSuccess = Program.ExecuteSafely(() => {
                            path = PathPlaner.ComputePath("imgrec/output/final/startimage_detected.json", "Start", ((char)('A' + currentSelectedPath)).ToString());
                        }, "Pfadberechnung");
                        
                        if (!pathSuccess || path == null || path.Count == 0)
                        {
                            Console.WriteLine("Pfadberechnung fehlgeschlagen oder kein Pfad gefunden!");
                            Console.WriteLine("Programm bleibt bereit für neue Eingaben.");
                            isRunning = false;
                            if (Program.MUSIC_MODE)
                            {
                                Program.musicThread.Interrupt();
                                Program.musicThread = new Thread(() => GPIO.music.PlayRTTTL007());
                                Program.musicThread.Start();
                            }
                            
                            // Use soft reset instead of hard reset
                            Program.SoftReset();
                            return;
                        }
                        Console.WriteLine($"Pfad: {string.Join(", ", path)}");
                        while (!Program.isTinyKReady)
                        {
                            Console.WriteLine("Warte auf TinyK22...");
                            Thread.Sleep(100);
                        }
                        Program.messageHandler.RegisterCommand("pathrcv", () =>
                        {
                            Console.WriteLine("Pfad erfolgreich empfangen!");
                            if (!Program.getIsDummyRun())
                            {
                                // Play a chirp sound to indicate successful path reception
                                GPIO.buzzer.TurnOn(5000);
                                Thread.Sleep(50);
                                GPIO.buzzer.TurnOff();
                                Thread.Sleep(50);
                                GPIO.buzzer.TurnOn(6000);
                                Thread.Sleep(50);
                                GPIO.buzzer.TurnOff();
                                Thread.Sleep(50);
                                GPIO.buzzer.TurnOn(7000);
                                Thread.Sleep(70);
                                GPIO.buzzer.TurnOff();
                                Program.uart.SendMessage("start");
                                if (Program.MUSIC_MODE)
                                {
                                    Program.musicThread.Interrupt();
                                    Program.musicThread = new Thread(() => GPIO.music.PlayBennyHill());
                                    Program.musicThread.Start();
                                }
                            }
                        });
                        Program.messageHandler.RegisterCommand("reached_end", () =>
                        {
                            Console.WriteLine("Ziel erreicht!");
                            DateTime endTime = DateTime.Now;
                            TimeSpan duration = endTime - Program.timestamp;
                            Console.WriteLine($"Pfadberechnung dauerte: {duration.TotalSeconds} Sekunden");
                            Console.WriteLine("Pfadberechnung abgeschlossen.");
                            isRunning = false;
                            
                            if (!Program.getIsDummyRun())
                            {
                                new Thread(() =>
                                    {
                                        GPIO.ledA.TurnOff();
                                        GPIO.ledB.TurnOff();
                                        GPIO.ledC.TurnOff();
                                        GPIO.ledD.TurnOff();
                                        for (int sweep = 0; sweep < 20; sweep++)
                                        {
                                            GPIO.ledA.Toggle();
                                            Thread.Sleep(100);
                                            GPIO.ledB.Toggle();
                                            Thread.Sleep(100);
                                            GPIO.ledC.Toggle();
                                            Thread.Sleep(100);
                                            GPIO.ledD.Toggle();


                                            // Small pause between sweeps
                                            Thread.Sleep(150);
                                        }
                                    }).Start();
                                Program.musicThread.Interrupt();
                                GPIO.music.PlaySongOnce(Music.Song.SuccessMelody);
                            }
                        });
                        Thread sendPathThread = new Thread(() => {
                            Program.ExecuteSafely(() => {
                                Program.uart.SendPathToTinyK22(path);
                            }, "Pfad-Übertragung an TinyK22");
                        });
                        sendPathThread.IsBackground = true;
                        sendPathThread.Start();
                        
                    }
                    else
                    {
                        Console.WriteLine("Bitte erst einen Pfad (A, B oder C) auswählen!");
                    }
                    break;


                case "C": //Send UART command
                    Console.WriteLine("Geben Sie einen Text zum Senden ein:");
                    string messageToSend = Console.ReadLine() ?? string.Empty;
                    if (!string.IsNullOrEmpty(messageToSend))
                    {
                        Console.WriteLine($"Sende Nachricht: {messageToSend}");
                        Program.ExecuteSafely(() => {
                            Program.uart.SendMessage(messageToSend);
                        }, "UART-Nachricht senden");
                    }
                    else
                    {
                        Console.WriteLine("Leere Nachricht, nichts gesendet.");
                    }
                    break;
                default:
                    Console.WriteLine($"Unbekannte Eingabe: {input}");
                    break;
            }
        }


    }
}