using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MGT.Utilities.CRC
{
    public class CRC8
    {
        private byte crc8Poly;

        public CRC8(byte crc8poly)
        {
            this.crc8Poly = crc8poly;
        }

        public byte calculate(byte[] bytes)
        {
            int crc = 0;

            for (int index = 0; index < bytes.Length; index++)
            {
              crc = (crc ^ bytes[index]) & 0xFF;
              for (int loop = 0; loop < 8; loop++)
              {
                if ((crc & 0x1) == 1)
                  crc = crc >> 1 ^ crc8Poly;
                else
                  crc >>= 1;
              }
              crc &= 255;
            }

            return (byte)crc;
        }

        public byte calculate(byte[] bytes, int startIndex, int length)
        {
            int crc = 0;

            for (int i = startIndex; i < startIndex + length; i++)
            {
                crc = (crc ^ bytes[i]) & 0xFF;
                for (int loop = 0; loop < 8; loop++)
                {
                    if ((crc & 0x1) == 1)
                        crc = crc >> 1 ^ crc8Poly;
                    else
                        crc >>= 1;
                }
                crc &= 255;
            }

            return (byte)crc;
        }
    }
}
