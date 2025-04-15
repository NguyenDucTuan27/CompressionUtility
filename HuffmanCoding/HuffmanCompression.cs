using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Interface;

namespace HuffmanCoding
{
    public class HuffmanCompression : ICompressionAlgorithm
    {
        private class Node
        {
            public byte Value { get; set; } 
            public int Frequency { get; set; }
            public Node? Left { get; set; }
            public Node? Right { get; set; }
            public bool IsLeaf => Left == null && Right == null;
        }

        /* Main entry for compression:
         *  read files as binary data
         *  build frequency table of byte occurrences
         *  construct Huffman tree
         *  generate Huffman code for each byte
         *  write metadata, tree structure and compressed data to output */
        public void Compress(string inputFilePath, string outputFilePath)
        { 
            byte[] fileData = File.ReadAllBytes(inputFilePath);
            var frequencyTable = BuildFrequencyTable(fileData);
            var root = BuildHuffmanTree(frequencyTable);
            var huffmanCodes = BuildHuffmanCodes(root);

            using (var output = new BinaryWriter(File.Open(outputFilePath, FileMode.Create)))
            {
                output.Write(IsBinaryFile(inputFilePath)); //flag
                output.Write(fileData.Length); // original size

                WriteHuffmanTree(output, root);
                WriteCompressedData(output, fileData, huffmanCodes);
            }
        }
        /* Main entry for decomrpession
         *  read metadata written by the compressor
         *      read file type flag
         *      read original file size
         *      read the HUffman tree
         *  decompress data and write to output*/
        public void Decompress(string inputFilePath, string outputFilePath)
        {
            using (var input = new BinaryReader(File.Open(inputFilePath, FileMode.Open)))
            {
                try
                {
                    bool isBinary = input.ReadBoolean();
                    Console.WriteLine($"File type is binary: {isBinary}");
                    int originalSize = input.ReadInt32();

                    Console.WriteLine($"Original file size: {originalSize} bytes");
                    var root = ReadHuffmanTree(input);

                    byte[] decompressedData = ReadCompressedData(input, root, originalSize);
                    Console.WriteLine($"Decompressed data length: {decompressedData.Length} bytes");

                    File.WriteAllBytes(outputFilePath, decompressedData);
                    Console.WriteLine($"Data written to {outputFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Decompression error: {ex.Message}\nStack trace: {ex.StackTrace}");
                    throw;
                }
            }
        }

        /* Count occurrences of each byte in the data*/
        private Dictionary<byte, int> BuildFrequencyTable(byte[] data)
        {
            var frequencyTable = new Dictionary<byte, int>();
            foreach (var b in data)
            {
                if (!frequencyTable.ContainsKey(b))
                {
                    frequencyTable[b] = 0;
                }
                frequencyTable[b]++;
            }
            return frequencyTable;
        }

        /* Construct HUffman tree from frequency data
         *  handle edge cases(empty input, single-value input)
         *  create priority queue(min-heap) of nodes, ordered by frequency
         *      take 2 lowest-frequency node from queue and create a parent node with sum of the 2 children nodes
         *      add parent back to queue
         *  stop when only one node remains(root) and return it */
        private Node BuildHuffmanTree(Dictionary<byte, int> frequencyTable)
        {
            if (frequencyTable.Count == 0)
            {
                throw new InvalidOperationException("Frequency table is empty");
            }

            if (frequencyTable.Count == 1)
            {
                byte onlyByte = frequencyTable.Keys.First();
                return new Node { Value = onlyByte, Frequency = frequencyTable[onlyByte] };
            }

            var priorityQueue = new PriorityQueue<Node, int>();

            foreach (var keyValue in frequencyTable)
            {
                priorityQueue.Enqueue(new Node { Value = keyValue.Key, Frequency = keyValue.Value }, keyValue.Value);
            }

            while (priorityQueue.Count > 1)
            {
                var left = priorityQueue.Dequeue();
                var right = priorityQueue.Dequeue();

                var parent = new Node
                {
                    Value = 0, // Non-leaf nodes don't need a value
                    Frequency = left.Frequency + right.Frequency,
                    Left = left,
                    Right = right
                };
                priorityQueue.Enqueue(parent, parent.Frequency);
            }

            return priorityQueue.Dequeue();
        }

        /* Traverse the Huffman tree to generate binary codes for each byte
         *  use a recursive function to build the code for each leaf node
         *  store the codes in a dictionary */
        private Dictionary<byte, string> BuildHuffmanCodes(Node root)
        {
            var huffmanCodes = new Dictionary<byte, string>();
            BuildHuffmanCodesRecursive(root, "", huffmanCodes);
            return huffmanCodes;
        }

        private void BuildHuffmanCodesRecursive(Node? node, string code, Dictionary<byte, string> huffmanCodes)
        {
            if (node == null)
            {
                return;
            }

            if (node.IsLeaf)
            {
                huffmanCodes[node.Value] = code.Length > 0 ? code : "0"; // Ensure at least one bit for single-node trees
            }

            BuildHuffmanCodesRecursive(node.Left, code + "0", huffmanCodes);
            BuildHuffmanCodesRecursive(node.Right, code + "1", huffmanCodes);
        }

        /* Write the Huffman tree to the output file for later decompression
         *  use pre-order traversal (recursive)
         *  if leaf node write "true" flag and byte value else write "false" and recursively write left/right sub tree*/
        private void WriteHuffmanTree(BinaryWriter output, Node? node)
        {
            if (node == null)
            {
                return;
            }

            if (node.IsLeaf)
            {
                output.Write(true);
                output.Write(node.Value);
            }
            else
            {
                output.Write(false);
                WriteHuffmanTree(output, node.Left);
                WriteHuffmanTree(output, node.Right);
            }
        }
        /* Deserialize and construct Huffman tree
         *  read a boolean flag
         *  if "true" -> create leaf node with byte value from input
         *  if "false" -> create internal node, recursively reads left and right children
         *  return the reconstructed node (eventually the full tree)*/
        private Node? ReadHuffmanTree(BinaryReader input)
        {
            if (input.ReadBoolean())
            {
                return new Node { Value = input.ReadByte() };
            }
            else
            {
                return new Node
                {
                    Left = ReadHuffmanTree(input),
                    Right = ReadHuffmanTree(input)
                };
            }
        }
        /* Encode and write compressed data
         *  maintain a current byte and bit position
         *      each byte in input get its Huffman code
         *      for each bit in code set appropiate bit in currentByte. when bitPosition hits 8 reset
         *  handle partial final byte
         *  write count of bits used in final byte for later decompression*/

        private void WriteCompressedData(BinaryWriter output, byte[] data, Dictionary<byte, string> huffmanCodes)
        {
            byte currentByte = 0;
            int bitPosition = 0;

            foreach (var b in data)
            {
                string code = huffmanCodes[b];
                foreach (char bit in code)
                {
                    if (bit == '1')
                    {
                        currentByte |= (byte)(1 << (7 - bitPosition));
                    }

                    bitPosition++;

                    if (bitPosition == 8)
                    {
                        output.Write(currentByte);
                        currentByte = 0;
                        bitPosition = 0;
                    }
                }
            }
            if (bitPosition > 0)
            {
                output.Write(currentByte);
            }
            output.Write((byte)(bitPosition == 0 ? 0 : bitPosition));
        }
        /* Decode compressed data back to original bytes
         *  handle special cases (empty files, single-byte files)
         *  read bit count for final bytes to determine how many bits were used in the last byte
         *  nested loop to process each bit in the compressed data (MSB to LSB) 
         *      if a bit is 0 navigate left
         *      if a bit is 1 navigate right
         *      when each a leaf node out put the corresponding value
         *      reset to root*/
        private byte[] ReadCompressedData(BinaryReader input, Node root, int originalSize)
        {
            if (originalSize == 0)
                return new byte[0]; //empty array

            if (root.IsLeaf)
            {
                byte[] decompressedBytes = new byte[originalSize];
                for (int i = 0; i < originalSize; i++)
                {
                    decompressedBytes[i] = root.Value;
                }
                return decompressedBytes;
            }

            long dataStartPosition = input.BaseStream.Position;
            input.BaseStream.Seek(-1, SeekOrigin.End);
            byte lastByteBitsUsed = input.ReadByte();

            long compressedDataLength = input.BaseStream.Length - dataStartPosition - 1;
            input.BaseStream.Position = dataStartPosition;

            byte[] compressedBytes = new byte[compressedDataLength];
            int bytesRead = input.Read(compressedBytes, 0, (int)compressedDataLength);
            if (bytesRead != compressedDataLength)
            {
                throw new InvalidDataException($"Expected to read {compressedDataLength} bytes but read {bytesRead}");
            }

            List<byte> result = new List<byte>(originalSize);
            Node? currentNode = root;

            for (int byteIndex = 0; byteIndex < compressedBytes.Length; byteIndex++)
            {
                // process valid bits in the last byte
                int bitsToProcess = (byteIndex == compressedBytes.Length - 1 && lastByteBitsUsed > 0) ? lastByteBitsUsed : 8;
                for (int bitIndex = 0; bitIndex < bitsToProcess; bitIndex++)
                {
                    if (result.Count >= originalSize)
                        break;
                    bool bit = (compressedBytes[byteIndex] & (1 << (7 - bitIndex))) != 0;
                    currentNode = bit ? currentNode?.Right : currentNode?.Left;
                    if (currentNode == null)
                    {
                        throw new InvalidDataException("Corrupt Huffman tree: encountered null node during traversal");
                    }
                    if (currentNode.IsLeaf)
                    {
                        result.Add(currentNode.Value);
                        currentNode = root;
                    }
                }
                if (result.Count >= originalSize)
                    break;
            }
            return result.ToArray();
        }
        private bool IsBinaryFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension == ".pdf" || extension == ".docx" || extension == ".xlsx" ||
                   extension == ".jpg" || extension == ".png" || extension == ".zip" || extension == ".wav"; 
        }
    }
}