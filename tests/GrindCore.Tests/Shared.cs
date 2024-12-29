using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrindCore.Tests
{
    internal class Shared
    {
        public static byte[] CreateData(int size)
        {
            // Initialize a new byte array with the specified size
            byte[] data = new byte[size];

            // Generate Fibonacci sequence and fill the array
            if (size > 0) data[0] = 0;
            if (size > 1) data[1] = 1;

            for (int i = 2; i < size; i++)
                data[i] = (byte)((data[i - 1] + data[i - 2]) % 256);

            return data;
        }
    }
}
