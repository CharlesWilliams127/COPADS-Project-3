using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;

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
                p1.getData(host, p1.prepareQuery("www.snapchat.com"));
                Console.ReadKey();
            }
            else
            {
                System.Console.WriteLine("Usage: dotnet run <host>");
            }
        }

        public byte[] prepareQuery(string requestText)
        {
            byte[] header = { 0xAA, 0xAA, // Transaction ID
                    0x01, 0x20, // Query Params
                    0x00, 0x01, // Number of Questions
                    0x00, 0x00, // Number of Answers
                    0x00, 0x00, // Number of Authority Records
                    0x00, 0x00 }; // Number of Additional Records
            byte[] type = {  0x00, 0x01 };
            byte[] dnsClass = { 0x00, 0x01 };

            var queryList = new List<byte>();

            byte[] text_bytes = Encoding.ASCII.GetBytes(requestText);

            queryList.AddRange(header);

            // this will be where we add our first count
            var startIndex = queryList.Count - 1;

            // add text in and replace the .'s with the length of the next element
            queryList.AddRange(text_bytes);

            // find and replace periods with appropriate number of elements
            int index = startIndex;
            while (index < queryList.Count)
            {
                if (queryList[index] == 0x2e) // period
                {
                    byte[] stringCount = BitConverter.GetBytes(index - (startIndex + 1)).TakeWhile(b => { return b != 0x00; }).ToArray();
                    if (BitConverter.IsLittleEndian) Array.Reverse(stringCount);
                    queryList.RemoveAt(index);
                    queryList.InsertRange(startIndex + 1, stringCount);
                    startIndex = index;
                }
                index++;
            }
            byte[] finaleStringCount = BitConverter.GetBytes(index - (startIndex + 1)).TakeWhile(b => { return b != 0x00; }).ToArray();
            if (BitConverter.IsLittleEndian) Array.Reverse(finaleStringCount);
            queryList.InsertRange(startIndex + 1, finaleStringCount);
            startIndex = index;

            // finish the string with a null
            queryList.Add(0x00);

            // finally add type and class
            queryList.AddRange(type);
            queryList.AddRange(dnsClass);

            // convert to array and return
            return queryList.ToArray();
        }

        public async void getData(string host, byte[] query)
        {
            try
            {
                //var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                var client = new UdpClient();

                IPAddress serverAddr = IPAddress.Parse(DEFUALT_DNS);
                IPEndPoint endPoint = new IPEndPoint(serverAddr, 53);

                // need to append type and class
                byte[] send_buffer = query;
                await client.SendAsync(send_buffer, send_buffer.Length, endPoint);

                var result = await client.ReceiveAsync();
                var resultString = Encoding.ASCII.GetString(result.Buffer);
                System.Console.WriteLine(resultString);

            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                System.Console.WriteLine("Unable to connect to the server");
            }

        }
    }
}
