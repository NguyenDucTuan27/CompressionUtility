using System;
using System.Collections.Generic;
using System.IO;
using Interface;

namespace LZW
{
    public class LZWcompression : ICompressionAlgorithm
    {
        private const int INITIAL_BITS = 9;      // initial size of dictionary codes
        private const int MAX_BITS = 12;         // maximum size of dictionary codes

        private const int EOS_CODE = 256;        // code for end of compressed data
        private const int CLEAR_CODE = 257;      // code for clear/reset dictionary
        private const int FIRST_CODE = 258;      // first entry when compressing

        /* Main entry for compressing
         *  check if file is already compressed format
         *  read the input file as bytes
         *  write metadata (file extention, original size)
         *  skip compression if a already compressed format
         *  compressToStream and convert it back to byte array
         *  check if compressed file is bigger than original file or not*/
        public void Compress(string inputFilePath, string outputFilePath)
        {
            try
            {
                // Check if this file is likely already compressed
                bool isLikelyCompressed = IsLikelyCompressedFile(inputFilePath);
                byte[] inputData = File.ReadAllBytes(inputFilePath);

                using (FileStream outFile = File.Create(outputFilePath))
                using (BinaryWriter writer = new BinaryWriter(outFile))
                {
                    // Write metadata
                    writer.Write(Path.GetExtension(inputFilePath));
                    writer.Write(inputData.Length);

                    if (isLikelyCompressed)
                    {
                        writer.Write(false); // flag: not compressed
                        writer.Write(inputData);
                        Console.WriteLine($"Skipped compression for {inputFilePath} (likely already compressed)");
                        return;
                    }

                    using (MemoryStream compressedData = new MemoryStream()) //in-memory buffer to hold compressed data
                    using (BitOutputStream bitStream = new BitOutputStream(compressedData)) // bit-level writing
                    {
                        CompressToStream(inputData, bitStream);
                        bitStream.Flush();

                        byte[] compressed = compressedData.ToArray(); // convert memory stream to byte array

                        if (compressed.Length < inputData.Length)
                        {
                            writer.Write(true); // flag compressed
                            writer.Write(compressed.Length);
                            writer.Write(compressed);
                            Console.WriteLine($"Compressed {inputFilePath} from {inputData.Length} to {compressed.Length} bytes");
                        }
                        else
                        {
                            writer.Write(false); // flag not compressed
                            writer.Write(inputData);
                            Console.WriteLine($"LZW ineffective for {inputFilePath}, stored uncompressed");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error compressing file: {ex.Message}");
                throw;
            }
        }

        /* Main entry for decompressing
         *  read input stream - metadata (extension, original size)
         *  check if the file was compressed(1) or skipped(0)
         *  if 1 -> read compressed data size
         *      load the compressed data into memory and create a memory stream with it
         *      process in bit-level
         *      decompress from stream and write the output*/
        public void Decompress(string inputFilePath, string outputFilePath)
        {
            try
            {
                using (FileStream inFile = File.OpenRead(inputFilePath))
                using (BinaryReader reader = new BinaryReader(inFile))
                {
                    // Read metadata
                    string extension = reader.ReadString();
                    int originalSize = reader.ReadInt32();
                    if (string.IsNullOrEmpty(Path.GetExtension(outputFilePath)))
                    {
                        outputFilePath = outputFilePath + extension;
                    }

                    bool isCompressed = reader.ReadBoolean();

                    if (isCompressed)
                    {
                        int compressedSize = reader.ReadInt32();
                        byte[] compressedData = reader.ReadBytes(compressedSize);

                        using (MemoryStream ms = new MemoryStream(compressedData))
                        using (BitInputStream bitStream = new BitInputStream(ms))
                        {
                            byte[] decompressed = DecompressFromStream(bitStream, originalSize);
                            File.WriteAllBytes(outputFilePath, decompressed);
                            Console.WriteLine($"Decompressed {inputFilePath} to {outputFilePath} ({decompressed.Length} bytes)");
                        }
                    }
                    else
                    {
                        // Read uncompressed data
                        byte[] data = reader.ReadBytes(originalSize);
                        File.WriteAllBytes(outputFilePath, data);
                        Console.WriteLine($"Extracted {inputFilePath} to {outputFilePath} ({data.Length} bytes)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decompressing file: {ex.Message}");
                throw;
            }
        }
        /* Core LZW algorithm
         *  initialize dictionary byte sequences to integer codes, 256 codes for single bytes 
         *  skip 256 and 257 for EOS and clear markers. write clear_code with current bit size
         *  process each byte in the input
         *      check if newPattern exist
         *          if yes-> keep growing
         *          if not -> output current pattern code
         *              check if the dict reach current bit size to extend the dict until bit size reaches 12 -> reset
         *  output code for remaining pattern
         *  write EOS code*/
        private void CompressToStream(byte[] data, BitOutputStream output)
        {  
            Dictionary<List<byte>, int> dictionary = new Dictionary<List<byte>, int>(new ByteListComparer());
            for (int i = 0; i < 256; i++)
            {
                dictionary[new List<byte> { (byte)i }] = i;
            }

            int currentCodeSize = INITIAL_BITS;
            int nextCode = FIRST_CODE;

            output.WriteBits(CLEAR_CODE, currentCodeSize);

            List<byte> pattern = new List<byte>();

            foreach (byte b in data)
            {
                List<byte> newPattern = new List<byte>(pattern);
                newPattern.Add(b);

                if (dictionary.ContainsKey(newPattern))
                {
                    pattern = newPattern;
                }
                else
                {
                    output.WriteBits(dictionary[pattern], currentCodeSize);
                    int maxCode = (1 << currentCodeSize) - 1; //2^currentCodeSize - 1
                    if (nextCode <= maxCode)
                    {
                        dictionary[newPattern] = nextCode++;

                        if (nextCode == maxCode && currentCodeSize < MAX_BITS)
                        {
                            currentCodeSize++;
                        }
                        else if (nextCode > maxCode && currentCodeSize == MAX_BITS)
                        {
                            output.WriteBits(CLEAR_CODE, currentCodeSize);
                            dictionary.Clear();
                            for (int i = 0; i < 256; i++)
                            {
                                dictionary[new List<byte> { (byte)i }] = i;
                            }
                            nextCode = FIRST_CODE;
                            currentCodeSize = INITIAL_BITS;
                        }
                    }
                    pattern = new List<byte> { b };
                }
            }
            if (pattern.Count > 0)
            {
                output.WriteBits(dictionary[pattern], currentCodeSize);
            }
            output.WriteBits(EOS_CODE, currentCodeSize); //EOS
        }
        /* Reverse compression process
         *  initialize dictionary byte sequences to integer codes, 256 codes for single bytes 
         *  skip 256 and 257 for EOS and clear markers. write clear_code with current bit size
         *  while loop to read from input stream
         *      check for EOS and Clear_code markers
         *      handle other codes
         *          if code in dict -> output the pattern
         *          add new entry to dict if there is an oldcode
         *      handle special case for pattern + pattern[0]*/
        private byte[] DecompressFromStream(BitInputStream input, int originalSize)
        {
            List<byte> result = new List<byte>(originalSize);
            Dictionary<int, List<byte>> dictionary = new Dictionary<int, List<byte>>();
            for (int i = 0; i < 256; i++)
            {
                dictionary[i] = new List<byte> { (byte)i };
            }

            int currentCodeSize = INITIAL_BITS;
            int nextCode = FIRST_CODE;

            int code = input.ReadBits(currentCodeSize); //clear code
            if (code != CLEAR_CODE)
            {
                throw new InvalidDataException("Invalid LZW data - missing clear code");
            }

            int oldCode = -1;
            List<byte> pattern = null;

            while (true)
            {
                code = input.ReadBits(currentCodeSize);
                if (code == EOS_CODE)
                    break;
                if (code == CLEAR_CODE)
                {
                    dictionary.Clear();
                    for (int i = 0; i < 256; i++)
                    {
                        dictionary[i] = new List<byte> { (byte)i };
                    }
                    nextCode = FIRST_CODE;
                    currentCodeSize = INITIAL_BITS;
                    oldCode = -1;
                    continue;
                }
                if (dictionary.ContainsKey(code))
                {
                    pattern = dictionary[code];
                    result.AddRange(pattern);
                    if (oldCode >= 0)
                    {
                        int maxCode = (1 << currentCodeSize) - 1; //2^currentCodeSize - 1
                        if (nextCode <= maxCode)
                        {
                            List<byte> newPattern = new List<byte>(dictionary[oldCode]);
                            newPattern.Add(pattern[0]);
                            dictionary[nextCode++] = newPattern;
                            if (nextCode == maxCode && currentCodeSize < MAX_BITS)
                            {
                                currentCodeSize++;
                            }
                        }
                    }
                }
                else
                { //special case for pattern + pattern[0]
                    if (code == nextCode && oldCode >= 0)
                    {
                        pattern = new List<byte>(dictionary[oldCode]);
                        pattern.Add(pattern[0]);
                        result.AddRange(pattern);
                        dictionary[nextCode++] = pattern;
                        int maxCode = (1 << currentCodeSize) - 1;
                        if (nextCode == maxCode && currentCodeSize < MAX_BITS)
                        {
                            currentCodeSize++;
                        }
                    }
                    else
                    {
                        throw new InvalidDataException($"Invalid LZW code: {code}");
                    }
                }

                oldCode = code;
                if (result.Count >= originalSize)
                    break;
            }

            return result.ToArray();
        }

        /* Check for already compressed formats*/
        private bool IsLikelyCompressedFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            string[] compressedExtensions = {
                ".zip", ".rar", ".7z",
            };
            return Array.IndexOf(compressedExtensions, extension) >= 0;
        }

        /* support classes to identify indentical byte patterns not from memory refs*/
        private class ByteListComparer : IEqualityComparer<List<byte>>
        {
            /*only return true if all bytes match*/
            public bool Equals(List<byte> x, List<byte> y)
            {
                if (x.Count != y.Count)
                    return false;

                for (int i = 0; i < x.Count; i++)
                {
                    if (x[i] != y[i])
                        return false;
                }

                return true;
            }

            public int GetHashCode(List<byte> obj)
            {
                int hash = 17;
                foreach (byte b in obj)
                {
                    hash = hash * 31 + b;
                }
                return hash;
            }
        }

        // Bit-level I/O classes
        // Bit-level I/O classes
        private class BitOutputStream : IDisposable
        {
            private Stream stream;
            private byte buffer;
            private int bitsInBuffer;

            public BitOutputStream(Stream stream)
            {
                this.stream = stream;
                buffer = 0;
                bitsInBuffer = 0;
            }
            /* write bit from MSB to LSB*/
            public void WriteBits(int value, int bitCount)
            {
                for (int i = bitCount - 1; i >= 0; i--)
                {
                    int bit = (value >> i) & 1;
                    WriteBit(bit);
                }
            }

            private void WriteBit(int bit)
            {
                // Add bit to buffer
                buffer = (byte)((buffer << 1) | (bit & 1));
                bitsInBuffer++;

                // Write buffer when full
                if (bitsInBuffer == 8)
                {
                    stream.WriteByte(buffer);
                    buffer = 0;
                    bitsInBuffer = 0;
                }
            }

            public void Flush()
            {
                if (bitsInBuffer > 0)
                {
                    buffer = (byte)(buffer << (8 - bitsInBuffer));
                    stream.WriteByte(buffer);
                    buffer = 0;
                    bitsInBuffer = 0;
                }
            }

            public void Dispose()
            {
                Flush();

            }
        }

        private class BitInputStream : IDisposable
        {
            private Stream stream;
            private byte buffer;
            private int bitsLeft;
            private bool endOfStream;

            public BitInputStream(Stream stream)
            {
                this.stream = stream;
                buffer = 0;
                bitsLeft = 0;
                endOfStream = false;
            }

            public int ReadBits(int bitCount)
            {
                int result = 0;

                for (int i = 0; i < bitCount; i++)
                {
                    result = (result << 1) | ReadBit();
                }

                return result;
            }
            /* Bits are written form right to left where 8th bit is MSB*/
            private int ReadBit()
            {
                // Refill buffer if needed
                if (bitsLeft == 0)
                {
                    int nextByte = stream.ReadByte();
                    if (nextByte == -1)
                    {
                        endOfStream = true;
                        return 0; //padding
                    }

                    buffer = (byte)nextByte;
                    bitsLeft = 8;
                }

                // extract most significant bit
                int bit = (buffer >> 7) & 1;

                // shift buffer left by 1
                buffer = (byte)(buffer << 1);
                bitsLeft--;

                return bit;
            }

            public void Dispose()
            {
            }
        }
    }
}