using System;
using System.Data.SqlClient;
using System.Management;
using System.Runtime.InteropServices;

namespace USBAccessControlUser
{
    class Program
    {
        // Import the necessary Windows API functions
        [DllImport("user32.dll")]
        public static extern int SendMessage(int hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern int RegisterWindowMessage(string lpString);

        static void Main(string[] args)
        {
            Console.WriteLine("USB Access Control - User Side");

            // Establish the connection to the remote SQL Server
            string connectionString = "Data Source=F1-LAPTOP-MPC\\SQLEXPRESS;Initial Catalog=USB;Integrated Security=True;";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    Console.WriteLine("Connected to the database.");

                    // Retrieve the USB device ID
                    string deviceID = GetUSBDeviceID();

                    Console.WriteLine("USB device ID: " + deviceID);

                    // Check if the device ID exists in the USB list
                    bool isAllowed = CheckDeviceID(connection, deviceID);

                    // Take action based on the result
                    if (isAllowed)
                    {
                        AllowAccess();
                    }
                    else
                    {
                        BlockAccess();
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine("Error connecting to the database: " + ex.Message);
                }
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static string GetUSBDeviceID()
        {
            string deviceID = string.Empty;

            // Query to retrieve USB devices
            string query = "SELECT * FROM Win32_PnPEntity WHERE Status='OK' AND Caption='USB Mass Storage Device'";

            // Create a management object searcher with the query
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query))
            {
                // Iterate over the USB devices
                foreach (ManagementObject usbDevice in searcher.Get())
                {
                    deviceID = usbDevice["DeviceID"].ToString();
                    break; // Assuming there is only one USB device
                }
            }

            return deviceID;
        }

        private static bool CheckDeviceID(SqlConnection connection, string deviceID)
        {
            // Prepare the SQL query
            string query = "SELECT COUNT(*) FROM UsbDevices WHERE DeviceID = @DeviceID";

            // Create the command and parameters
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@DeviceID", deviceID);

                // Execute the query
                int count = (int)command.ExecuteScalar();

                // If the count is greater than zero, the device ID exists in the USB list
                return count > 0;
            }
        }

        private static void AllowAccess()
        {
            Console.WriteLine("USB access allowed.");

            // Implement the code to allow USB access to the PC
            // For example, you can continue with the normal execution flow of your application.

            // Windows-specific code to enable USB access
            // ...

        }

        private static void BlockAccess()
        {
            Console.WriteLine("The inserted USB is not authorized to access this PC.");

            // Disable USB ports using Windows API functions
            DisableUSBPorts();

            // Display a warning message to the user
            ShowWarningMessage();

            // Implement additional actions if needed

            // ...
        }

        private static void DisableUSBPorts()
        {
            // Windows API code to disable USB ports
            // ...

            // Example: Using Registry Editor to disable USB storage
            Microsoft.Win32.Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\USBSTOR", "Start", 4, Microsoft.Win32.RegistryValueKind.DWord);
        }

        private static void ShowWarningMessage()
        {
            // Windows API code to display a warning message
            // ...

            // Example: Using MessageBox to display the warning message
            System.Windows.Forms.MessageBox.Show("The inserted USB is not authorized to access this PC.", "USB Access Blocked", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
        }

    }
}
