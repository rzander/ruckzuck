// ************************************************************************************
//          jaindb (c) Copyright 2018 by Roger Zander
// ************************************************************************************

using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace RZ.Server
{
    public static class Hash
    {
        //Base58 Digits
        private const string Digits = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        public static byte[] CalculateSHA2_256Hash(string input)
        {
            SHA256 sha = SHA256.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = sha.ComputeHash(inputBytes);
            byte[] mhash = new byte[hash.Length + 2]; //we need two additional bytes

            //Add Multihash identifier
            hash.CopyTo(mhash, 2);
            mhash[0] = 0x12; //SHA256
            mhash[1] = Convert.ToByte(hash.Length); //Hash length

            return mhash;
        }

        public static string CalculateSHA2_256HashString(string input)
        {
            return Encode58(CalculateSHA2_256Hash(input));
        }

        public static byte[] CalculateMD5Hash(string input)
        {
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);
            byte[] mhash = new byte[hash.Length + 2];
            hash.CopyTo(mhash, 2);
            //Add Multihash identifier
            mhash[0] = 0xD5; //MD5
            mhash[1] = Convert.ToByte(hash.Length); //Hash legth
            return mhash;
        }

        public static string CalculateMD5HashString(string input)
        {
            return Encode58(CalculateMD5Hash(input));
        }

        public static string Encode58(byte[] data)
        {
            // Decode byte[] to BigInteger
            BigInteger intData = 0;
            for (int i = 0; i < data.Length; i++)
            {
                intData = intData * 256 + data[i];
            }

            // Encode BigInteger to Base58 string
            string result = "";
            while (intData > 0)
            {
                int remainder = (int)(intData % 58);
                intData /= 58;
                result = Digits[remainder] + result;
            }

            // Append `1` for each leading 0 byte
            for (int i = 0; i < data.Length && data[i] == 0; i++)
            {
                result = '1' + result;
            }

            return result;
        }

        public static bool checkTrailingZero(byte[] bHash, int complexity, string sGoal = "")
        {
            bool bRes = false;
            try
            {
                if (complexity > 0)
                {
                    if (string.IsNullOrEmpty(sGoal)) //create TrailingZero string if it does not exists
                        sGoal = new string('0', complexity);

                    //Check the last n Bits of the hash if they are 0, where n is the complexity
                    int iBytes = 1 + (complexity / 8); //Nr of bytes we have toc get
                    var aLast = bHash.Skip(bHash.Length - iBytes); //Get the last n Bytes
                    string sRes = string.Join("", aLast.Select(x => Convert.ToString(x, 2).PadLeft(8, '0'))); //Convert to bit string

                    if (sRes.Substring(sRes.Length - complexity) == sGoal) //do we have a match ?
                        return true;
                }
                else
                    return true;
            }
            catch { }

            return bRes;
        }
    }
}
