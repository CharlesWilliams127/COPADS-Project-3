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
        const int ANSWER_RR_INDEX = 6;
        const byte REFERENCE_BYTE = 0xc0;
        const byte PERIOD_BYTE = 0x2e;

        // offsets for parsing results and headers
        // all offsets are with respect to the start of the particular answer
        const int TYPE_OFFSET = 2;
        const int CLASS_OFFSET = 4;
        const int TTL_OFFSET = 6;
        const int DATA_LENGTH_OFFSET = 10;
        const int DATA_OFFSET = 12;

        // DNS Answers
        List<DNSAnswer> answers;

        public static void Main(string[] args)
        {
            //switch(args.Length)
            //{
            //    case 1:
            //        break;
            //    case 2:
            //        break;
            //    case 3:
            //        break;
            //    default:
            //        break;
            //}
            if (args.Length == 1)
            {
                var host = args[0];
                var p1 = new Program();
                p1.getData(host, p1.prepareQuery("www.rit.edu", "AAAA"));
                Console.ReadKey();
            }
            else
            {
                System.Console.WriteLine("Usage: dotnet run <host>");
            }
        }

        public byte[] prepareQuery(string requestText, string typeString)
        {
            byte[] header = { 0xAA, 0xAA, // Transaction ID
                    0x01, 0x20, // Query Params
                    0x00, 0x01, // Number of Questions
                    0x00, 0x00, // Number of Answers
                    0x00, 0x00, // Number of Authority Records
                    0x00, 0x00 }; // Number of Additional Records

            byte[] type = new byte[2];
            // determine type
            if (typeString == "AAAA")
            {
                type[0] = 0x00;
                type[1] = 0x1c;
            }
            else // anything else we'll send as the defualt
            {
                type[0] = 0x00;
                type[1] = 0x01;
            }

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
                if (queryList[index] == PERIOD_BYTE) // period
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
                answers = new List<DNSAnswer>();
                var client = new UdpClient();

                IPAddress serverAddr = IPAddress.Parse(DEFUALT_DNS);
                IPEndPoint endPoint = new IPEndPoint(serverAddr, 53);

                // need to append type and class
                byte[] send_buffer = query;
                await client.SendAsync(send_buffer, send_buffer.Length, endPoint);

                var result = await client.ReceiveAsync();

                // THIS IS WHERE THE FUN BEGINS
                // find out how many responses there are
                var resultList = result.Buffer.ToList();
                int responseNum = (resultList[ANSWER_RR_INDEX] << 8) | resultList[ANSWER_RR_INDEX + 1];
                var answerStartIndex = query.Count();

                // arrays to keep track of data as we parse it
                string[] names = new string[responseNum];
                string[] types = new string[responseNum];
                int[] ttls = new int[responseNum];
                int[] lengths = new int[responseNum];
                string[] dataSections = new string[responseNum];

                // index to keep track of where we are in the stream
                int index = answerStartIndex;

                // create lists to store full parsed answers
                for (int i = 0; i < responseNum; i++)
                {
                    answers.Add(new DNSAnswer());
                }

                foreach(DNSAnswer answer in answers)
                {
                    index = answer.parseBytes(resultList, index);
                    Console.WriteLine(answer.name + " " + answer.classType + " " + answer.type + " " + answer.data);
                }

                // finally print everything
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                System.Console.WriteLine("Unable to connect to the server");
            }

        }
    }
}
