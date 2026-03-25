using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Flex.Smoothlake.FlexLib
{
    public class SslClient
    {
        private const int TCP_KEEPALIVE_PING_MS = 10000;
        StreamWriter _writer = null;
        private string _hostname;
        private int _port;
        private Lock _writerLockObj = new Lock();        

        private SslStream _sslStream;
        private TcpClient _tcpClient;
        private volatile bool _detached = false;

        public Action<string>? RawLineReceived { get; set; }

        public SslClient(string hostname, string port, int src_port = 0, bool start_ping_thread = false, bool validate_cert = true)
        {
            _hostname = hostname;
            _port = int.Parse(port);

            try
            {
                IPEndPoint localEndpoint = new IPEndPoint(IPAddress.Any, src_port);
                _tcpClient = new TcpClient(localEndpoint);
                _tcpClient.Connect(_hostname, _port);
                // TODO: Fix long timeout

                _sslStream = CreateSslStream(_tcpClient, _hostname, validate_cert);

                StartWriterStream(_sslStream);

                if (start_ping_thread)
                    Task.Factory.StartNew(() => PingThread(), TaskCreationOptions.LongRunning);

                _isConnected = true;
            }
            catch (Exception)
            {
                // callers should check IsConnected == true to ensure this didn't fail
            }
        }

        public void StartReceiving()
        {
            Task.Factory.StartNew(() => StartListener(), TaskCreationOptions.LongRunning);
        }

        public void Write(string message)
        {
            lock (_writerLockObj)
            {
                StreamWriter writer = _writer;
                if (writer == null)
                {
                    Disconnect();
                    return;
                }

                try
                {
                    writer.WriteLine(message);
                }
                catch (Exception)
                {
                    Disconnect();
                }
            }
        }

        private void StartWriterStream(SslStream sslStream)
        {
            _writer = new StreamWriter(sslStream);
            _writer.AutoFlush = true;
        }

        private void PingThread()
        {
            try
            {
                while (_isConnected)
                {
                    Write("ping from client");
                    Thread.Sleep(TCP_KEEPALIVE_PING_MS);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SslClient::PingThread Exception: " + ex.ToString());
            }
        }

        private ManualResetEventSlim _listenerExited = new ManualResetEventSlim(false);

        public SslStream? DetachStream()
        {
            if (_sslStream == null || !IsConnected) return null;
            _detached = true;
            MessageReceivedReady = null;

            // Set a short read timeout to unblock the listener's ReadLine()
            try { _sslStream.ReadTimeout = 100; } catch { }

            // Wait for the listener thread to actually exit (max 2s)
            _listenerExited.Wait(TimeSpan.FromSeconds(2));

            // Reset timeout for the bridge's use
            try { _sslStream.ReadTimeout = Timeout.Infinite; } catch { }

            return _sslStream;
        }

        private void StartListener()
        {
            try
            {
                // Don't wrap in StreamReader — it buffers data and consumes bytes
                // that the bridge needs after detach. Use raw byte reads instead.
                byte[] buffer = new byte[4096];
                StringBuilder sb = new StringBuilder();
                while (_tcpClient != null && _tcpClient.Connected && !_detached)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = _sslStream.Read(buffer, 0, buffer.Length);
                    }
                    catch (IOException) when (_detached)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    if (bytesRead == 0 || _detached) break;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    // Split on newlines (SmartSDR protocol is newline-delimited)
                    string accumulated = sb.ToString();
                    int newlineIdx;
                    while ((newlineIdx = accumulated.IndexOf('\n')) >= 0)
                    {
                        string line = accumulated.Substring(0, newlineIdx).TrimEnd('\r');
                        accumulated = accumulated.Substring(newlineIdx + 1);
                        if (!_detached)
                        {
                            RawLineReceived?.Invoke(line);
                            OnMessageReceivedReady(line);
                        }
                    }
                    sb.Clear();
                    sb.Append(accumulated);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SslClient::StartListener Exception: " + ex.ToString());                
            }
            finally
            {
                _listenerExited.Set();
                if (!_detached)
                    Disconnect();
            }
        }

        public delegate void MessageReceived(string msg);
        public event MessageReceived MessageReceivedReady;

        private void OnMessageReceivedReady(string msg)
        {
            if (MessageReceivedReady == null) return;
            MessageReceivedReady(msg);
        }

        // The following method is invoked by the RemoteCertificateValidationDelegate.
        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        { 
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Debug.WriteLine("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.

#if !DEBUG
            return false;
#else
            return true;
#endif
        }

        private SslStream CreateSslStream(TcpClient tcpClient, string hostname, bool validate_cert)
        {
            SslStream sslStream = null;
            if (validate_cert)
            {
                /* Validate the certificate - meant to be used for SmartLink Server */

                sslStream = new SslStream(tcpClient.GetStream(), false,
                    new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            }
            else
            {
                /* No validation - meant to be directly connected to radio */
                sslStream = new SslStream(tcpClient.GetStream(), false,
                    new RemoteCertificateValidationCallback((s, certificate, chain, policyErrors) => { return true; }),
                    new LocalCertificateSelectionCallback((a, b, c, d, e2) => { return null; }));
            }

            sslStream.AuthenticateAsClient(hostname);
            
            return sslStream;
        }

        Lock _disconnectLockObj = new Lock();
        public void Disconnect()
        {
            lock (_disconnectLockObj)
            {
                if (_isConnected)
                {
                    _isConnected = false;

                    if (_writer != null)
                    {
                        _writer.Close();
                        _writer = null;
                    }

                    if (_sslStream != null)
                    {
                        _sslStream.Close();
                        _sslStream = null;
                    }

                    if (_tcpClient != null)
                    {
                        _tcpClient.Close();
                        _tcpClient = null;
                    }

                    OnDisconnected();
                }
            }
        }

        public EventHandler Disconnected;
        
        private void OnDisconnected()
        {
            if (Disconnected != null)
                Disconnected(this, null);
        }

        private bool _isConnected = false;
        public bool IsConnected
        {
            get { return _isConnected; }
        }
    }
}
