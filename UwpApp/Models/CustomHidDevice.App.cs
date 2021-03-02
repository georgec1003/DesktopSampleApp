using Windows.ApplicationModel;

namespace UwpApp.Models
{
    public partial class CustomHidDevice
    {
        private bool _watcherSuspended;

        private void RegisterForAppEvents()
        {
            if (_application != null)
            {
                _application.Suspending += OnApplication_Suspension;
                _application.Resuming += OnApplication_Resume;
            }
        }

        private void UnRegisterFromAppEvents()
        {
            if (_application != null)
            {
                _application.Resuming -= OnApplication_Resume;
                _application.Suspending -= OnApplication_Suspension;
            }
        }

        private void OnApplication_Resume(object sender, object e)
        {
            if (_watcherSuspended)
            {
                _watcherSuspended = false;
                StartDeviceWatcher();
            }
        }

        private void OnApplication_Suspension(object sender, SuspendingEventArgs e)
        {
            if (DeviceWatcherStarted)
            {
                _watcherSuspended = true;
                StopDeviceWatcher();
            }
            else
            {
                _watcherSuspended = false;
            }

            CloseCurrentlyConnectedDevice();
        }
    }
}