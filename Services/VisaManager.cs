using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace DG2072_USB_Control
{
    public class VisaManager : IDisposable
    {
        // VISA instrument handle
        private IntPtr resourceManagerHandle = IntPtr.Zero;
        private IntPtr instrumentHandle = IntPtr.Zero;
        private bool isConnected = false;
        
        // Constants for VISA operations
        private const int VI_NULL = 0;
        private const int VI_SUCCESS = 0;
        private const int VI_TRUE = 1;
        private const int VI_FALSE = 0;
        private const int VI_INFINITE = -1;
        private const int VI_TMO_IMMEDIATE = 0;
        private const int VI_GPIB_REN_DEASSERT = 0;
        private const int VI_GPIB_REN_ASSERT = 1;
        private const int VI_GPIB_REN_DEASSERT_GTL = 2;
        private const int VI_GPIB_REN_ASSERT_ADDRESS = 3;
        private const int VI_GPIB_REN_ASSERT_LLO = 4;
        private const int VI_GPIB_REN_ASSERT_ADDRESS_LLO = 5;
        private const int VI_GPIB_REN_ADDRESS_GTL = 6;
        
        #region VISA P/Invoke Declarations
        
        [DllImport("visa32.dll")]
        private static extern int viOpenDefaultRM(out IntPtr sesn);
        
        [DllImport("visa32.dll")]
        private static extern int viOpen(IntPtr sesn, string rsrcName, int accessMode, int openTimeout, out IntPtr vi);
        
        [DllImport("visa32.dll")]
        private static extern int viClose(IntPtr vi);
        
        [DllImport("visa32.dll")]
        private static extern int viWrite(IntPtr vi, byte[] buf, int count, out int retCount);
        
        [DllImport("visa32.dll")]
        private static extern int viRead(IntPtr vi, byte[] buf, int count, out int retCount);
        
        [DllImport("visa32.dll")]
        private static extern int viGpibControlREN(IntPtr vi, int mode);
        
        [DllImport("visa32.dll")]
        private static extern int viFindRsrc(IntPtr sesn, string expr, out IntPtr findList, out int retcnt, StringBuilder desc);
        
        [DllImport("visa32.dll")]
        private static extern int viFindNext(IntPtr findList, StringBuilder desc);
        
        #endregion
        
        // Event for logging messages
        public event EventHandler<string> LogEvent;
        
        public bool IsConnected => isConnected;
        
        // Connect to the instrument
        public bool Connect(string resourceName)
        {
            try
            {
                // Open the resource manager
                int status = viOpenDefaultRM(out resourceManagerHandle);
                if (status != VI_SUCCESS)
                {
                    Log($"Failed to open the VISA resource manager. Error code: {status}");
                    return false;
                }
                
                // Open the instrument
                status = viOpen(resourceManagerHandle, resourceName, 0, VI_TMO_IMMEDIATE, out instrumentHandle);
                if (status != VI_SUCCESS)
                {
                    Log($"Failed to open the instrument. Error code: {status}");
                    viClose(resourceManagerHandle);
                    return false;
                }
                
                isConnected = true;
                Log($"Connected to {resourceName}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Connection error: {ex.Message}");
                return false;
            }
        }
        
        // Disconnect from the instrument
        public bool Disconnect()
        {
            try
            {
                if (instrumentHandle != IntPtr.Zero)
                {
                    // Set the device back to local mode
                    viGpibControlREN(instrumentHandle, VI_GPIB_REN_DEASSERT_GTL);
                    
                    // Close the instrument
                    int status = viClose(instrumentHandle);
                    instrumentHandle = IntPtr.Zero;
                    
                    if (status != VI_SUCCESS)
                    {
                        Log($"Failed to close the instrument. Error code: {status}");
                        return false;
                    }
                    
                    // Close the resource manager
                    if (resourceManagerHandle != IntPtr.Zero)
                    {
                        viClose(resourceManagerHandle);
                        resourceManagerHandle = IntPtr.Zero;
                    }
                    
                    isConnected = false;
                    Log("Disconnected from the instrument");
                    return true;
                }
                return true;
            }
            catch (Exception ex)
            {
                Log($"Disconnection error: {ex.Message}");
                return false;
            }
        }
        
        // Send a command to the instrument
        public bool SendCommand(string command)
        {
            if (!isConnected || instrumentHandle == IntPtr.Zero)
            {
                Log("Not connected to the instrument.");
                return false;
            }
            
            try
            {
                // Send command
                byte[] buffer = Encoding.ASCII.GetBytes(command + "\n");
                int retCount = 0;
                int status = viWrite(instrumentHandle, buffer, buffer.Length, out retCount);
                
                if (status != VI_SUCCESS)
                {
                    Log($"Failed to write to the instrument. Error code: {status}");
                    return false;
                }
                
                Log($"Command sent: {command}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Send command error: {ex.Message}");
                return false;
            }
        }
        
        // Send a query to the instrument and get the response
        public string SendQuery(string query)
        {
            if (!isConnected || instrumentHandle == IntPtr.Zero)
            {
                Log("Not connected to the instrument.");
                return string.Empty;
            }
            
            try
            {
                // Send query
                byte[] writeBuffer = Encoding.ASCII.GetBytes(query + "\n");
                int retWriteCount = 0;
                int status = viWrite(instrumentHandle, writeBuffer, writeBuffer.Length, out retWriteCount);
                
                if (status != VI_SUCCESS)
                {
                    Log($"Failed to write query to the instrument. Error code: {status}");
                    return string.Empty;
                }
                
                // Read response
                byte[] readBuffer = new byte[1024];
                int retReadCount = 0;
                status = viRead(instrumentHandle, readBuffer, readBuffer.Length, out retReadCount);
                
                if (status != VI_SUCCESS)
                {
                    Log($"Failed to read from the instrument. Error code: {status}");
                    return string.Empty;
                }
                
                string response = Encoding.ASCII.GetString(readBuffer, 0, retReadCount).Trim();
                Log($"Query: {query}, Response: {response}");
                return response;
            }
            catch (Exception ex)
            {
                Log($"Query error: {ex.Message}");
                return string.Empty;
            }
        }
        
        // Find all VISA resources
        public List<string> FindResources()
        {
            List<string> resources = new List<string>();
            IntPtr findList = IntPtr.Zero;
            int resourceCount = 0;
            
            try
            {
                // Open resource manager if not already open
                if (resourceManagerHandle == IntPtr.Zero)
                {
                    int rmStatus = viOpenDefaultRM(out resourceManagerHandle);
                    if (rmStatus != VI_SUCCESS)
                    {
                        Log($"Failed to open the VISA resource manager. Error code: {rmStatus}");
                        return resources;
                    }
                }
                
                // Find resources
                StringBuilder desc = new StringBuilder(256);
                int findStatus = viFindRsrc(resourceManagerHandle, "?*", out findList, out resourceCount, desc);
                
                if (findStatus != VI_SUCCESS)
                {
                    Log($"Failed to find resources. Error code: {findStatus}");
                    return resources;
                }
                
                // Add first resource
                resources.Add(desc.ToString());
                
                // Find all remaining resources
                for (int i = 1; i < resourceCount; i++)
                {
                    int nextStatus = viFindNext(findList, desc);
                    if (nextStatus == VI_SUCCESS)
                    {
                        resources.Add(desc.ToString());
                    }
                    else
                    {
                        Log($"Failed to find next resource. Error code: {nextStatus}");
                        break;
                    }
                }
                
                // Close find list
                viClose(findList);
                
                return resources;
            }
            catch (Exception ex)
            {
                Log($"Find resources error: {ex.Message}");
                if (findList != IntPtr.Zero)
                {
                    viClose(findList);
                }
                return resources;
            }
        }
        
        // Log message
        private void Log(string message)
        {
            LogEvent?.Invoke(this, message);
        }
        
        // Dispose method
        public void Dispose()
        {
            Disconnect();
        }
    }
}
