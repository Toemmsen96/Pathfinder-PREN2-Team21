using System.CodeDom;
using Iot.Device.PiJuiceDevice.Models;
using Iot.Device.Tm16xx;
using RpiGPIO;

namespace PfadfinderMain
{
    internal class GPIO
    {
        public const int BUTTON_EXTRA_PIN = 2;
        public const int BUTTON_ENTER_PIN = 3;
        public const int BUTTON_ZIEL_PIN = 4;

        public const int LED_A_PIN = 24;
        public const int LED_B_PIN = 22;
        public const int LED_C_PIN = 23;
        public const int LED_D_PIN = 27;

        public const int BUZZER_PIN = 18;

        public static Dictionary<string, int> LEDs = new Dictionary<string, int>
        {
            { "LED_A", 0 },
            { "LED_B", 1 },
            { "LED_C", 2 },
            { "LED_D", 3 }
        };

        internal static Buzzer buzzer = null!;
        internal static Music music = null!;
        internal static LED ledA = null!;
        internal static LED ledB = null!;
        internal static LED ledC = null!;
        internal static LED ledD = null!;
        internal static Button buttonExtra = null!;
        internal static Button buttonEnter = null!;
        internal static Button buttonZiel = null!;
        internal static RpiGPIOController rpiGpioController = null!;


        public static void CleanupResources()
        {
            Console.WriteLine("Cleaning up resources...");
            if (rpiGpioController == null)
            {
                Console.WriteLine("GPIO controller is already disposed or running DUMMY mode.");
                return;
            }
            // Turn off all LEDs
            ledA.TurnOff();
            ledB.TurnOff();
            ledC.TurnOff();
            ledD.TurnOff();

            // Stop buzzer if it's active
            buzzer.TurnOff();

            // Dispose GPIO controller
            rpiGpioController.Dispose();

            Console.WriteLine("Cleanup completed.");
        }

        internal static void InitGpio()
        {
            Console.WriteLine("Initializing GPIO...");

            if (Program.getIsDummyRun())
            {
                Console.WriteLine("Exiting GPIO initialization.");
                Console.WriteLine("Running in dummy mode. GPIO will not be initialized.");
                return;
            }


            rpiGpioController = new RpiGPIOController();

            buzzer = new Buzzer(BUZZER_PIN, rpiGpioController);
            music = new Music(buzzer);
            ledA = new LED(LED_A_PIN, rpiGpioController);
            ledB = new LED(LED_B_PIN, rpiGpioController);
            ledC = new LED(LED_C_PIN, rpiGpioController);
            ledD = new LED(LED_D_PIN, rpiGpioController);
            buttonExtra = new Button(BUTTON_EXTRA_PIN, rpiGpioController);
            buttonEnter = new Button(BUTTON_ENTER_PIN, rpiGpioController);
            buttonZiel = new Button(BUTTON_ZIEL_PIN, rpiGpioController);
            ledA.TurnOff();
            ledB.TurnOff();
            ledC.TurnOff();
            ledD.TurnOff();

            Console.WriteLine("GPIO initialized.");
        }

        internal static void CycleLEDsTo(int led)
        {
            if (Program.getIsDummyRun())
            {
                Console.WriteLine($"Cycling LEDs to {led}");
                return;
            }

            // Turn off all LEDs
            ledA.TurnOff();
            ledB.TurnOff();
            ledC.TurnOff();
            ledD.TurnOff();

            // Turn on the specified LED
            switch (led)
            {
                case 0: // LEDs["LED_A"]
                    ledA.TurnOn();
                    break;
                case 1: // LEDs["LED_B"]
                    ledB.TurnOn();
                    break;
                case 2: // LEDs["LED_C"]
                    ledC.TurnOn();
                    break;
                case 3: // LEDs["LED_D"]
                    ledD.TurnOn();
                    break;
                default:
                    Console.WriteLine("Invalid LED number.");
                    break;
            }
        }
    }
}