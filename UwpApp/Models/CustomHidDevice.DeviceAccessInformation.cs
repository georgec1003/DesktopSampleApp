using System;
using System.Diagnostics;
using Windows.Devices.Enumeration;

namespace UwpApp.Models
{
    public partial class CustomHidDevice
    {
        private DeviceAccessInformation _deviceAccessInformation;

        private void RegisterForDeviceAccessStatusChange()
        {
            if (_deviceAccessInformation == null)
            {
                _deviceAccessInformation = DeviceAccessInformation.CreateFromId(DeviceInformation.Id);
            }

            _deviceAccessInformation.AccessChanged += OnAccessInformation_AccessChanged;
        }

        private void UnRegisterFromDeviceAccessStatusChange()
        {
            _deviceAccessInformation.AccessChanged -= OnAccessInformation_AccessChanged;
        }

        private async void OnAccessInformation_AccessChanged(
            DeviceAccessInformation sender,
            DeviceAccessChangedEventArgs args)
        {
            try
            {
                if (args.Status == DeviceAccessStatus.DeniedBySystem
                    || args.Status == DeviceAccessStatus.DeniedByUser)
                {
                    CloseCurrentlyConnectedDevice();
                }
                else if (args.Status == DeviceAccessStatus.Allowed && DeviceInformation != null)
                {
                    var result = await InternalOpenDeviceAsync(DeviceInformation);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }
    }
}