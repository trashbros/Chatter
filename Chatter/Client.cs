﻿/*
Primary UDP messaging client class
Copyright (C) 2020  Trash Bros (BlinkTheThings, Reakain)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Chatter
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public string Message { get; set; }
        public IPAddress SenderIP { get; set; }
        public MessageReceivedEventArgs(string message, IPAddress senderIP)
        {
            Message = message;
            SenderIP = senderIP;
        }
    }

    public class AlreadyReceivingException : Exception
    {
        public AlreadyReceivingException()
        {
        }

        public AlreadyReceivingException(string message)
            : base(message)
        {
        }

        public AlreadyReceivingException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class Client : IDisposable
    {
        #region Properties
        public IPAddress LocalIP { get; }
        public IPAddress MulticastIP { get; }
        public int Port { get; }
        #endregion

        #region Events
        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEventHandler;
        #endregion

        #region Private member variables
        private Socket _receiveSocket;
        private bool _disposed = false;
        #endregion

        public Client(IPAddress localIP, IPAddress multicastIP, int port)
        {
            LocalIP = localIP;
            MulticastIP = multicastIP;
            Port = port;
        }

        public void StartReceiving()
        {
            if (_receiveSocket != null)
            {
                throw new AlreadyReceivingException();
            }

            try
            {
                using (_receiveSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    _receiveSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _receiveSocket.Bind(new IPEndPoint(IPAddress.Any, Port));
                    _receiveSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(MulticastIP, LocalIP));

                    IPEndPoint remoteIPEndPoint = new IPEndPoint(MulticastIP, 0);
                    EndPoint remoteEndPoint = remoteIPEndPoint;

                    while (true)
                    {
                        byte[] datagram = new byte[65536];
                        int length = _receiveSocket.ReceiveFrom(datagram, 0, datagram.Length, SocketFlags.None, ref remoteEndPoint);
                        Array.Resize(ref datagram, length);

                        string message = Encoding.UTF8.GetString(Convert.FromBase64String(Encoding.UTF8.GetString(datagram)));
                        MessageReceivedEventHandler?.Invoke(this, new MessageReceivedEventArgs(message, remoteIPEndPoint.Address));
                    }
                }
            }
            catch (SocketException e)
            {
                StopReceiving();
                Console.WriteLine(e);
            }
        }

        public void StopReceiving()
        {
            try
            {
                _receiveSocket?.Shutdown(SocketShutdown.Receive);
                _receiveSocket?.Close();
                _receiveSocket = null;
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
        }

        public void Send(string message)
        {
            try
            {
                using (var udpClient = new UdpClient(new IPEndPoint(LocalIP, 0)))
                {
                    byte[] datagram = Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(message)));
                    udpClient.Send(datagram, datagram.Length, new IPEndPoint(MulticastIP, Port));
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopReceiving();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
