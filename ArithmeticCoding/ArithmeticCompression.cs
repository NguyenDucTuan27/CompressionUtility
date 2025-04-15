using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Interface;

namespace ArithmeticCoding
{
    public class ArithmeticCompression : ICompressionAlgorithm
    {
        
        private const int Precision = 32; // 32-bit precision
        private const uint TopValue = 0xFFFFFFFF; // 2^32 - 1   (1.0)
        private const uint Half = 0x80000000;     // 2^(Precision-1)    (0.5)
        private const uint Quarter = 0x40000000;  // 2^(Precision-2)    (0.25)
        private const uint ThreeQuarter = 0xC0000000; // half + quarter     (0.75)
        private const int EOF_SYMBOL = 256;// EOF flag

        /* Main entry for compression
         *  write meta data (data type, extension, file size)
         *  calculate frequency table and write it to output
         *  calculate cumulatitive frequency
         *  set low and high range
         *  encode each byte + EOF
         *  write final bits*/
        public void Compress(string inputFilePath, string outputFilePath)
        {
            try
            {
                byte[] inputData = File.ReadAllBytes(inputFilePath);
                bool isBinary = IsBinaryFile(inputFilePath);
                string fileExtension = Path.GetExtension(inputFilePath);

                using (var outFile = File.Open(outputFilePath, FileMode.Create))
                using (var writer = new BinaryWriter(outFile))
                {
                    writer.Write(isBinary);
                    writer.Write(fileExtension);
                    writer.Write(inputData.Length);  

                    Dictionary<int, int> frequencyTable = CountFrequencies(inputData);

                    writer.Write(frequencyTable.Count);
                    foreach (var k in frequencyTable)
                    {
                        writer.Write(k.Key);
                        writer.Write(k.Value);
                    }

                    var cumulativeFreq = CalculateCumulativeFrequencies(frequencyTable);

                    uint low = 0;
                    uint high = TopValue;
                    int pendingBits = 0;

                    using (var bitWriter = new BitWriter(outFile))
                    {
                        foreach (byte b in inputData)
                        {
                            EncodeSymbol(b, ref low, ref high, ref pendingBits, cumulativeFreq, bitWriter);
                        }
                        EncodeSymbol(EOF_SYMBOL, ref low, ref high, ref pendingBits, cumulativeFreq, bitWriter);

                        if (low < Quarter)
                        {
                            bitWriter.WriteBit(0);
                            for (int i = 0; i < pendingBits; i++)
                                bitWriter.WriteBit(1);
                        }
                        else
                        {
                            bitWriter.WriteBit(1);
                            for (int i = 0; i < pendingBits; i++)
                                bitWriter.WriteBit(0);
                        }

                        bitWriter.Flush();
                    }
                }

                Console.WriteLine($"Compressed {inputFilePath} to {outputFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Compression error: {ex.Message}");
                throw;
            }
        }
        /* Main entry for decompression
         *  read meta data
         *  read frequency table
         *  calculate cumulative frequency
         *  create a lookup table for finding symbols
         *  initialize low, high and value and read 32 bits into value
         *  decoding process
         *  write output*/
        public void Decompress(string inputFilePath, string outputFilePath)
        {
            try
            {
                using (var inFile = File.Open(inputFilePath, FileMode.Open))
                using (var reader = new BinaryReader(inFile))
                {
                    bool isBinary = reader.ReadBoolean();
                    string fileExtension = reader.ReadString();

                    /*if (string.IsNullOrEmpty(Path.GetExtension(outputFilePath)))
                        outputFilePath = outputFilePath + fileExtension;*/

                    int originalSize = reader.ReadInt32();
                    int tableCount = reader.ReadInt32();
                    Dictionary<int, int> frequencyTable = new Dictionary<int, int>();
                    for (int i = 0; i < tableCount; i++)
                    {
                        int symbol = reader.ReadInt32();
                        int frequency = reader.ReadInt32();
                        frequencyTable[symbol] = frequency;
                    }

                    var cumulativeFreq = CalculateCumulativeFrequencies(frequencyTable);
                    var symbolLookup = BuildSymbolLookup(cumulativeFreq);
                    uint low = 0;
                    uint high = TopValue;
                    uint value = 0;

                    byte[] outputData = new byte[originalSize];
                    int outputIndex = 0;

                    using (var bitReader = new BitReader(inFile))
                    {
                        for (int i = 0; i < Precision; i++)
                        {
                            value = (value << 1) | (uint)bitReader.ReadBit();
                        }
                        while (true)
                        {
                            int symbol = DecodeSymbol(value, low, high, symbolLookup);

                            if (symbol == EOF_SYMBOL)
                                break;

                            if (outputIndex < originalSize)
                                outputData[outputIndex++] = (byte)symbol;

                            UpdateDecoderState(symbol, ref low, ref high, ref value, cumulativeFreq, bitReader);
                            if (outputIndex >= originalSize)
                                break;
                        }
                    }
                    File.WriteAllBytes(outputFilePath, outputData);
                    Console.WriteLine($"Decompressed {inputFilePath} to {outputFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Decompression error: {ex.Message}");
                throw;
            }
        }

        /* Count frequency of each byte in the input data
         *  create a dict where each key has occurences count as byte values (0-255) and a EOF symbol (256)
         *  start every count at 1 to avoid 0 probability. every symbol has at least 1 occurence
         *  count occurences for each byte*/
        private Dictionary<int, int> CountFrequencies(byte[] data)
        {
            var frequencies = new Dictionary<int, int>();

            for (int i = 0; i <= 256; i++)
                frequencies[i] = 1; 
            foreach (byte b in data)
                frequencies[b]++;

            return frequencies;
        }

        /* Calculate cumulative frequencies for each symbol
         *  create an empty dict to store cumulative frequencies
         *  calculate the sum of all keys in the frequency table
         *  initialize cumulative frequency to 0
         *  sort the frequency table by key
         *  calculate the probability of each symbol
         *      calculate key's probability
         *      create a tuple with low and high range
         *      update cumulative frequency for the next symbol
         *  output dict with keys and tuple of low and high range from frequency table*/
        private Dictionary<int, (double Low, double High)> CalculateCumulativeFrequencies(Dictionary<int, int> frequencies)
        {
            var result = new Dictionary<int, (double Low, double High)>();
            long total = frequencies.Values.Sum(v => (long)v); //convert to long to avoid overflow
            double cumulative = 0.0;

            foreach (var keyValuePair in frequencies.OrderBy(x => x.Key))
            {
                double probability = (double)keyValuePair.Value / total;
                result[keyValuePair.Key] = (cumulative, cumulative + probability);
                cumulative += probability;
            }

            return result;
        }

        /* Encode a single symbol
         *  calculate current interval range (initial: 0 to 2^32-1)
         *  update range based on symbol's probability range
         *  bit shifting:
         *      both high and low are in lower half
         *          write 0 and pending bits when pendingbits > 0
         *          shift low and high left by 1 (double the range)
         *      both high and low are in upper half
         *          write 1 and pending bits when pendingbits > 0
         *          shift low and high left by 1 and subtract half
         *      both high and low are in middle (0.25-0.75)
         *          increment pending bits
         *          shift low and high to turn the current range into [0, 1.0]
         *      else break (no scaling needed)
         *  */
        private void EncodeSymbol(int symbol, ref uint low, ref uint high, ref int pendingBits, Dictionary<int, (double Low, double High)> cumulativeFreq, BitWriter writer)
        {
            ulong range = (ulong)(high - low) + 1; // +1 to get [) range
            high = low + (uint)(range * cumulativeFreq[symbol].High) - 1;
            low = low + (uint)(range * cumulativeFreq[symbol].Low);

            while (true)
            {
                if (high < Half) 
                {
                    writer.WriteBit(0);
                    for (int i = 0; i < pendingBits; i++)
                        writer.WriteBit(1);
                    pendingBits = 0;

                    low <<= 1;
                    high = (high << 1) | 1;
                }
                else if (low >= Half)
                {
                    writer.WriteBit(1);
                    for (int i = 0; i < pendingBits; i++)
                        writer.WriteBit(0);
                    pendingBits = 0;

                    low = (low - Half) << 1;
                    high = ((high - Half) << 1) | 1;
                }
                else if (low >= Quarter && high < ThreeQuarter)
                {
                    pendingBits++;
                    low = (low - Quarter) << 1;
                    high = ((high - Quarter) << 1) | 1;
                }
                else
                {
                    break; 
                }
            }
        }

        /* Build a lookup table for quick symbol decoding*/
        private (int Symbol, double Low, double High)[] BuildSymbolLookup(Dictionary<int, (double Low, double High)> cumulativeFreq)
        {
            return cumulativeFreq
                .Select(kvp => (kvp.Key, kvp.Value.Low, kvp.Value.High))
                .OrderBy(item => item.Low)
                .ToArray();
        }

        /* Decode a symbol based on current value
         *  calculate the size of the current decoding range
         *  reverse the encoding process by scaling the value to [0, 1]
         *  find the symbol using binary search with scaled value
         *      if scaled is not exactly found, find the closest match
         *  fallback (due to floating point precision)
         *  */
        private int DecodeSymbol(uint value, uint low, uint high, (int Symbol, double Low, double High)[] symbolLookup)
        {
            ulong range = (ulong)(high - low) + 1;
            double scaledValue = (double)(value - low + 1) / range;

            int left = 0;
            int right = symbolLookup.Length - 1;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                var item = symbolLookup[mid];

                if (scaledValue < item.Low)
                    right = mid - 1;
                else if (scaledValue >= item.High)
                    left = mid + 1;
                else
                    return item.Symbol;
            }

            for (int i = 0; i < symbolLookup.Length; i++)
            {
                if (scaledValue >= symbolLookup[i].Low && scaledValue < symbolLookup[i].High)
                    return symbolLookup[i].Symbol;
            }

            return symbolLookup[0].Symbol; 
        }

        /* Update decoding range based on the previous decoded symbol 
         *  same logic as EncodeSymbol but shift value using sliding window
         *  each case reads a new bit to maintain value*/
        private void UpdateDecoderState(int symbol, ref uint low, ref uint high, ref uint value,
                                       Dictionary<int, (double Low, double High)> cumulativeFreq, BitReader reader)
        {
            ulong range = (ulong)(high - low) + 1;

            high = low + (uint)(range * cumulativeFreq[symbol].High) - 1;
            low = low + (uint)(range * cumulativeFreq[symbol].Low);

          
            while (true)
            {
                if (high < Half) 
                {
                    low <<= 1;
                    high = (high << 1) | 1;
                    value = (value << 1) | (uint)reader.ReadBit();
                }
                else if (low >= Half)
                {
                    low = (low - Half) << 1;
                    high = ((high - Half) << 1) | 1;
                    value = ((value - Half) << 1) | (uint)reader.ReadBit();
                }
                else if (low >= Quarter && high < ThreeQuarter)  
                {
                    low = (low - Quarter) << 1;
                    high = ((high - Quarter) << 1) | 1;
                    value = ((value - Quarter) << 1) | (uint)reader.ReadBit();
                }
                else
                {
                    break;  
                }
            }
        }

        private bool IsBinaryFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension == ".pdf" || extension == ".docx" || extension == ".xlsx" ||
                   extension == ".jpg" || extension == ".png" || extension == ".zip" ||
                   extension == ".mp3" || extension == ".mp4" || extension == ".wav" ||
                   extension == ".gif" || extension == ".exe" || extension == ".dll";
        }
        private class BitWriter : IDisposable
        {
            private Stream stream;
            private byte buffer;
            private int bitsInBuffer;

            public BitWriter(Stream stream)
            {
                this.stream = stream;
                buffer = 0;
                bitsInBuffer = 0;
            }

            public void WriteBit(int bit)
            {
                buffer = (byte)((buffer << 1) | (bit & 1)); //add to buf
                bitsInBuffer++;


                if (bitsInBuffer == 8) //write when full
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

        private class BitReader : IDisposable
        {
            private Stream stream;
            private int buffer;
            private int bitsLeft;

            public BitReader(Stream stream)
            {
                this.stream = stream;
                buffer = 0;
                bitsLeft = 0;
            }

            public int ReadBit()
            {
                if (bitsLeft == 0) //refill buf when empty
                {
                    buffer = stream.ReadByte();
                    if (buffer == -1)
                        return 0; 
                    bitsLeft = 8;
                }

                int bit = (buffer >> (bitsLeft - 1)) & 1;
                bitsLeft--;
                return bit;
            }

            public void Dispose()
            {
            }
        }
    }
}