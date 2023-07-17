using System;
using System.Data.SqlClient;
using System.Management;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace UsbDeviceAccess
{
    class Program
    {
        // Connection string for the database
        const string ConnectionString = "Data Source=F1-LAPTOP-MPC\\SQLEXPRESS;Initial Catalog=USB;Integrated Security=True;";

        // Constants for DIF_PROPERTYCHANGE and DICS_PROPCHANGE
        private const uint DIF_PROPERTYCHANGE = 0x00000012;

        // P/Invoke declaration for UpdateDriverForPlugAndPlayDevices function
        [DllImport("newdev.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool UpdateDriverForPlugAndPlayDevices(IntPtr hwndParent, string deviceInstancePath, string fullInfPath, uint installFlags, out bool bRebootRequired);

        static void Main(string[] args)
        {
            StartUsbDetection();
            RegisterUsbEventWatcher(); // Add this line to register the USB event watcher

            // Keep the console application running to detect USB device events
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        public static void StartUsbDetection()
        {
            // Implement USB device detection and get the device ID
            string deviceId = GetUsbDeviceId();

            if (!string.IsNullOrEmpty(deviceId))
            {
                bool deviceFound = CheckDeviceInDatabase(deviceId);
                if (!deviceFound)
                {
                    DisableUsbStorageDevices();
                    Console.WriteLine("USB storage devices disabled.");
                }
                else
                {
                    EnableUsbStorageDevices();
                    Console.WriteLine("USB storage devices enabled.");
                    // Allow the USB device to access the PC (not implemented here)
                    Console.WriteLine("USB device allowed to access the PC.");
                }

                // Trigger the USB controller rescan with the provided device instance path
                TriggerUsbControllerRescan(deviceId);
            }
        }

        public static string GetUsbDeviceId()
        {
            string deviceId = null;

            try
            {
                // Implement USB device detection and retrieval here
                // For example, you can use ManagementObjectSearcher to query Win32_PnPEntity class to get USB device IDs

                ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PnPEntity");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    if (queryObj["Caption"] != null && queryObj["Caption"].ToString().Contains("USB") &&
                        queryObj["DeviceID"] != null && queryObj["DeviceID"].ToString().Contains("USB"))
                    {
                        deviceId = queryObj["DeviceID"].ToString();
                        // Display the device ID to the console
                        Console.WriteLine("USB device ID: " + deviceId);
                        break; // For simplicity, just get the first USB device ID found
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while retrieving USB device ID: " + ex.Message);
            }

            return deviceId;
        }


        public static bool CheckDeviceInDatabase(string deviceId)
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                try
                {
                    connection.Open();

                    // SQL command to check if the device ID exists in the database
                    string sql = "SELECT COUNT(*) FROM [USB].[dbo].[UsbDevices] WHERE DeviceID = @DeviceID";

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@DeviceID", deviceId);
                        int count = (int)command.ExecuteScalar();

                        return count > 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error while checking the database: " + ex.Message);
                    return false;
                }
            }
        }

        public static void EnableUsbStorageDevices()
        {
            try
            {
                // Restore the original value of the registry key to enable USB storage devices
                string keyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\USBSTOR";
                object originalValue = Registry.GetValue(keyPath, "Start", null);
                if (originalValue != null)
                {
                    Registry.SetValue(keyPath, "Start", originalValue, RegistryValueKind.DWord);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while enabling USB storage devices: " + ex.Message);
            }
        }

        public static void DisableUsbStorageDevices()
        {
            try
            {
                // Modify the registry key value to disable USB storage devices (set to 4)
                string keyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\USBSTOR";
                Registry.SetValue(keyPath, "Start", 4, RegistryValueKind.DWord);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while disabling USB storage devices: " + ex.Message);
            }
        }

        private static void RefreshDeviceManager()
        {
            try
            {
                // Create an empty DeviceInfoData structure
                SP_DEVINFO_DATA devInfoData = new SP_DEVINFO_DATA();
                devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

                // Call the SetupDiCallClassInstaller function with DIF_PROPERTYCHANGE to refresh the Device Manager
                SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, IntPtr.Zero, ref devInfoData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while refreshing the Device Manager: " + ex.Message);
            }
        }

        // P/Invoke declaration for SetupDiCallClassInstaller function
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetupDiCallClassInstaller(uint InstallFunction, IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData);

        // Struct for SP_DEVINFO_DATA required for the SetupDiCallClassInstaller function
        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        private static void TriggerUsbControllerRescan(string usbDeviceId)
{
    try
    {
        ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2",
            $"SELECT * FROM Win32_PnPEntity WHERE DeviceID = '{usbDeviceId}'");

        foreach (ManagementObject queryObj in searcher.Get())
        {
            string deviceInstancePath = queryObj["PNPDeviceID"].ToString();
            bool rebootRequired;
            bool result = UpdateDriverForPlugAndPlayDevices(IntPtr.Zero, deviceInstancePath, null, 0x00000001, out rebootRequired);

            if (result)
            {
                Console.WriteLine("USB controller rescan successful.");
            }
            else
            {
                Console.WriteLine("Failed to trigger USB controller rescan.");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error while triggering USB controller rescan: " + ex.Message);
    }
}


        // New method to handle USB device events
        public static void HandleUsbDeviceEvent(string deviceId, bool isConnected)
        {
            try
            {
                if (isConnected)
                {
                    bool deviceFound = CheckDeviceInDatabase(deviceId);
                    if (!deviceFound)
                    {
                        DisableUsbStorageDevices(); // Corrected method name
                        Console.WriteLine("USB storage devices disabled.");
                    }
                    else
                    {
                        EnableUsbStorageDevices(); // Corrected method name
                        Console.WriteLine("USB storage devices enabled.");
                        // Allow the USB device to access the PC (not implemented here)
                        Console.WriteLine("USB device allowed to access the PC.");
                    }
                }
                else
                {
                    // Handle the case when the USB device is disconnected
                    // For example, you may choose to enable USB storage devices again when disconnected
                    // ...

                    // Alternatively, you can decide not to do anything when the USB device is disconnected
                    // ...
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while handling USB device event: " + ex.Message);
            }
        }
    

        public static void RegisterUsbEventWatcher()
        {
            try
            {
                ManagementScope scope = new ManagementScope("root\\CIMV2");
                var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
                ManagementEventWatcher watcher = new ManagementEventWatcher(scope, query);
                watcher.EventArrived += UsbEventArrived;
                watcher.Start();
                Console.WriteLine("USB event watcher registered.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while registering USB event watcher: " + ex.Message);
            }
        }

        public static void UsbEventArrived(object sender, EventArrivedEventArgs e)
        {
            try
            {
                PropertyData targetInstanceData = e.NewEvent.Properties["TargetInstance"];
                if (targetInstanceData != null && targetInstanceData.Value is ManagementBaseObject targetInstance)
                {
                    string deviceId = targetInstance.GetPropertyValue("DeviceID").ToString();
                    bool isConnected = (int)targetInstance.GetPropertyValue("ConfigManagerErrorCode") == 0;

                    if (!string.IsNullOrEmpty(deviceId))
                    {
                        HandleUsbDeviceEvent(deviceId, isConnected);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while handling USB event: " + ex.Message);
            }
        }

    }
}
