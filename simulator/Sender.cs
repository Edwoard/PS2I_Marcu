using System;
using System.Net.Sockets;

namespace Comm
{
    public class Sender
    {
        private readonly string ipAddress;
        private readonly int portNumber;
        TcpClient _sender;
		
		public Sender(string IpAddress, int PortNumber)
		{
            ipAddress = IpAddress;
            portNumber = PortNumber;
        }
		public void Send(byte ValueToSend)
		{
			try
			{
				_sender = _sender ?? new TcpClient(ipAddress, portNumber);
				NetworkStream nwStream = _sender.GetStream();
				byte[] bytesToSend = new byte[4];
				bytesToSend[0] = ValueToSend;
				nwStream.Write(bytesToSend, 0, bytesToSend.Length);				
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
		}
	}
}
