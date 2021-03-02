using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using UwpApp.Helpers;
using UwpApp.Models;

namespace UwpApp.ViewModels
{
    public class MainPageViewModel : Observable
    {
        private CustomHidDevice _device;
        private readonly CustomHidDeviceWatcher _deviceWatcher = new CustomHidDeviceWatcher();

        private string _deviceConnectStatus = "Disconnect";

        public string DeviceConnectStatus
        {
            get => _deviceConnectStatus;
            set => Set(ref _deviceConnectStatus, value);
        }


        public void Initialize()
        {
            _deviceWatcher.DeviceAdd += OnDeviceWatcher_DeviceAdd;
            _deviceWatcher.DeviceRemoved += OnDeviceWatcher_DeviceRemoved;
            _deviceWatcher.DeviceEnumerationComplete += OnDeviceWatcher_DeviceEnumerationComplete;
            _deviceWatcher.InitializeDeviceWatcher();
            _deviceWatcher.Start();
        }

        private void OnDeviceWatcher_DeviceEnumerationComplete(object sender, EventArgs e)
        {
        }

        private void OnDeviceWatcher_DeviceRemoved(object sender, Windows.Devices.Enumeration.DeviceInformationUpdate deviceInfo)
        {
            if (_device != null && deviceInfo.Id.Equals(_device.DeviceInformation.Id))
            {
                _device.CloseDevice();
                _device.Dispose();
                _device = null;
            }
        }

        private async void OnDeviceWatcher_DeviceAdd(object sender, Windows.Devices.Enumeration.DeviceInformation deviceInfo)
        {
            if (_device == null)
            {
                // The device possible first connected 
                await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        var device = await HidDevice.FromIdAsync(deviceInfo.Id, FileAccessMode.ReadWrite);

                        if (device != null)
                        {
                            device.Dispose();

                            await Task.Run(async () =>
                            {
                                _device = new CustomHidDevice(Application.Current);
                                _device.Connected += OnDevice_Connected; ;
                                var result = await _device.OpenDeviceAsync(deviceInfo);
                            });
                        }
                    });
            }
        }

        private async void OnDevice_Connected(object sender, bool connected)
        {
            await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    DeviceConnectStatus = connected ? "Connected" : "Disconnect";
                });
        }
    }
}
