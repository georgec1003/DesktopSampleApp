using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace UwpApp.Models
{
    public class CustomHidDeviceWatcher
    {
        public event EventHandler<DeviceInformation> DeviceAdd;
        public event EventHandler<DeviceInformationUpdate> DeviceRemoved;
        public event EventHandler DeviceEnumerationComplete;

        private DeviceWatcher _deviceWatcher;

        public bool Started => _deviceWatcher != null && _deviceWatcher.Status != DeviceWatcherStatus.Created && _deviceWatcher.Status != DeviceWatcherStatus.Started && _deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted;

        public bool AllDevicesEnumerated => _deviceWatcher != null && _deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted;

        public void InitializeDeviceWatcher()
        {
            var deviceSelector = HidDevice.GetDeviceSelector(
                CustomHidDevice.DeviceInfo.UsagePage,
                CustomHidDevice.DeviceInfo.UsageId,
                CustomHidDevice.DeviceInfo.Vid,
                CustomHidDevice.DeviceInfo.Pid);

            var deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);

            AddDeviceWatcher(deviceWatcher, deviceSelector);
        }

        private void AddDeviceWatcher(DeviceWatcher deviceWatcher, string deviceSelector)
        {
            deviceWatcher.Added += OnDeviceAdded;
            deviceWatcher.Removed += OnDeviceRemoved;
            deviceWatcher.EnumerationCompleted += OnDeviceEnumerationComplete;

            _deviceWatcher = deviceWatcher;
        }

        private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            NotifyDeviceAdded(deviceInformation);
        }

        private void NotifyDeviceAdded(DeviceInformation deviceInfo)
        {
            DeviceAdd?.Invoke(this, deviceInfo);
        }

        private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            NotifyDeviceRemoved(deviceInformationUpdate);
        }

        private void NotifyDeviceRemoved(DeviceInformationUpdate deviceInfoUpdated)
        {
            DeviceRemoved?.Invoke(this, deviceInfoUpdated);
        }

        private void OnDeviceEnumerationComplete(DeviceWatcher sender, object args)
        {
            NotifyDeviceEnumerationComplete();
        }

        private void NotifyDeviceEnumerationComplete()
        {
            DeviceEnumerationComplete?.Invoke(this, EventArgs.Empty);
        }

        public void Start()
        {
            // Start all device watchers
            if (!Started)
            {
                _deviceWatcher?.Start();
            }
        }

        public void Stop()
        {
            if (Started)
            {
                _deviceWatcher?.Stop();
            }
        }
    }

    
    public partial class CustomHidDevice : IDisposable
    {
        public class DeviceInfo
        {
            public const ushort Vid = 0x0A48;
            public const ushort Pid = 0x5678;
            public const ushort UsagePage = 0xFFAA;
            public const ushort UsageId = 0x01;
        }

        public class ControlPattern
        {
            public const ushort ReportId = 0x00;
            public const ushort UsagePage = 0x0001;
            public const ushort UsageId = 0x01;
        }

        private static string _deviceSelector = string.Empty;
        public static string DeviceSelector
        {
            get
            {
                if (string.IsNullOrEmpty(_deviceSelector))
                {
                    _deviceSelector = 
                        HidDevice.GetDeviceSelector(
                            DeviceInfo.UsagePage,
                            DeviceInfo.UsageId,
                            DeviceInfo.Vid,
                            DeviceInfo.Pid);
                }

                return _deviceSelector;
            }
        }

        public event EventHandler<bool> Connected;

        public bool IsDeviceConnected => _device != null;

        public DeviceInformation DeviceInformation;

        private HidDevice _device;
        private readonly Application _application;

        public CustomHidDevice(Application application)
        {
            _application = application;
        }

        public async Task<bool> OpenDeviceAsync(DeviceInformation deviceInformation)
        {
            var result = false;
            try
            {
                result = await InternalOpenDeviceAsync(deviceInformation);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

            if (result)
            {
                RegisterForAppEvents();

                RegisterForDeviceAccessStatusChange();

                // Create and register device watcher events for the device to be opened unless we're reopening the device
                if (_deviceWatcher == null)
                {
                    _deviceWatcher = DeviceInformation.CreateWatcher(DeviceSelector);

                    RegisterForDeviceWatcherEvents();
                }

                if (!DeviceWatcherStarted)
                {
                    // Start the device watcher after we made sure that the device is opened.
                    StartDeviceWatcher();
                }
            }

            return result;
        }

        public async Task<bool> InternalOpenDeviceAsync(DeviceInformation deviceInformation)
        {
            if (_device != null) throw new Exception();

            _device = await HidDevice.FromIdAsync(deviceInformation.Id, FileAccessMode.ReadWrite);

            if (_device != null)
            {
                DeviceInformation = deviceInformation;
                _device.InputReportReceived += OnDevice_InputReportReceived;
            }
            else
            {
                var deviceAccessStatus = DeviceAccessInformation.CreateFromId(deviceInformation.Id).CurrentStatus;

                if (deviceAccessStatus == DeviceAccessStatus.DeniedByUser)
                {
                }
                else if (deviceAccessStatus == DeviceAccessStatus.DeniedBySystem)
                {
                    // This status is most likely caused by app permissions (did not declare the device in the app's package.appxmanifest)
                    // This status does not cover the case where the device is already opened by another app.
                }
                else
                {
                    // Most likely the device is opened by another app, but cannot be sure
                }
            }

            OnConnected(_device != null);

            return _device != null;
        }

        public void CloseDevice()
        {
            CloseCurrentlyConnectedDevice();

            UnRegisterFromDeviceWatcherEvents();
            StopDeviceWatcher();
            _deviceWatcher = null;
            UnRegisterFromDeviceAccessStatusChange();
            UnRegisterFromAppEvents();
        }

        private void CloseCurrentlyConnectedDevice()
        {
            if (_device != null)
            {
                OnConnected(false);

                _device.InputReportReceived -= OnDevice_InputReportReceived;
                _device.Dispose();
                _device = null;
            }
        }

        private void OnDevice_InputReportReceived(HidDevice sender, HidInputReportReceivedEventArgs args)
        {
            Debug.WriteLine($"{args.Report.Id}");
        }

        protected virtual void OnConnected(bool e)
        {
            Connected?.Invoke(this, e);
        }

        public async Task WriteAsync(byte[] pattern)
        {
            var featureReport = _device.CreateFeatureReport(ControlPattern.ReportId);

            var dataWriter = new DataWriter();

            dataWriter.WriteByte((byte)ControlPattern.ReportId);
            dataWriter.WriteBytes(pattern);

            featureReport.Data = dataWriter.DetachBuffer();

            await _device.SendFeatureReportAsync(featureReport);
        }

        public async Task<byte[]> ReadAsync()
        {
            var featureReport = await _device.GetFeatureReportAsync(ControlPattern.ReportId);

            if (featureReport.Data.Length != 0)
            {
                var dataReader = DataReader.FromBuffer(featureReport.Data);

                dataReader.ReadByte(); // Skip report id
                var pattern = new byte[featureReport.Data.Length - 1];
                dataReader.ReadBytes(pattern);

                return pattern;
            }

            return null;
        }

        #region IDisposable Support
        private bool _disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    CloseDevice();
                    DeviceInformation = null;
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
