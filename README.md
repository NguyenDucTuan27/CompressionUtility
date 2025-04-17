# CompressionUtility

This compression utility implements three classic compression algorithms: Huffman coding, Lempel-Ziv-Welch (LZW), and Arithmetic coding. The application provides a command-line interface for compressing and decompressing both individual files and entire directories.
Algorithms Implemented
1. Huffman Coding
   
A statistical compression technique that assigns variable-length codes to symbols based on their frequencies. More frequent symbols receive shorter codes, resulting in data reduction.
Features:

Binary Huffman tree encoding/decoding

Supports both text and binary files

Bit-level encoding for maximum compression

2. LZW (Lempel-Ziv-Welch)
   
A dictionary-based compression algorithm that builds a dictionary of sequences found in the data. It replaces repeating patterns with single codes.
Features:

Variable-width code implementation (9-12 bits)

Adaptive dictionary with reset mechanism

Optimized for text files but supports binary data

Automatic bypass for already-compressed file formats

3. Arithmetic Coding
   
A statistical method that encodes the entire message into a single number within an interval. It achieves compression by using fractional bits per symbol.
Features:

32-bit precision implementation\n

Handles underflow with E1/E2/E3 rescaling

Efficient bit-level I/O

Statistical modeling with cumulative frequency ranges

# How to Use

*Input file path example: C:\Users\Admin\Pictures\jake.png

*Output file example: C:\Users\Admin\Pictures\jake.hu

*Remove any quotation marks. Provide full path directly to the file/folder. Change file name or extension to .hu / .lzw / .ar is recommended

*When decompressing a file, must choose the same algorithm as compressing

*Test cases: https://drive.google.com/drive/folders/1pARgdz7JRKguobXHpDxYsLR1zyQ5Qrqo?usp=drive_link 



1. Compressing a File

Select option 1 from the main menu

Choose your compression algorithm (1=Huffman, 2=LZW, 3=Arithmetic)

Enter the input file path 

Enter the output file path

2. Decompressing a File

Select option 2 from the main menu

Choose the same algorithm used for compression

Enter the compressed file path

Enter the output file path

3. Compressing a Directory

Select option 3 from the main menu

Enter the directory path

Enter the output ZIP file path

Choose a compression strategy (single algorithm or best-fit)

4. Extracting an Archive

Select option 4 from the main menu

Enter the archive path

Enter the destination directory

# Technical Details

1. Huffman Implementation

Uses binary tree structure for Huffman codes

Serializes tree structure in compressed output

Bit-packing for efficient code storage

Handles edge cases like single-character files

2. LZW Implementation

Uses variable bit-width codes (9-12 bits)

Implements dictionary growth and reset mechanisms

Detects and bypasses already-compressed formats

Uses custom byte sequence comparison for dictionary lookups

3. Arithmetic Implementation

32-bit precision for high accuracy

Implements range narrowing and scaling

Handles underflow with pending bits mechanism

Includes bit-level I/O for output precision

# Limitations

Large files (>1GB) may cause memory issues as data is loaded entirely

Compression efficiency varies by file type and content patterns

Pre-compressed files (JPEG, MP3, etc.) typically show little to no additional compression

Notes:
This compression utility was developed as part of a bachelor thesis project to demonstrate the implementation of classical compression algorithms. It balances theoretical correctness with practical usability.
