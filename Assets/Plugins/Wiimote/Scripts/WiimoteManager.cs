using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Runtime.InteropServices;
using WiimoteApi.Internal;

namespace WiimoteApi
{
    public class WiimoteManager
    {
        private const ushort vendor_id_wiimote = 0x057e;
        private const ushort product_id_wiimote = 0x0306;
        private const ushort product_id_wiimoteplus = 0x0330;

        /// <summary>
        /// A list of all currently connected Wii Remotes.
        /// </summary>
        public static List<Wiimote> Wiimotes { get { return _Wiimotes; } }
        private static List<Wiimote> _Wiimotes = new List<Wiimote>();

        /// <summary>
        /// If true, WiimoteManager and Wiimote will write data reports and other debug messages to the console.
        /// </summary>
        public static bool Debug_Messages = false;

        /// <summary>
        /// The maximum time, in milliseconds, between data report writes.
        /// </summary>
        public static int MaxWriteFrequency = 20; // In ms
        private static Queue<WriteQueueData> WriteQueue;

        // A cancellation flag for stopping the send thread.
        private static volatile bool _shouldStopSendThread = false;
        private static Thread SendThreadObj;

        // ------------- RAW HIDAPI INTERFACE ------------- //

        /// <summary>
        /// Attempts to find connected Wii Remotes.
        /// </summary>
        public static bool FindWiimotes()
        {
            bool ret = _FindWiimotes(WiimoteType.WIIMOTE);
            ret = ret || _FindWiimotes(WiimoteType.WIIMOTEPLUS);
            return ret;
        }

        private static bool _FindWiimotes(WiimoteType type)
        {
            ushort vendor = 0;
            ushort product = 0;

            if (type == WiimoteType.WIIMOTE)
            {
                vendor = vendor_id_wiimote;
                product = product_id_wiimote;
            }
            else if (type == WiimoteType.WIIMOTEPLUS || type == WiimoteType.PROCONTROLLER)
            {
                vendor = vendor_id_wiimote;
                product = product_id_wiimoteplus;
            }

            IntPtr ptr = HIDapi.hid_enumerate(vendor, product);
            IntPtr cur_ptr = ptr;

            if (ptr == IntPtr.Zero)
                return false;

            hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));
            bool found = false;

            while (cur_ptr != IntPtr.Zero)
            {
                Wiimote remote = null;
                bool fin = false;
                foreach (Wiimote r in Wiimotes)
                {
                    if (fin)
                        continue;

                    if (r.hidapi_path.Equals(enumerate.path))
                    {
                        remote = r;
                        fin = true;
                    }
                }
                if (remote == null)
                {
                    IntPtr handle = HIDapi.hid_open_path(enumerate.path);
                    WiimoteType trueType = type;

                    // Wii U Pro Controllers have the same identifiers as the newer Wii Remote Plus except for product string.
                    if (enumerate.product_string.EndsWith("UC"))
                        trueType = WiimoteType.PROCONTROLLER;

                    remote = new Wiimote(handle, enumerate.path, trueType);

                    if (Debug_Messages)
                        Debug.Log("Found New Remote: " + remote.hidapi_path);

                    Wiimotes.Add(remote);

                    remote.SendDataReportMode(InputDataType.REPORT_BUTTONS);
                    remote.SendStatusInfoRequest();
                }

                cur_ptr = enumerate.next;
                if (cur_ptr != IntPtr.Zero)
                    enumerate = (hid_device_info)Marshal.PtrToStructure(cur_ptr, typeof(hid_device_info));
            }

            HIDapi.hid_free_enumeration(ptr);
            return found;
        }

        /// <summary>
        /// Disables the given Wiimote by closing its bluetooth HID connection and removes it from Wiimotes.
        /// </summary>
        public static void Cleanup(Wiimote remote)
        {
            if (remote.hidapi_handle != IntPtr.Zero)
                HIDapi.hid_close(remote.hidapi_handle);

            Wiimotes.Remove(remote);
        }

        /// <summary>
        /// Returns true if any Wii Remotes are connected.
        /// </summary>
        public static bool HasWiimote()
        {
            return !(Wiimotes.Count <= 0 || Wiimotes[0] == null || Wiimotes[0].hidapi_handle == IntPtr.Zero);
        }

        /// <summary>
        /// Sends RAW DATA to the given bluetooth HID device.
        /// </summary>
        public static int SendRaw(IntPtr hidapi_wiimote, byte[] data)
        {
            if (hidapi_wiimote == IntPtr.Zero) return -2;

            // Start the send thread if it hasn't been started.
            if (WriteQueue == null)
            {
                WriteQueue = new Queue<WriteQueueData>();
                _shouldStopSendThread = false;
                SendThreadObj = new Thread(new ThreadStart(SendThread));
                SendThreadObj.Start();
            }

            WriteQueueData wqd = new WriteQueueData
            {
                pointer = hidapi_wiimote,
                data = data
            };
            lock (WriteQueue)
                WriteQueue.Enqueue(wqd);

            return 0; // TODO: Better error handling
        }

        private static void SendThread()
        {
            // Loop until the cancellation flag is set.
            while (!_shouldStopSendThread)
            {
                lock (WriteQueue)
                {
                    if (WriteQueue.Count != 0)
                    {
                        WriteQueueData wqd = WriteQueue.Dequeue();
                        // Only attempt a write if the pointer is valid.
                        if (wqd.pointer != IntPtr.Zero)
                        {
                            int res = HIDapi.hid_write(wqd.pointer, wqd.data, new UIntPtr(Convert.ToUInt32(wqd.data.Length)));
                            if (res == -1)
                                Debug.LogError("HidAPI reports error " + res + " on write: " + Marshal.PtrToStringUni(HIDapi.hid_error(wqd.pointer)));
                            else if (Debug_Messages)
                                Debug.Log("Sent " + res + "b: [" + wqd.data[0].ToString("X").PadLeft(2, '0') + "] " + BitConverter.ToString(wqd.data, 1));
                        }
                    }
                }
                Thread.Sleep(MaxWriteFrequency);
            }
        }

        /// <summary>
        /// Receives RAW DATA from the given bluetooth HID device.
        /// </summary>
        public static int RecieveRaw(IntPtr hidapi_wiimote, byte[] buf)
        {
            if (hidapi_wiimote == IntPtr.Zero) return -2;

            HIDapi.hid_set_nonblocking(hidapi_wiimote, 1);
            int res = HIDapi.hid_read(hidapi_wiimote, buf, new UIntPtr(Convert.ToUInt32(buf.Length)));

            return res;
        }

        /// <summary>
        /// Shuts down the WiimoteManager by stopping background threads and cleaning up resources.
        /// Call this when you are finished with the manager (e.g., in OnDestroy).
        /// </summary>
        public static void Shutdown()
        {
            // Signal the send thread to stop.
            _shouldStopSendThread = true;
            if (SendThreadObj != null && SendThreadObj.IsAlive)
            {
                // Wait for the thread to exit.
                SendThreadObj.Join();
            }

            // Close all Wiimote handles.
            foreach (Wiimote remote in Wiimotes)
            {
                if (remote.hidapi_handle != IntPtr.Zero)
                    HIDapi.hid_close(remote.hidapi_handle);
            }
            Wiimotes.Clear();

            // Clear any remaining write queue data.
            if (WriteQueue != null)
            {
                lock (WriteQueue)
                {
                    WriteQueue.Clear();
                }
            }
        }

        private class WriteQueueData
        {
            public IntPtr pointer;
            public byte[] data;
        }
    }
}
