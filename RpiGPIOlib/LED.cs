using System.Device.Gpio;

namespace RpiGPIO
{
    public class LED
    {
        private readonly int _pinNumber;
        private readonly GpioController _gpioController;
        private bool _isOn;

        public LED(int pinNumber, GpioController controller)
        {
            _pinNumber = pinNumber;
            _gpioController = controller;
            _gpioController.OpenPin(_pinNumber, PinMode.Output);
        }

        public LED(int pinNumber, RpiGPIOController controller)
        {
            _pinNumber = pinNumber;
            _gpioController = controller.GetGpioController();
            controller.OpenPin(_pinNumber, PinMode.Output);
        }

        public void TurnOn()
        {
            _gpioController.Write(_pinNumber, PinValue.High);
        }

        public void TurnOff()
        {
            _gpioController.Write(_pinNumber, PinValue.Low);
        }
        public void Toggle()
        {
            _isOn = !_isOn;
            _gpioController.Write(_pinNumber, _isOn ? PinValue.High : PinValue.Low);
        }
        public void Blink(int duration)
        {
            TurnOn();
            Thread.Sleep(duration);
            TurnOff();
        }
        public void Blink(int duration, int interval)
        {
            for (int i = 0; i < duration / interval; i++)
            {
                Toggle();
                Thread.Sleep(interval);
            }
        }
        public bool IsOn()
        {
            return _isOn;
        }
    }
}