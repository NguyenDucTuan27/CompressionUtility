using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HuffmanCoding;
using LZW;
using ArithmeticCoding;
using Interface;

namespace CompressionUtility
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                try
                {
                    Console.Clear();
                    Console.WriteLine("===== Compression Utility =====");
                    Console.WriteLine("Choose an operation:");
                    Console.WriteLine("1. Compress File");
                    Console.WriteLine("2. Decompress File");
                    Console.WriteLine("3. Compress Directory");
                    Console.WriteLine("4. Extract Archive");
                    Console.WriteLine("0. Exit");
                    Console.Write("Enter your choice: ");
                    var operation = Console.ReadLine();

                    if (operation == "0")
                        break;

                    switch (operation)
                    {
                        case "1":
                            CompressFile();
                            break;
                        case "2":
                            DecompressFile();
                            break;
                        case "3":
                            CompressDirectory();
                            break;
                        case "4":
                            ExtractArchive();
                            break;
                        default:
                            Console.WriteLine("Invalid operation. Press any key to continue.");
                            Console.ReadKey();
                            continue;
                    }

                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
        }

        static void CompressFile()
        {
            Console.WriteLine("\nChoose a compression algorithm:");
            Console.WriteLine("1. Huffman Coding");
            Console.WriteLine("2. LZW");
            Console.WriteLine("3. Arithmetic Coding");
            Console.Write("Enter your choice: ");
            var choice = Console.ReadLine();

            ICompressionAlgorithm algorithm = choice switch
            {
                "1" => new HuffmanCompression(),
                "2" => new LZWcompression(),
                "3" => new ArithmeticCompression(),
                _ => throw new InvalidOperationException("Invalid algorithm choice")
            };

            Console.Write("Enter the input file path: ");
            var inputFilePath = Console.ReadLine();

            if (!File.Exists(inputFilePath))
            {
                Console.WriteLine("Input file does not exist.");
                return;
            }

            Console.Write("Enter the output file path: ");
            var outputFilePath = Console.ReadLine();

            try
            {
                Path.GetFullPath(outputFilePath);
            }
            catch
            {
                Console.WriteLine("Invalid output file path.");
                return;
            }

            algorithm.Compress(inputFilePath, outputFilePath);
            Console.WriteLine("Compression completed successfully.");
        }

        static void DecompressFile()
        {
            Console.WriteLine("\nChoose a compression algorithm:");
            Console.WriteLine("1. Huffman Coding");
            Console.WriteLine("2. LZW");
            Console.WriteLine("3. Arithmetic Coding");
            Console.Write("Enter your choice: ");
            var choice = Console.ReadLine();

            ICompressionAlgorithm algorithm = choice switch
            {
                "1" => new HuffmanCompression(),
                "2" => new LZWcompression(),
                "3" => new ArithmeticCompression(),
                _ => throw new InvalidOperationException("Invalid algorithm choice")
            };

            Console.Write("Enter the input file path: ");
            var inputFilePath = Console.ReadLine();

            if (!File.Exists(inputFilePath))
            {
                Console.WriteLine("Input file does not exist.");
                return;
            }

            Console.Write("Enter the output file path: ");
            var outputFilePath = Console.ReadLine();

            try
            {
                Path.GetFullPath(outputFilePath);
            }
            catch
            {
                Console.WriteLine("Invalid output file path.");
                return;
            }

            algorithm.Decompress(inputFilePath, outputFilePath);
            Console.WriteLine("Decompression completed successfully.");
        }

        static void CompressDirectory()
        {
            Console.Write("Enter the directory path to compress: ");
            string directoryPath = Console.ReadLine();

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine("Directory does not exist.");
                return;
            }

            Console.Write("Enter the output zip file path (including .zip extension): ");
            string zipFilePath = Console.ReadLine();

            if (!zipFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                zipFilePath += ".zip";
            }

            Console.WriteLine("\nChoose a compression algorithm:");
            Console.WriteLine("1. Huffman Coding");
            Console.WriteLine("2. LZW");
            Console.WriteLine("3. Arithmetic Coding");
            Console.Write("Enter your choice: ");
            var choice = Console.ReadLine();

            ICompressionAlgorithm algorithm = choice switch
            {
                "1" => new HuffmanCompression(),
                "2" => new LZWcompression(),
                "3" => new ArithmeticCompression(),
                _ => new LZWcompression() // Default to LZW
            };

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDir);

                try
                {
                    Console.WriteLine("Compressing files...");
                    int fileCount = 0;

                    string[] allFiles = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);

                    if (allFiles.Length == 0)
                    {
                        Console.WriteLine("The directory is empty. Nothing to compress.");
                        return;
                    }

                    List<string> manifest = new List<string>();

                    foreach (string filePath in allFiles)
                    {
                        string relativePath = filePath.Substring(directoryPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        string tempFilePath = Path.Combine(tempDir, relativePath);

                        string tempDirPath = Path.GetDirectoryName(tempFilePath);
                        if (!string.IsNullOrEmpty(tempDirPath) && !Directory.Exists(tempDirPath))
                        {
                            Directory.CreateDirectory(tempDirPath);
                        }

                        try
                        {
                            // Compress the file
                            algorithm.Compress(filePath, tempFilePath);
                            fileCount++;
                            int algoId = algorithm is HuffmanCompression ? 1 :
                                         algorithm is LZWcompression ? 2 : 3;
                            manifest.Add($"{relativePath}|{algoId}");

                            Console.WriteLine($"Compressed: {relativePath}");
                        }
                        catch
                        {
                            File.Copy(filePath, tempFilePath, true);
                            manifest.Add($"{relativePath}|0"); // 0 means no compression
                            Console.WriteLine($"Copied file (uncompressed): {relativePath}");
                        }
                    }
                    File.WriteAllLines(Path.Combine(tempDir, "_manifest.txt"), manifest);

                    if (File.Exists(zipFilePath))
                        File.Delete(zipFilePath);

                    ZipFile.CreateFromDirectory(tempDir, zipFilePath);

                    Console.WriteLine($"\nDirectory compressed successfully.");
                    Console.WriteLine($"Total files processed: {fileCount}");
                    Console.WriteLine($"ZIP archive created at: {zipFilePath}");
                }
                finally
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        Console.WriteLine("Warning: Could not delete temporary files.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error compressing directory: {ex.Message}");
            }
        }

        static void ExtractArchive()
        {
            Console.Write("Enter the zip file path to extract: ");
            string zipFilePath = Console.ReadLine();

            if (!File.Exists(zipFilePath))
            {
                Console.WriteLine("Zip file does not exist.");
                return;
            }

            Console.Write("Enter the destination directory path: ");
            string extractPath = Console.ReadLine();

            try
            {
                if (!Directory.Exists(extractPath))
                {
                    Directory.CreateDirectory(extractPath);
                }
                string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempDir);

                try
                {
                    ZipFile.ExtractToDirectory(zipFilePath, tempDir);

                    string manifestPath = Path.Combine(tempDir, "_manifest.txt");
                    if (!File.Exists(manifestPath))
                    {
                        Console.WriteLine("This appears to be a standard ZIP file. Extracting directly.");
                        CopyDirectory(tempDir, extractPath);
                        return;
                    }

                    string[] manifestEntries = File.ReadAllLines(manifestPath);

                    ICompressionAlgorithm huffman = new HuffmanCompression();
                    ICompressionAlgorithm lzw = new LZWcompression();
                    ICompressionAlgorithm arithmetic = new ArithmeticCompression();

                    Console.WriteLine("Extracting files...");

                    foreach (string entry in manifestEntries)
                    {
                        string[] parts = entry.Split('|');
                        if (parts.Length != 2)
                            continue;

                        string relativePath = parts[0];
                        int algorithmId = int.Parse(parts[1]);

                        string tempFilePath = Path.Combine(tempDir, relativePath);
                        string outputFilePath = Path.Combine(extractPath, relativePath);

                        string outputDir = Path.GetDirectoryName(outputFilePath);
                        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                        {
                            Directory.CreateDirectory(outputDir);
                        }

                        if (algorithmId == 0)
                        {
                            File.Copy(tempFilePath, outputFilePath, true);
                        }
                        else
                        {
                            ICompressionAlgorithm algorithm = algorithmId switch
                            {
                                1 => huffman,
                                2 => lzw,
                                3 => arithmetic,
                                _ => throw new InvalidDataException($"Unknown algorithm ID: {algorithmId}")
                            };

                            algorithm.Decompress(tempFilePath, outputFilePath);
                        }
                    }

                    Console.WriteLine($"Archive extracted successfully to {extractPath}");
                }
                finally
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        Console.WriteLine("Warning: Could not delete temporary files.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting archive: {ex.Message}");
            }
        }

        static void CopyDirectory(string sourceDir, string destinationDir)
        {
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            // Copy all files
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                if (Path.GetFileName(filePath) == "_manifest.txt")
                    continue; 

                string fileName = Path.GetFileName(filePath);
                string destFile = Path.Combine(destinationDir, fileName);
                File.Copy(filePath, destFile, true);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDir);
                string destSubDir = Path.Combine(destinationDir, subDirName);
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}