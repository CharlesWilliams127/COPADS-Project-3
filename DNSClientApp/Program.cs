using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DNSClientApp
{
    class Program
    {
        const string DEFUALT_DNS = "8.8.8.8";

        public static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                var host = args[0];
                var p1 = new Program(); 
                p1.getData(host);
                Console.ReadKey();
            }
            else
            {
                System.Console.WriteLine("Usage: dotnet run <host>");
            }
        }

        public async void getData(string host)
        {
            try
            {
                //var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                var client = new UdpClient();

                IPAddress serverAddr = IPAddress.Parse(DEFUALT_DNS);
                IPEndPoint endPoint = new IPEndPoint(serverAddr, 0);

                var requestText = "www.rit.edu";
                byte[] send_buffer = Encoding.ASCII.GetBytes(requestText);
                await client.SendAsync(send_buffer, send_buffer.Length, endPoint);

                var result = await client.ReceiveAsync();
                var theTime = Encoding.ASCII.GetString(result.Buffer);
                System.Console.WriteLine(theTime);

            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                System.Console.WriteLine("Unable to connect to the server");
            }

        }
    }
}
