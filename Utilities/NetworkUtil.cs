using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using HttpListener = System.Net.HttpListener;

namespace SZExtractorGUI.Utilities
{
    public static class NetworkUtil
    {
        /// <summary>
        /// Checks if a specific port is available for use on localhost.
        /// </summary>
        /// <param name="port">The port number to check.</param>
        /// <returns>True if the port is available, false otherwise.</returns>
        public static bool IsPortAvailable(int port)
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets an available port. Prefer using GetOsAssignedPort() instead.
        /// This method is kept for scenarios where a specific port range is required.
        /// </summary>
        /// <param name="startPort">The port to start searching from (default: 5000). Use 0 to let the OS assign any available port.</param>
        /// <param name="maxAttempts">Maximum number of ports to try (default: 10).</param>
        /// <returns>An available port number.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no available port is found after maxAttempts.</exception>
        public static int FindAvailablePort(int startPort = 5000, int maxAttempts = 10)
        {
            // If startPort is 0, let the OS assign any available port
            if (startPort == 0)
            {
                return GetOsAssignedPort();
            }

            for (int i = 0; i < maxAttempts; i++)
            {
                int port = startPort + i;
                if (port > 65535) break;
                
                if (IsPortAvailable(port))
                {
                    Debug.WriteLine($"[NetworkUtil] Found available port: {port}");
                    return port;
                }
            }
            
            throw new InvalidOperationException($"Could not find an available port after {maxAttempts} attempts starting from {startPort}");
        }

        /// <summary>
        /// Gets an available port assigned by the operating system.
        /// This is more efficient than checking ports sequentially.
        /// </summary>
        /// <returns>An available port number assigned by the OS.</returns>
        public static int GetOsAssignedPort()
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                Debug.WriteLine($"[NetworkUtil] OS assigned available port: {port}");
                return port;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkUtil] Error getting OS-assigned port: {ex.Message}");
                throw new InvalidOperationException("Failed to get an OS-assigned port", ex);
            }
        }

        /// <summary>
        /// Checks whether an HTTP listener can bind to a given port using HttpListener.
        /// This catches cases where TCP appears free but HTTP namespace reservations prevent binding.
        /// </summary>
        public static bool IsHttpPortBindable(int port)
        {
            HttpListener? listener = null;
            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                listener.Start();
                return true;
            }
            catch (HttpListenerException ex)
            {
                Debug.WriteLine($"[NetworkUtil] Port {port} is not bindable by HttpListener: {ex.ErrorCode} - {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkUtil] Port {port} unexpected error when probing HttpListener: {ex.Message}");
                return false;
            }
            finally
            {
                try { listener?.Stop(); } catch { /* ignore */ }
                try { listener?.Close(); } catch { /* ignore */ }
            }
        }

        /// <summary>
        /// Finds a bindable HTTP port by requesting an OS-assigned port and verifying HttpListener can bind.
        /// </summary>
        public static int FindBindableHttpPort(int maxAttempts = 10)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                var port = GetOsAssignedPort();
                if (IsHttpPortBindable(port))
                {
                    Debug.WriteLine($"[NetworkUtil] Found bindable HTTP port: {port}");
                    return port;
                }
            }
            throw new InvalidOperationException("Failed to find a bindable HTTP port after multiple attempts");
        }
    }
}
