using System;
using System.IO;

namespace HLSTools.NETFramework
{
    public class FileWriter
    {
        /// <summary>
        /// Writes the given bytes into a file.
        /// </summary>
        /// <param name="bytesToWrite">The bytes to write.</param>
        /// <param name="destinationFilename">The destination file name.</param>
        public static void WriteBytesToFile(byte[] bytesToWrite, string destinationFilename)
        {
            try
            {
                using (FileStream fileStream =
                    new FileStream(destinationFilename, FileMode.Create, FileAccess.Write))
                {
                    fileStream.Write(bytesToWrite, 0, bytesToWrite.Length);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to write to file: {e.Message}");
            }

            System.Diagnostics.Debug.WriteLine($"Wrote {bytesToWrite.Length} bytes to file {destinationFilename}");
        }
    }
}
