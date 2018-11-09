using System;
using System.Collections.Generic;
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

                byte[] type = { 0x00, 0x01 };
                byte[] dnsClass = { 0x00, 0x01 };

                var queryList = new List<byte>();

                IPAddress serverAddr = IPAddress.Parse(DEFUALT_DNS);
                IPEndPoint endPoint = new IPEndPoint(serverAddr, 53);

                var requestText = "www.rit.edu";
                byte[] text_bytes = Encoding.ASCII.GetBytes(requestText);

                queryList.AddRange(text_bytes);
                queryList.AddRange(type);
                queryList.AddRange(dnsClass);

                // need to append type and class
                byte[] send_buffer = queryList.ToArray();
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
