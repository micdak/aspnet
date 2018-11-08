using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PDFSharpMerge.Classes
{
    public class Helper
    {


        /// <summary>
        /// Returns a cryptgraphically secure GUID.  More computationally intensive, but can't be guessed.
        /// </summary>
        /// <returns></returns>
        public static Guid GetSecureGUID()
        {
            using (var provider = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                var bytes = new byte[16];
                provider.GetBytes(bytes);

                return new Guid(bytes);
            }
        }

        public static Tuple<string, string> GetPathAndName(string filePath)
        {
            var fileparts = filePath.Split('/');
            var filename = fileparts.Last();
            var path = string.Join("/", fileparts, 0, fileparts.Length - 1) + "/";

            return new Tuple<string, string>(path, filename);
        }
        
        public static byte[] GetJohnHancockBytes()
        {
            return Convert.FromBase64String(GetJohnHancockPNGB64());
        }

        public static string GetJohnHancockPNGB64()
        {
            return "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";
        }

        /// <summary>
        /// Increments the integer component of version strings 
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public static string IncrementVersion(string original)
        {
            if (original == "") original = "0.0";

            //regex to find the last digit in a string and put it in second match
            var pattern = new Regex(@"(.*?)(\d+)(\D*)\z");
            var match = pattern.Matches(original);

            return match[0].Groups[1].ToString() + (int.Parse(match[0].Groups[2].ToString()) + 1) + match[0].Groups[3].ToString();

        }

        /// <summary>
        /// Detects if a string contains chinese charcaters
        /// </summary>
        /// <param name="text">The unicode string to be checked</param>
        /// <returns></returns>
        public static bool IsChinese(string text)
        {
            return text.Any(c => (uint)c >= 0x4E00 && (uint)c <= 0x2FA1F);
        }

        /// <summary>
        /// Reads data from a stream until the end is reached. The
        /// data is returned as a byte array. An IOException is
        /// thrown if any of the underlying IO calls fail.
        /// </summary>
        /// <param name="stream">The stream to read data from</param>
        /// <param name="initialLength">The initial buffer length</param>
        public static byte[] ReadFully(System.IO.Stream stream, long initialLength)
        {
            // reset pointer just in case
            stream.Seek(0, System.IO.SeekOrigin.Begin);

            // If we've been passed an unhelpful initial length, just
            // use 32K.
            if (initialLength < 1)
            {
                initialLength = 32768;
            }

            byte[] buffer = new byte[initialLength];
            int read = 0;

            int chunk;
            while ((chunk = stream.Read(buffer, read, buffer.Length - read)) > 0)
            {
                read += chunk;

                // If we've reached the end of our buffer, check to see if there's
                // any more information
                if (read == buffer.Length)
                {
                    int nextByte = stream.ReadByte();

                    // End of stream? If so, we're done
                    if (nextByte == -1)
                    {
                        return buffer;
                    }

                    // Nope. Resize the buffer, put in the byte we've just
                    // read, and continue
                    byte[] newBuffer = new byte[buffer.Length * 2];
                    Array.Copy(buffer, newBuffer, buffer.Length);
                    newBuffer[read] = (byte)nextByte;
                    buffer = newBuffer;
                    read++;
                }
            }
            // Buffer is now too big. Shrink it.
            byte[] ret = new byte[read];
            Array.Copy(buffer, ret, read);
            return ret;
        }
    }

    public static class StringExtensions
    {
        /// <summary>
        /// takes a substring between two anchor strings (or the end of the string if that anchor is null)
        /// </summary>
        /// <param name="this">a string</param>
        /// <param name="from">an optional string to search after</param>
        /// <param name="until">an optional string to search before</param>
        /// <param name="comparison">an optional comparison for the search</param>
        /// <returns>a substring based on the search</returns>
        public static string Substring(this string @this, string from = null, string until = null, StringComparison comparison = StringComparison.InvariantCulture)
        {
            var fromLength = (from ?? string.Empty).Length;
            var startIndex = !string.IsNullOrEmpty(from)
                ? @this.IndexOf(from, comparison) + fromLength
                : 0;

            if (startIndex < fromLength) { throw new ArgumentException("from: Failed to find an instance of the first anchor"); }

            var endIndex = !string.IsNullOrEmpty(until)
            ? @this.IndexOf(until, startIndex, comparison)
            : @this.Length;

            if (endIndex < 0) { throw new ArgumentException("until: Failed to find an instance of the last anchor"); }

            var subString = @this.Substring(startIndex, endIndex - startIndex);
            return subString;
        }
    }
}

