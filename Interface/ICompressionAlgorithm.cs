namespace Interface
{
    public interface ICompressionAlgorithm
    {
        void Compress(string inputFilePath, string outputFilePath);
        void Decompress(string inputFilePath, string outputFilePath);
    }
}