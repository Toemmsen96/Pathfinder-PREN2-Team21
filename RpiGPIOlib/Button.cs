using System.Device.Gpio;

namespace RpiGPIO
{
    public class Button
    {
        private readonly int _pin;
        private readonly GpioController _controller;

        public Button(int pin)
        {
            _pin = pin;
            _controller = new GpioController();
            _controller.OpenPin(_pin, PinMode.InputPullUp);
        }
        public Button(int pin, RpiGPIOController controller)
        {
            _pin = pin;
            _controller = controller.GetGpioController();
            controller.OpenPin(_pin, PinMode.InputPullUp);
        }

        public bool IsPressed()
        {
            return _controller.Read(_pin) == PinValue.Low;
        }

        public int GetPin()
        {
            return _pin;
        }
        public void Dispose()
        {
            _controller.ClosePin(_pin);
        }
    }
}