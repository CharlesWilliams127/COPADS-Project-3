using System;
using System.Collections.Generic;
using System.Text;

namespace DNSClientApp
{
    class DNSAnswer
    {
        // bytes used for parsing
        const byte REFERENCE_BYTE = 0xc0;
        const byte PERIOD_BYTE = 0x2e;
        const byte NULL_BYTE = 0x00;

        // offsets for parsing results and headers
        // all offsets are with respect to the start of the particular answer
        const int TYPE_OFFSET = 2;
        const int CLASS_OFFSET = 4;
        const int TTL_OFFSET = 6;
        const int DATA_LENGTH_OFFSET = 10;
        const int DATA_OFFSET = 12;

        // storage for fields pulled out of the DNS response stream
        public string name;
        public string type;
        public string classType;
        public int timeToLive;
        public int dataLength;
        public string data;

        // private list of data for internal tracking
        private List<byte> parsedData;

        public DNSAnswer()
        {
            parsedData = new List<byte>();
        }

        private List<byte> parsePointerInternal(List<byte> operateList, List<byte> initList, int index, int count)
        {
            operateList.AddRange(initList.GetRange(index + 1, count));

            if (initList[index + count + 1] == NULL_BYTE)
            {
                return operateList;
            }
            else if (initList[index + count + 1] == REFERENCE_BYTE)
            {
                return parsePointer(operateList, initList, index + count + 1);
            }
            else // also signifies a period should be added
            {
                operateList.Add(PERIOD_BYTE);
                return parsePointerInternal(operateList, initList, index + count + 1, initList[index + count + 1]);
            }
        }

        private List<byte> parsePointer(List<byte> operateList, List<byte> initList, int index)
        {
            // our next byte is the offset into the stream
            // first byte into the offset will be the count
            // create a sub for loop to parse string pointed at
            int pointerIndex = (int)initList[index + 1];

            // read in string from pointed at bit
            int pointerStringLength = (int)initList[pointerIndex];

            return parsePointerInternal(operateList, initList, pointerIndex, pointerStringLength);
        }

        private List<byte> parseString(List<byte> operateList, List<byte> initList, int index, int count)
        {
            operateList.AddRange(initList.GetRange(index + 1, count));

            if(initList[index + count + 1] == NULL_BYTE)
            {
                return operateList;
            }
            else if(initList[index + count + 1] == REFERENCE_BYTE)
            {
                return parsePointer(operateList, initList, index + count + 1);
            }
            else
            {
                operateList.Add(PERIOD_BYTE);
                return parseString(operateList, initList, index + count + 1, initList[index + count + 1]);
            }
        }

        public int parseBytes(List<byte> resultList, int index)
        {

            //* ONLY NAME AND RDATA have variable lengths *//

            // check if data is a pointer
            if (resultList[index] == REFERENCE_BYTE)
            {
                // our next byte is the offset into the stream
                // first byte into the offset will be the count
                // create a sub for loop to parse string pointed at
                parsedData = parsePointer(parsedData, resultList, index);
            }
            //else // no pointer, just data, meaning this is a count
            //{
            //    int nameLength = (int)(resultList[index] << 8 | resultList[index + 1]);

            //    // check to ensure the added data contained no pointers
            //    if (resultList.GetRange(index + TYPE_OFFSET, nameLength).Contains(REFERENCE_BYTE))
            //    {
            //        parsedData = parsePointer(parsedData, resultList, resultList.GetRange(index + TYPE_OFFSET, nameLength).IndexOf(REFERENCE_BYTE));
            //    }
            //    else
            //    {
            //        parsedData.AddRange(resultList.GetRange(index + TYPE_OFFSET, nameLength));
            //    }
            //}
            // assign this value to the name
            name = Encoding.ASCII.GetString(parsedData.ToArray());

            // add NUL for later processing to show we're done with the name
            parsedData.Add(NULL_BYTE);

            if (resultList[index + TYPE_OFFSET] == 0x00 && resultList[index + TYPE_OFFSET + 1] == 0x05)
            {
                type = "CNAME";
            }
            else if (resultList[index + TYPE_OFFSET] == 0x00 && resultList[index + TYPE_OFFSET + 1] == 0x01)
            {
                type = "A";
            }
            else if (resultList[index + TYPE_OFFSET] == 0x00 && resultList[index + TYPE_OFFSET + 1] == 0x1c)
            {
                type = "AAAA";
            }
            else
            {
                type = "UNKNOWN TYPE";
            }

            // class parsing 
            if (resultList[index + CLASS_OFFSET] == 0x00 && resultList[index + CLASS_OFFSET + 1] == 0x01)
            {
                classType = "IN";
            }
            else
            {
                classType = "UNKOWN CLASS";
            }

            byte[] ttlBytes = resultList.GetRange(index + TTL_OFFSET, 4).ToArray();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(ttlBytes);
            timeToLive = BitConverter.ToInt32(ttlBytes, 0);

            // reached the beginning of the length section for actual data
            // this byte and the next will be the length of the data section
            // check if data is a pointer
            List<byte> dataList = new List<byte>();

            if (type == "CNAME")
            {
                if (resultList[index + DATA_OFFSET] == REFERENCE_BYTE)
                {
                    // our next byte is the offset into the stream
                    // first byte into the offset will be the count
                    // create a sub for loop to parse string pointed at
                    dataList = parsePointer(parsedData, resultList, index + DATA_OFFSET);
                }
                else // no pointer, just data, meaning this is a count
                {
                    dataLength = (resultList[index + DATA_LENGTH_OFFSET] << 8 | resultList[index + DATA_LENGTH_OFFSET + 1]);

                    dataList = parseString(dataList, resultList, index + DATA_OFFSET, resultList[index + DATA_OFFSET]);
                }

                data = Encoding.ASCII.GetString(dataList.ToArray());
            }
            else if (type == "A")
            {
                // should always be 4
                dataLength = (resultList[index + DATA_LENGTH_OFFSET] << 8 | resultList[index + DATA_LENGTH_OFFSET + 1]);

                for (int i = index + DATA_OFFSET; i < index + DATA_OFFSET + dataLength; i++)
                {
                    data += (((int)resultList[i]).ToString());
                    data += ".";
                }

                data = data.Remove(data.LastIndexOf('.'));
            }

            //parsedData.AddRange(resultList.GetRange(index + DATA_OFFSET, dataLength));
            

            // returns the index the next answer section should begin at
            return index + DATA_OFFSET + dataLength;
        }
    }
}
