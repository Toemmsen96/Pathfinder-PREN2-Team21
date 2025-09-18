using System;
using System.Device.Gpio;
using System.Device.Pwm;
using System.Threading;
using System.Threading.Tasks;
using System.Device.Pwm.Drivers;

namespace RpiGPIO
{
    public enum BuzzerMode
    {
        SquareWave,
        SuperMario
    }

    public class Buzzer
    {
        private readonly int _pinNumber;
        private readonly GpioController _gpioController;
        private PwmChannel? _pwmChannel;
        private CancellationTokenSource? _buzzerTokenSource;

        // Super Mario theme notes and durations
        private static readonly (int frequency, int duration)[] MarioTheme = new[]
        {
            (660, 100), (0, 150), (660, 100), (0, 300), (660, 100), (0, 300), (510, 100), (0, 100),
            (660, 100), (0, 300), (770, 100), (0, 550), (380, 100), (0, 575),
            
            (510, 100), (0, 450), (380, 100), (0, 400), (320, 100), (0, 500), (440, 100), (0, 300),
            (480, 80), (0, 330), (450, 100), (0, 150), (430, 100), (0, 300), (380, 100), (0, 200),
            
            (660, 80), (0, 200), (760, 50), (0, 150), (860, 100), (0, 300), (700, 80), (0, 150),
            (760, 50), (0, 350), (660, 80), (0, 300), (520, 80), (0, 150), (580, 80), (0, 150),
            
            (480, 80), (0, 500)
        };


        public Buzzer(int pinNumber, GpioController controller)
        {
            _pinNumber = pinNumber;
            _gpioController = controller;
            
            // Initialize PWM using hardware PWM on pin 18 (PWM0)
            try
            {
                // Hardware PWM for pin 18
                _pwmChannel = PwmChannel.Create(0, 0, 1000, 0.5);  // Use chip 0, channel 0 (PWM0)
                Console.WriteLine("Hardware PWM initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize hardware PWM: {ex.Message}");
                Console.WriteLine("Falling back to software PWM");
                
                // Fallback to software PWM if hardware PWM fails
                _pwmChannel = new SoftwarePwmChannel(_pinNumber, 1000, 0.5);
                Console.WriteLine("Software PWM initialized as fallback");
            }
            
            // Start the PWM channel with initial duty cycle of 0 (off)
            _pwmChannel.Start();
            _pwmChannel.DutyCycle = 0;
        }

        public Buzzer(int pinNumber, RpiGPIOController controller)
            : this(pinNumber, controller.GetGpioController())
        {
        }

        public void TurnOn(double frequencyHz)
        {
            // Stop any existing buzzer operation
            TurnOff();

            // Set the PWM frequency and 50% duty cycle for square wave
            if (_pwmChannel != null)
            {
                _pwmChannel.Frequency = (int)frequencyHz;
                _pwmChannel.DutyCycle = 0.5; // 50% duty cycle for square wave
                
                //Console.WriteLine($"PWM started at {frequencyHz} Hz with 50% duty cycle");
            }
        }

        public void PlaySuperMarioTheme()
        {
            // Stop any existing buzzer operation
            TurnOff();

            _buzzerTokenSource = new CancellationTokenSource();
            var token = _buzzerTokenSource.Token;

            Task.Run(() => 
            {
                try 
                {
                    while (!token.IsCancellationRequested)
                    {
                        // Play through all the notes in the theme
                        foreach (var (frequency, duration) in MarioTheme)
                        {
                            if (token.IsCancellationRequested) break;
                            
                            if (_pwmChannel != null)
                            {
                                if (frequency > 0)
                                {
                                    // Set frequency and duty cycle for this note
                                    _pwmChannel.Frequency = frequency;
                                    _pwmChannel.DutyCycle = 0.5;
                                    Thread.Sleep(duration);
                                }
                                else
                                {
                                    // This is a rest (pause)
                                    _pwmChannel.DutyCycle = 0;
                                    Thread.Sleep(duration);
                                }
                            }
                        }
                        
                        // Short pause before repeating
                        if (!token.IsCancellationRequested && _pwmChannel != null)
                        {
                            _pwmChannel.DutyCycle = 0;
                            Thread.Sleep(1000);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Task canceled, ensure PWM is off
                    if (_pwmChannel != null)
                    {
                        _pwmChannel.DutyCycle = 0;
                    }
                }
            }, token);
        }

        public void TurnOff()
        {
            if (_buzzerTokenSource != null)
            {
                _buzzerTokenSource.Cancel();
                _buzzerTokenSource.Dispose();
                _buzzerTokenSource = null;
            }
            
            if (_pwmChannel != null)
            {
                _pwmChannel.DutyCycle = 0;
            }
        }

        public void Cleanup()
        {
            TurnOff();
            
            if (_pwmChannel != null)
            {
                _pwmChannel.Stop();
                _pwmChannel.Dispose();
                _pwmChannel = null;
            }
        }
    }
}