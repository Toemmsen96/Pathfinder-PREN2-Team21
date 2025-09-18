using System.Device.Gpio;


namespace RpiGPIO
{
    /// <summary>
    /// This class manages the GPIO pins on a Raspberry Pi.
    /// It provides methods to open and close pins, and ensures that all resources are released properly.
    /// </summary>
    public class RpiGPIOController : IDisposable
    {
        private readonly GpioController _gpioController;
        private readonly HashSet<int> _openPins = new HashSet<int>();
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the RpiGPIOController class.
        /// </summary>
        public RpiGPIOController()
        {
            _gpioController = new GpioController();
        }

        /// <summary>
        /// Initializes the GPIO controller
        /// </summary>
        public void Initialize()
        {
            // Additional initialization logic can be added here if needed
        }

        public void OpenPin(int pinNumber, PinMode mode)
        {
            _gpioController.OpenPin(pinNumber, mode);
            _openPins.Add(pinNumber);
        }

        /// <summary>
        /// Closes all open pins and releases resources
        /// </summary>
        public void Cleanup()
        {
            if (_disposed)
                return;

            foreach (var pin in _openPins)
            {
                if (_gpioController.IsPinOpen(pin))
                {
                    _gpioController.ClosePin(pin);
                }
            }
            _openPins.Clear();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Cleanup();
                _gpioController.Dispose();
            }

            _disposed = true;
        }

        public GpioController GetGpioController()
        {
            return _gpioController;
        }

        ~RpiGPIOController()
        {
            Dispose(false);
        }
    }
}