using System;
using System.Net.Sockets;
using System.Text;

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
		public void Send(string ValueToSend)
		{
			//try
			//{
			//	_sender = _sender ?? new TcpClient(ipAddress, portNumber);
			//	NetworkStream nwStream = _sender.GetStream();
			//	var data = Encoding.UTF8.GetBytes(ValueToSend);
			//	nwStream.Write(data, 0, data.Length);				
			//}
			//catch (Exception ex)
			//{
			//	Console.WriteLine(ex.ToString());
			//}
		}
	}
}
