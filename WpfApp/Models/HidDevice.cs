using HidLibrary;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using WpfApp.Helpers;

namespace WpfApp.Models
{
    public class HidDevice : Observable
    {
        public string DevicePath { get; }

        public event EventHandler<byte[]> ReceivedData;

        protected virtual void OnReceivedData(byte[] data)
        {
            ReceivedData?.Invoke(this, data);
        }

        private IHidDevice _hidReadWriteFeatureDataDevice;
        private IHidDevice _hidInterruptedReadDevice;

        public Task<bool> WriteAsync(byte[] data)
        {
            throw new NotSupportedException();
        }

        public bool Write(byte[] data)
        {
            var featureData = new byte[_hidReadWriteFeatureDataDevice.Capabilities.FeatureReportByteLength];
            Array.Copy(data, 0, featureData, 1,
                _hidReadWriteFeatureDataDevice.Capabilities.FeatureReportByteLength - 1);
            return _hidReadWriteFeatureDataDevice.WriteFeatureData(featureData);
        }

        public bool Read(out byte[] data)
        {
            var result = _hidReadWriteFeatureDataDevice.ReadFeatureData(out var featureData);
            if (result)
            {
                data = new byte[_hidReadWriteFeatureDataDevice.Capabilities.FeatureReportByteLength - 1];
                Array.Copy(featureData, 1, data, 0, Math.Min(featureData.Length, data.Length));
            }
            else
            {
                data = null;
            }

            return result;
        }

        private bool _isConnected;

        public bool IsConnected
        {
            get => _isConnected;
            private set => Set(ref _isConnected, value);
        }

        private TaskCompletionSource<byte[]> _waitResultDataCompletionSource;

        public async Task<bool> ConnectAsync()
        {
            var ret = false;
            try
            {
                // Following code is getting the same device interface for usage
                _hidReadWriteFeatureDataDevice = HidDevices.GetDevice(DevicePath);
                _hidInterruptedReadDevice = HidDevices.GetDevice(DevicePath);

                if (_hidReadWriteFeatureDataDevice == null)
                {
                    throw new ArgumentNullException($"{_hidReadWriteFeatureDataDevice}", "Can't find target device");
                }

                if (_hidInterruptedReadDevice == null)
                {
                    throw new ArgumentNullException($"{_hidInterruptedReadDevice}", "Can't find target device");
                }

                InitFeatureDataDevice();

                InitInterruptedReadDevice();

                _waitResultDataCompletionSource = new TaskCompletionSource<byte[]>();
                var getDeviceInfoCmd = new byte[] {0xB6, 0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                Write(getDeviceInfoCmd);
                byte[] deviceInfoData;
                var dueTime = (int) TimeSpan.FromSeconds(5).TotalMilliseconds;
                using (new Timer(ConnectTimeoutHandler,
                    _waitResultDataCompletionSource,
                    dueTime,
                    Timeout.Infinite))
                {
                    deviceInfoData = await _waitResultDataCompletionSource.Task;
                    _waitResultDataCompletionSource = null;
                }

                if (deviceInfoData != null)
                {
                    ret = true;
                }
                else
                {
                    Disconnect();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Disconnect();
            }

            return await Task.FromResult(ret);
        }

        public bool Connect()
        {
            var ret = false;
            try
            {
                _hidReadWriteFeatureDataDevice = HidDevices.GetDevice(DevicePath);
                _hidInterruptedReadDevice = HidDevices.GetDevice(DevicePath);

                if (_hidReadWriteFeatureDataDevice == null)
                {
                    throw new ArgumentNullException($"{_hidReadWriteFeatureDataDevice}", "Can't find target device");
                }

                if (_hidInterruptedReadDevice == null)
                {
                    throw new ArgumentNullException($"{_hidInterruptedReadDevice}", "Can't find target device");
                }

                InitFeatureDataDevice();

                InitInterruptedReadDevice();

                _waitResultDataCompletionSource = new TaskCompletionSource<byte[]>();
                var getDeviceInfoCmd = new byte[] {0xB6, 0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
                Write(getDeviceInfoCmd);
                byte[] deviceInfoData;
                var dueTime = (int) TimeSpan.FromSeconds(5).TotalMilliseconds;
                using (new Timer(ConnectTimeoutHandler,
                    _waitResultDataCompletionSource,
                    dueTime,
                    Timeout.Infinite))
                {
                    _waitResultDataCompletionSource.Task.Wait();
                    deviceInfoData = _waitResultDataCompletionSource.Task.Result;
                    _waitResultDataCompletionSource = null;
                }

                if (deviceInfoData != null)
                {
                    ret = true;
                }
                else
                {
                    Disconnect();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Disconnect();
            }

            return ret;
        }

        private void InitInterruptedReadDevice()
        {
            if (_hidInterruptedReadDevice != null)
            {
                _hidInterruptedReadDevice?.OpenDevice(
                    DeviceMode.Overlapped,
                    DeviceMode.NonOverlapped,
                    ShareMode.ShareRead | ShareMode.ShareWrite);

                _hidInterruptedReadDevice?.Read(OnRead);
            }
        }

        private void InitFeatureDataDevice()
        {
            if (_hidInterruptedReadDevice != null)
            {
                _hidReadWriteFeatureDataDevice.OpenDevice(
                    DeviceMode.Overlapped,
                    DeviceMode.NonOverlapped,
                    ShareMode.ShareRead | ShareMode.ShareWrite);
                _hidReadWriteFeatureDataDevice.MonitorDeviceEvents = true;
                _hidReadWriteFeatureDataDevice.Inserted += OnDevice_Inserted;
                _hidReadWriteFeatureDataDevice.Removed += OnDevice_Removed;
            }
        }

        public void Disconnect()
        {
            if (_hidReadWriteFeatureDataDevice != null)
            {
                _hidReadWriteFeatureDataDevice.Removed -= OnDevice_Removed;
                _hidReadWriteFeatureDataDevice.Inserted -= OnDevice_Inserted;
                _hidReadWriteFeatureDataDevice.CloseDevice();
                _hidReadWriteFeatureDataDevice.Dispose();
                _hidReadWriteFeatureDataDevice = null;
            }

            if (_hidInterruptedReadDevice != null)
            {
                _hidInterruptedReadDevice.CloseDevice();
                _hidInterruptedReadDevice.Dispose();
                _hidInterruptedReadDevice = null;
            }

            OnDevice_Removed();
        }

        public HidDevice(string devicePath)
        {
            DevicePath = devicePath;
        }

        private void OnDevice_Removed()
        {
            IsConnected = false;
        }

        private void OnDevice_Inserted()
        {
            IsConnected = true;
        }

        private void OnRead(HidDeviceData data)
        {
            try
            {
                if (_hidInterruptedReadDevice == null || !_hidInterruptedReadDevice.IsConnected)
                {
                    return;
                }

                _hidInterruptedReadDevice?.Read(OnRead);

                if (data.Data.Length > 1)
                {
                    var interruptedPackageData = new byte[data.Data.Length - 1];
                    Array.Copy(data.Data, 1, interruptedPackageData, 0, data.Data.Length - 1);
                    if (_waitResultDataCompletionSource != null &&
                        interruptedPackageData[0] == 0xB5 &&
                        interruptedPackageData[2] == 0x31)
                    {
                        var deviceInfoData = new byte[interruptedPackageData[1] - 1];
                        Array.Copy(interruptedPackageData, 3, deviceInfoData, 0, deviceInfoData.Length);
                        _waitResultDataCompletionSource.SetResult(deviceInfoData);
                    }
                    else
                    {
                        Task.Run(() => { OnReceivedData(interruptedPackageData); });
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private static string ByteArrayToHexString(byte[] array)
        {
            return array != null
                ? array.Aggregate(string.Empty, (current, b) => current + $"{b:X2}")
                : "Empty array";
        }

        private static T ByteArrayToStructure<T>(byte[] aaa)
        {
            var pinnedData = GCHandle.Alloc(aaa, GCHandleType.Pinned);
            var target = Marshal.PtrToStructure<T>(pinnedData.AddrOfPinnedObject());
            pinnedData.Free();

            return target;
        }

        private static byte[] HexStringToByteArray(string hex)
        {
            return !string.IsNullOrEmpty(hex)
                ? Enumerable.Range(0, hex.Length - 1)
                    .Where(x => x % 2 == 0)
                    .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                    .ToArray()
                : new byte[0];
        }

        private static ulong HexStringToULong(string hexString)
        {
            var macByteData = HexStringToByteArray(hexString);
            Array.Reverse(macByteData, 0, macByteData.Length);
            var ulongMacByteData = new byte[8];
            Array.Copy(macByteData, 0, ulongMacByteData, 0, macByteData.Length);
            return BitConverter.ToUInt64(ulongMacByteData, 0);
        }

        private void ConnectTimeoutHandler(object state)
        {
            var taskCompletionSource = (TaskCompletionSource<byte[]>) state;
            taskCompletionSource?.TrySetException(new TimeoutException("Connect timeout!"));
        }
    }
}
