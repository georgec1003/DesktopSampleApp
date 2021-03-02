using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfApp.Models;

namespace WpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int TargetDeviceVid = 0x0A48;
        private const int TargetDevicePid = 0x5678;
        private const short TargetUsagePage = 0xffaa - 0x10000;

        private WpfApp.Models.HidDevice device = null;

        public MainWindow()
        {
            InitializeComponent();
            device = GetTargetDevice();
            if (device != null)
            {
                var result = device.Connect();
                DeviceStatus.Content = result ? "Connected" : "Disconnect";
            }
            else
            {
                DeviceStatus.Content = "Disconnect";
            }
        }

        private static WpfApp.Models.HidDevice GetTargetDevice()
        {
            foreach (var device in HidDevices.Enumerate(TargetDeviceVid, TargetDevicePid))
            {
                if (device.Capabilities?.UsagePage == TargetUsagePage)
                {
                    return new WpfApp.Models.HidDevice(device.DevicePath);
                }
            }

            return null;
        }

    }
}
