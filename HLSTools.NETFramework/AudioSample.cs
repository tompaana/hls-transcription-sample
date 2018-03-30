using NAudio.Wave;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace HLSTools.NETFramework
{
    /// <summary>
    /// Wraps the wave stream and provides few utility methods for processing.
    /// </summary>
    public class AudioSample : IDisposable
    {
        public WaveStream BlockAlignReductionStream
        {
            get;
            private set;
        }

        public string WaveFormatEncoding
        {
            get
            {
                string encoding = BlockAlignReductionStream.WaveFormat?.Encoding.ToString();

                if (string.IsNullOrWhiteSpace(encoding))
                {
                    encoding = "Unknown";
                }

                return encoding;
            }
        }

        public TimeSpan Duration
        {
            get
            {
                return BlockAlignReductionStream.TotalTime;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="blockAlignReductionStream">The wave stream.</param>
        public AudioSample(WaveStream blockAlignReductionStream)
        {
            BlockAlignReductionStream = blockAlignReductionStream
                ?? throw new ArgumentNullException("Wave stream cannot be null");
        }

        public void Dispose()
        {
            if (BlockAlignReductionStream != null)
            {
                BlockAlignReductionStream.Dispose();
            }
        }

        /// <returns>The audio sample content as wave format byte array.</returns>
        public byte[] GetBytes()
        {
            MemoryStream outStream =
                new MemoryStream(BlockAlignReductionStream.WaveFormat.AverageBytesPerSecond * 4);
            BlockAlignReductionStream.Seek(0, SeekOrigin.Begin);
            WaveFileWriter.WriteWavFileToStream(outStream, BlockAlignReductionStream);
            BlockAlignReductionStream.Seek(0, SeekOrigin.Begin);
            return outStream.ToArray();
        }

        /// <summary>
        /// Writes this sample into a wave file with the specified file name.
        /// </summary>
        /// <param name="filename">The output file name.</param>
        public void WriteToWaveFile(string filename)
        {
            BlockAlignReductionStream.Seek(0, SeekOrigin.Begin);
            WaveFileWriter.CreateWaveFile(filename, BlockAlignReductionStream);
            BlockAlignReductionStream.Seek(0, SeekOrigin.Begin);
        }

        /// <summary>
        /// Constructs a wave file header based on the details of this sample.
        /// 
        /// The canonical wave file format
        /// See http://soundfile.sapp.org/doc/WaveFormat/
        /// 
        /// Endianness | File offset (bytes) | Field name         | Field size (bytes)
        /// -----------|---------------------|--------------------|-------------------
        ///        big |          0          | Chunk ID           | 4
        ///     little |          4          | Chunk size         | 4
        ///        big |          8          | Format             | 4
        ///        big |         12          | Subchunk 1 ID      | 4
        ///     little |         16          | Subchunk 1 size    | 4
        ///     little |         20          | Audio format       | 2
        ///     little |         22          | Number of channels | 2
        ///     little |         24          | Sample rate        | 4
        ///     little |         28          | Byte rate          | 4
        ///     little |         32          | Block align        | 2
        ///     little |         34          | Bits per sample    | 2
        ///        big |         36          | Subchunk 2 ID      | 4
        ///     little |         40          | Subchunk 2 size    | 4
        ///     little |         44          | Data               | Subchunk 2 size
        /// 
        /// </summary>
        /// <returns>The constructed header as byte array.</returns>
        public byte[] ConstructHeader()
        {
            if (!WaveFormatEncoding.ToLower().Equals("pcm"))
            {
                throw new NotImplementedException("Only PCM format supported for header creation");
            }

            byte[] headerBytes = new byte[44];
            bool isLittleEndian = BitConverter.IsLittleEndian;

            // Chunk ID: RIFF in ASCII, big endian
            AddBytes(Encoding.ASCII.GetBytes("RIFF"), headerBytes, 0);

            // Chunk size, little endian
            int subchunk1Size = 16; // For PCM

            if (BlockAlignReductionStream.Length > int.MaxValue)
            {
                throw new IndexOutOfRangeException("Stream length is greater than what can be described with 32-bit integer");
            }

            int subchunk2Size = (int)BlockAlignReductionStream.Length;
            int chunkSize = 4 + (8 + subchunk1Size) + (8 + subchunk2Size);
            chunkSize = IPAddress.NetworkToHostOrder(chunkSize);
            AddBytes(BitConverter.GetBytes(chunkSize), headerBytes, 4);

            // Format, big endian
            // Contains the letters "WAVE" (0x57415645 big-endian form)
            int format = IPAddress.HostToNetworkOrder(0x57415645);
            AddBytes(BitConverter.GetBytes(format), headerBytes, 8);

            // Subchunk 1 ID, big endian
            // Contains the letters "fmt " (0x666d7420 big-endian form).
            int subchunk1Id = IPAddress.HostToNetworkOrder(0x666d7420);
            AddBytes(BitConverter.GetBytes(subchunk1Id), headerBytes, 12);

            // Subchunk 1 size, 16 for PCM, little endian
            subchunk1Size = IPAddress.NetworkToHostOrder((int)16);
            AddBytes(BitConverter.GetBytes(subchunk1Size), headerBytes, 16);

            // Audio format, PCM = 1, little endian
            short audioFormat = IPAddress.NetworkToHostOrder((short)1);
            AddBytes(BitConverter.GetBytes(audioFormat), headerBytes, 20);

            // Number of channels, little endian
            short numberOfChannels = IPAddress.NetworkToHostOrder((short)BlockAlignReductionStream.WaveFormat.Channels);
            AddBytes(BitConverter.GetBytes(numberOfChannels), headerBytes, 22);

            // Sample rate, little endian
            int sampleRate = IPAddress.NetworkToHostOrder(BlockAlignReductionStream.WaveFormat.SampleRate);
            AddBytes(BitConverter.GetBytes(sampleRate), headerBytes, 24);

            // Byte rate, little endian
            int byteRate = IPAddress.NetworkToHostOrder(BlockAlignReductionStream.WaveFormat.AverageBytesPerSecond);
            AddBytes(BitConverter.GetBytes(byteRate), headerBytes, 28);

            // Block align, little endian
            short blockAlign = IPAddress.NetworkToHostOrder((short)BlockAlignReductionStream.WaveFormat.BlockAlign);
            AddBytes(BitConverter.GetBytes(blockAlign), headerBytes, 32);

            // Bits per sample, little endian
            short bitsPerSample = IPAddress.NetworkToHostOrder((short)BlockAlignReductionStream.WaveFormat.BitsPerSample);
            AddBytes(BitConverter.GetBytes(bitsPerSample), headerBytes, 34);

            // Subchunk 2 ID, big endian
            // Contains the letters "data" (0x64617461 big-endian form)
            int subchunk2Id = IPAddress.HostToNetworkOrder(0x64617461);
            AddBytes(BitConverter.GetBytes(subchunk2Id), headerBytes, 36);

            // Subchunk 2 size, little endian
            subchunk2Size = IPAddress.NetworkToHostOrder(subchunk2Size);
            AddBytes(BitConverter.GetBytes(subchunk2Size), headerBytes, 40);

            //  As an example, here are the opening 72 bytes of a WAVE file with bytes shown as hexadecimal numbers:
            //
            // 52 49 46 46 24 08 00 00 57 41 56 45 66 6d 74 20 10 00 00 00 01 00 02 00 
            // 22 56 00 00 88 58 01 00 04 00 10 00 64 61 74 61 00 08 00 00

            System.Diagnostics.Debug.WriteLine($"Constructed header: {BitConverter.ToString(headerBytes)}");

            return headerBytes;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append($"Audio sample:\n\t* {BlockAlignReductionStream.Length} bytes\n\t* Duration: {Duration}\n\t* Format: {WaveFormatEncoding}");

            WaveFormat waveFormat = BlockAlignReductionStream.WaveFormat;

            if (waveFormat != null)
            {
                stringBuilder.Append($"\n\t* Average bytes per second: {waveFormat.AverageBytesPerSecond}\n\t* Sample rate: {waveFormat.SampleRate}\n\t* Channel count: {waveFormat.Channels}\n\t* Bits per sample: {waveFormat.BitsPerSample}");
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Adds the given bytes into the given destination.
        /// </summary>
        /// <param name="source">The source bytes.</param>
        /// <param name="destination">The byte array to add the source into.</param>
        /// <param name="offset">The offset in respect to the destination.</param>
        private void AddBytes(byte[] source, byte[] destination, int offset)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i + offset] = source[i];
            }
        }
    }
}
