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
        const string DEFUALT_DNS = "129.21.3.17";
        const int ANSWER_RR_INDEX = 6;
        const byte REFERENCE_BYTE = 0xc0;
        const byte PERIOD_BYTE = 0x2e;

        public static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                var host = args[0];
                var p1 = new Program();
                p1.getData(host, p1.prepareQuery("www.snapchat.com", "AAAA"));
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

        public static List<byte> parsePointer(List<byte> operateList, List<byte> initList,int index, int count)
        {
            operateList.AddRange(initList.GetRange(index + 1, count));

            if (initList[index + count + 1] == 0x00)
            {
                return operateList;
            }
            else // also signifies a period should be added
            {
                operateList.Add(PERIOD_BYTE);
                return parsePointer(operateList, initList, index + count + 1, initList[index + count + 1]);
            }
        }

        public async void getData(string host, byte[] query)
        {
            try
            {
                var client = new UdpClient();

                IPAddress serverAddr = IPAddress.Parse(DEFUALT_DNS);
                IPEndPoint endPoint = new IPEndPoint(serverAddr, 53);

                // need to append type and class
                byte[] send_buffer = query;
                await client.SendAsync(send_buffer, send_buffer.Length, endPoint);

                var result = await client.ReceiveAsync();
                var resultString = Encoding.ASCII.GetString(result.Buffer);
                System.Console.WriteLine(resultString);

                // THIS IS WHERE THE FUN BEGINS
                // find out how many responses there are
                var resultList = result.Buffer.ToList();
                int responseNum = (resultList[ANSWER_RR_INDEX] << 8) | resultList[ANSWER_RR_INDEX + 1];
                var answerStartIndex = query.Count();

                // index to keep track of where we are in the stream
                int index = answerStartIndex;

                // create lists to store full parsed answers
                List<byte>[] parsedResponses = new List<byte>[responseNum];
                for (int i = 0; i < responseNum; i++)
                {
                    parsedResponses[i] = new List<byte>();
                }

                // bool to kick us out of the loop when one of our responses has ended
                bool responseParsed = false;

                // begin to loop through answer section responses
                for (int i = 0; i < responseNum; i++)
                {
                    responseParsed = false;
                    while (!responseParsed)
                    {
                        // check if data is a pointer
                        if (resultList[index] == REFERENCE_BYTE)
                        {
                            // our next byte is the offset into the stream
                            // first byte into the offset will be the count
                            // create a sub for loop to parse string pointed at
                            int pointerIndex = (int)resultList[index + 1];

                            // read in string from pointed at bit
                            int pointerStringLength = (int)resultList[pointerIndex];

                            // add in this range to our parsed responses
                            //for(int j = pointerIndex; i < pointerStringLength; i+=)
                            //parsedResponses[i].AddRange(resultList.GetRange(pointerIndex, pointerStringLength));
                            parsedResponses[i] = parsePointer(parsedResponses[i], resultList, pointerIndex, pointerStringLength);
                        }

                        // reached the beginning of the length section for actual data
                        if (index == answerStartIndex + 10)
                        {
                            // this byte and the next will be the length of the data section
                            int dataStringLength = (resultList[index] << 8 | resultList[index + 1]);

                            parsedResponses[i].AddRange(resultList.GetRange(index + 2, dataStringLength));

                            // we've read this data and added it to the appropriate array
                            // mark this data as parsed
                            responseParsed = true;
                        }
                        index++;
                    }
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e);
                System.Console.WriteLine("Unable to connect to the server");
            }

        }
    }
}
