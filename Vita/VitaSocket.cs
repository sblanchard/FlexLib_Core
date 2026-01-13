// ****************************************************************************
///*!	\file VitaSocket.cs
// *	\brief A Socket for use in communicating with the Vita protocol
// *
// *	\copyright	Copyright 2012-2017 FlexRadio Systems.  All Rights Reserved.
// *				Unauthorized use, duplication or distribution of this software is
// *				strictly prohibited by law.
// *
// *	\date 2012-03-05
// *	\author Eric Wachsmann, KE5DTO
// */
// ****************************************************************************

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Vita
{
    public class VitaSocket
    {
        private VitaDataReceivedCallback callback;
        private Socket socket;
        const int MIN_UDP_PORT = 1025;
        const int MAX_UDP_PORT = 65535;

        private int port;
        public int Port
        {
            get { return port; }
        }

        //private IPAddress ip;
        public IPAddress IP
        {
            get
            {
                if (socket == null || socket.LocalEndPoint == null) return null;
                return ((IPEndPoint)(socket.LocalEndPoint)).Address;
            }
        }

        public VitaSocket(int _port, VitaDataReceivedCallback _callback)
        {
            bool done = false;
            port = _port;
            callback = _callback;

            while (!done)
            {
                try
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.ExclusiveAddressUse = true;
                    socket.ReceiveBufferSize = 150000 * 5;
                    socket.Bind(new IPEndPoint(IPAddress.Any, port));
                    done = true;
                }
                catch (Exception ex) // if we get here, it is likely because the port is already open
                {
                    port++; // lets increment the port and try again
                    if (port > 6010)
                        throw new Exception(ex.Message);
                }
            }

            Debug.WriteLine("VitaSocket: Newly created on port " + _port);
            
            // beging looking for UDP packets immediately
            StartReceive();
        }

        private IPEndPoint RadioEndpoint;

        public void SendUDP(byte [] data)
        {
            try
            {
                socket.SendTo(data, RadioEndpoint);
            } 
            catch (Exception ex)
            {
                HandleException(ex, "VitaSocket::SendUDP()");
            }
        }

        public VitaSocket(int _port, VitaDataReceivedCallback _callback, IPAddress radioIp, int radioPort) : this (_port, _callback)
        {
            // In addition to creating the VitaSocket, for WAN we must also send the
            // 'client udp_register' command to the radio over the created UDP socket

            //ensure port is within range before assigning endpoint
            if (radioPort >= MIN_UDP_PORT && radioPort <= MAX_UDP_PORT)
                RadioEndpoint = new IPEndPoint(radioIp, radioPort);
        }

        /// <summary>
        /// Constructor for binding VitaSocket to a specific local IP address.
        /// Required for cross-subnet TX audio to ensure UDP source IP matches TCP connection.
        /// </summary>
        public VitaSocket(int _port, VitaDataReceivedCallback _callback, IPAddress localBindIp, IPAddress radioIp, int radioPort)
        {
            bool done = false;
            port = _port;
            callback = _callback;

            while (!done)
            {
                try
                {
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    socket.ExclusiveAddressUse = true;
                    socket.ReceiveBufferSize = 150000 * 5;
                    // Bind to specific local IP instead of IPAddress.Any for cross-subnet TX audio
                    socket.Bind(new IPEndPoint(localBindIp, port));
                    done = true;
                }
                catch (Exception ex) // if we get here, it is likely because the port is already open
                {
                    port++; // lets increment the port and try again
                    if (port > 6010)
                        throw new Exception(ex.Message);
                }
            }

            Debug.WriteLine($"VitaSocket: Newly created on port {_port}, bound to local IP {localBindIp}");

            //ensure port is within range before assigning endpoint
            if (radioPort >= MIN_UDP_PORT && radioPort <= MAX_UDP_PORT)
                RadioEndpoint = new IPEndPoint(radioIp, radioPort);

            // begin looking for UDP packets immediately
            StartReceive();
        }

        /// <summary>
        /// Begin an asynchronous receive
        /// </summary>
        private void StartReceive()
        { 
            try
            {
                byte[] buf = new byte[VitaFlex.MAX_VITA_PACKET_SIZE];
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                socket.BeginReceiveFrom(buf, 0, VitaFlex.MAX_VITA_PACKET_SIZE, SocketFlags.None, ref remoteEndPoint,
                    new AsyncCallback(DataReceived), buf);      
            }
            catch (Exception ex)
            {
                HandleException(ex, "VitaSocket::StartReceive");
            }           
        }

        private void DataReceived(IAsyncResult ar)
        {
            //Console.WriteLine("VitaSocket::DataReceived()");
            try
            {
                // If completed synchronously, defer to thread pool to prevent stack overflow
                // When BeginReceiveFrom has data immediately available, it can complete synchronously
                // and invoke this callback on the same stack frame, causing deep recursion
                if (ar.CompletedSynchronously)
                {
                    System.Threading.ThreadPool.QueueUserWorkItem(_ => DataReceived_Internal(ar));
                    return;
                }

                DataReceived_Internal(ar);
            }
            catch (Exception ex)
            {
                HandleException(ex, "VitaSocket::DataReceived");
            }
        }

        private void DataReceived_Internal(IAsyncResult ar)
        {
            try
            {
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                int num_bytes = socket.EndReceiveFrom(ar, ref remoteEndPoint);
                byte[] buf = (byte[])ar.AsyncState;

                if (callback != null)
                    callback((IPEndPoint)remoteEndPoint, buf, num_bytes);

                StartReceive();
            }
            catch (Exception ex)
            {
                HandleException(ex, "VitaSocket::DataReceived_Internal");
            }
        }

        private void HandleException(Exception ex, string function_path)
        {
            string s = function_path+" Exception: " + ex.Message + "\n\n";
            if (ex.InnerException != null)
                s += ex.InnerException.Message + "\n\n" + ex.InnerException.StackTrace;
            else s += ex.StackTrace;

            Debug.WriteLine(s);

            CloseSocket();
        }

        public void CloseSocket()
        {
            try
            {
                if (socket != null)
                    socket.Close();
            }
            catch { }
        }
    }
}
