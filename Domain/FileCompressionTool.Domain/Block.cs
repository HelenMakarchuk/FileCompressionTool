using System;
using System.IO;
using System.Linq;

namespace FileCompressionTool.Domain
{
    /// <summary>
    /// Блок данных
    /// </summary>
    public class Block : IDisposable
    {
        public const int MAX_SIZE = 1048576; // 1 MB
        public const short METADATA_LENGTH = 3;
        static readonly int _isCompressedMetadataValue = (int)Math.Pow(2, 7 * METADATA_LENGTH);

        bool isDisposed = false;

        public Block(int number, Stream stream, long endOffset, int size, bool isCompressed = false, bool isLast = false)
        {
            Number = number;
            Stream = stream;
            EndOffset = endOffset;
            Size = size;
            IsCompressed = isCompressed;
            IsLast = isLast;
        }

        ~Block()
        {
            Dispose(false);
        }

        public int Number { get; set; }
        public Stream Stream { get; set; }
        public long EndOffset { get; set; }
        public int Size { get; set; }
        public bool IsCompressed { get; set; }
        public bool IsLast { get; set; }

        public static Block Parse(int blockNumber, Stream stream, long endOffset)
        {
            stream.Seek(-endOffset, SeekOrigin.End);

            var metadata = new byte[METADATA_LENGTH + 1];
            stream.Read(metadata, 0, METADATA_LENGTH);

            var blockSize = BitConverter.ToInt32(metadata, 0);
            var isCompressedBlock = false;

            if (blockSize > MAX_SIZE)
            {
                isCompressedBlock = true;
                blockSize -= _isCompressedMetadataValue;
            }

            return new Block(blockNumber, stream, endOffset - METADATA_LENGTH, blockSize, isCompressedBlock);
        }

        /// <summary>
        /// Получение метаданных блока: размер блока а байтах, где последний бит: признак сжатия блока.
        /// </summary>
        public byte[] GetMetadata()
        {
            var metadata = IsCompressed ? Size + _isCompressedMetadataValue : Size;
            return BitConverter.GetBytes(metadata).Take(METADATA_LENGTH).ToArray();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposeManagedResources)
        {
            if (!isDisposed)
            {
                if (disposeManagedResources)
                {
                }

                Stream?.Dispose();

                isDisposed = true;
            }
        }
    }
}