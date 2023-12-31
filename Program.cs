﻿using System;
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

        static void Main(string[] args)
        {
            // Start the USB detection and access control logic
            StartUsbDetection();

            // Register for registry change events to automatically update Device Manager
            RegisterRegistryChangeEvents();

            // Register the USB event watcher to handle device connection and disconnection events
            RegisterUsbEventWatcher();

            // Keep the console application running to detect USB device events
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        // Method to start USB device detection and access control
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

        // Method to get the USB mass storage device ID and instance path
        public static string GetUsbDeviceId()
        {
            string deviceId = null;

            try
            {
                // Implement USB device detection and retrieval here
                // Use Win32_USBHub class to get USB mass storage devices
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_USBHub");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    if (queryObj["Caption"] != null && queryObj["DeviceID"] != null)
                    {
                        string caption = queryObj["Caption"].ToString();
                        string deviceID = queryObj["DeviceID"].ToString();

                        // Check if the device is a USB mass storage device
                        if (caption.Contains("USB Mass Storage"))
                        {
                            deviceId = deviceID;
                            // Display the device ID to the console
                            Console.WriteLine("USB mass storage device ID: " + deviceId);
                            break; // For simplicity, just get the first USB mass storage device ID found
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while retrieving USB mass storage device ID: " + ex.Message);
            }

            return deviceId;
        }

        // Method to check if the USB device is authorized in the database
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

        // Method to enable USB storage devices
        public static void EnableUsbStorageDevices()
        {
            try
            {
                // Set the registry key value to enable USB storage devices (set to 3)
                string keyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\USBSTOR";
                Registry.SetValue(keyPath, "Start", 3, RegistryValueKind.DWord);
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

        // Method to disable USB storage devices
        public static void DisableUsbStorageDevices()
        {
            try
            {
                // Set the registry key value to disable USB storage devices (set to 4)
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

        // Method to refresh the Device Manager
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

        // Method to trigger a USB controller rescan
        private static void TriggerUsbControllerRescan(string usbDeviceId)
        {
            try
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE '%{usbDeviceId}%'");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    SP_DEVINFO_DATA devInfoData = new SP_DEVINFO_DATA();
                    devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

                    // Get the device instance ID and pass it to the SetupDiCallClassInstaller function
                    string deviceInstanceId = queryObj["DeviceID"].ToString();
                    IntPtr ptr = Marshal.StringToHGlobalAuto(deviceInstanceId);
                    bool result = SetupDiCallClassInstaller(DIF_PROPERTYCHANGE, ptr, ref devInfoData);
                    Marshal.FreeHGlobal(ptr);

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

        // Method to register the USB event watcher
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

        // Method to handle the USB event arrival
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

        // Method to handle USB device events
        public static void HandleUsbDeviceEvent(string deviceId, bool isConnected)
        {
            try
            {
                if (isConnected)
                {
                    // Always trigger USB controller rescan
                    TriggerUsbControllerRescan(deviceId);

                    // Check if the device is authorized in the database
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
                }
                else
                {
                    // Handle the case when the USB device is disconnected
                    EnableUsbStorageDevices(); // Disable USB storage devices when the authorized USB is disconnected
                    Console.WriteLine("USB storage devices disabled.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while handling USB device event: " + ex.Message);
            }
        }

        // Method to register for registry change events
        public static void RegisterRegistryChangeEvents()
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += UsbStorageDevicesRegistryChanged;
        }

        // Method to handle registry change event for USB storage devices
        private static void UsbStorageDevicesRegistryChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            try
            {
                // Check if the changed user preference is related to the registry
                if (e.Category == Microsoft.Win32.UserPreferenceCategory.General)
                {
                    // Check if the change is related to the "Start" registry value of the "USBSTOR" service
                    string usbStorKeyPath = @"SYSTEM\CurrentControlSet\Services\USBSTOR";
                    RegistryKey usbStorKey = Registry.LocalMachine.OpenSubKey(usbStorKeyPath);

                    if (usbStorKey != null)
                    {
                        int startValue = (int)usbStorKey.GetValue("Start", -1);

                        // Check if the value is changed to 3 (enabled) or 4 (disabled)
                        if (startValue == 3)
                        {
                            // USB storage devices are enabled
                            // You can add additional logic here if needed
                            Console.WriteLine("USB storage devices enabled.");
                        }
                        else if (startValue == 4)
                        {
                            // USB storage devices are disabled
                            // You can add additional logic here if needed
                            Console.WriteLine("USB storage devices disabled.");
                        }

                        // If you want to trigger the USB controller rescan here, you can call the method like this:
                        // TriggerUsbControllerRescan(GetUsbDeviceId());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while handling USB storage devices registry change event: " + ex.Message);
            }
        }
    }
}
