using Windows.Devices.Enumeration;

namespace UwpApp.Models
{
    public partial class CustomHidDevice
    {
        public static DeviceWatcher CreateWatcher()
        {
            return DeviceInformation.CreateWatcher(DeviceSelector);
        }

        private DeviceWatcher _deviceWatcher;

        private bool DeviceWatcherStarted => _deviceWatcher != null &&
                                             (_deviceWatcher.Status == DeviceWatcherStatus.Started ||
                                              _deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted);

        private void RegisterForDeviceWatcherEvents()
        {
            _deviceWatcher.Added += OnDeviceWatcher_Added;
            _deviceWatcher.Removed += ODeviceWatcher_Removed;
        }

        private void UnRegisterFromDeviceWatcherEvents()
        {
            _deviceWatcher.Removed -= ODeviceWatcher_Removed;
            _deviceWatcher.Added -= OnDeviceWatcher_Added;
        }

        private void StartDeviceWatcher()
        {
            if (!DeviceWatcherStarted)
            {
                _deviceWatcher.Start();
            }
        }

        private void StopDeviceWatcher()
        {
            if (DeviceWatcherStarted)
            {
                _deviceWatcher.Stop();
            }
        }

        private void ODeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            if (IsDeviceConnected && args.Id == DeviceInformation.Id)
            {
                CloseCurrentlyConnectedDevice();
            }
        }

        private async void OnDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            if (DeviceInformation != null && args.Id == DeviceInformation.Id && !IsDeviceConnected)
            {
                var result = await InternalOpenDeviceAsync(DeviceInformation);
            }
        }
    }
}