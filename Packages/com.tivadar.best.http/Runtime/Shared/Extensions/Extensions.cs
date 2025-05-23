using Best.HTTP.Shared.PlatformSupport.Memory;
using Best.HTTP.Shared.PlatformSupport.Text;
using Best.HTTP.Shared.Streams;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using static Best.HTTP.Hosts.Connections.HTTP1.Constants;

using Cryptography = System.Security.Cryptography;

namespace Best.HTTP.Shared.Extensions
{
    public static class Extensions
    {
        #region ASCII Encoding (These are required because Windows Phone doesn't supports the Encoding.ASCII class.)

        /// <summary>
        /// On WP8 platform there are no ASCII encoding.
        /// </summary>
        public static string AsciiToString(this byte[] bytes)
        {
            StringBuilder sb = StringBuilderPool.Get(bytes.Length); //new StringBuilder(bytes.Length);
            foreach (byte b in bytes)
                sb.Append(b <= 0x7f ? (char)b : '?');
            return StringBuilderPool.ReleaseAndGrab(sb);
        }

        /// <summary>
        /// On WP8 platform there are no ASCII encoding.
        /// </summary>
        public static BufferSegment GetASCIIBytes(this string str)
        {
            byte[] result = BufferPool.Get(str.Length, true);
            for (int i = 0; i < str.Length; ++i)
            {
                char ch = str[i];
                result[i] = (byte)((ch < (char)0x80) ? ch : '?');
            }

            return new BufferSegment(result, 0, str.Length);
        }

        public static void SendAsASCII(this BinaryWriter stream, string str)
        {
            for (int i = 0; i < str.Length; ++i)
            {
                char ch = str[i];

                stream.Write((byte)((ch < (char)0x80) ? ch : '?'));
            }
        }

        #endregion

        #region Headers

        public static Dictionary<string, List<string>> AddHeader(this Dictionary<string, List<string>> headers, string name, string value)
        {
            if (headers == null)
                headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            List<string> values;
            if (!headers.TryGetValue(name, out values))
                headers.Add(name, values = new List<string>(1));

            values.Add(value);

            return headers;
        }

        public static Dictionary<string, List<string>> SetHeader(this Dictionary<string, List<string>> headers, string name, string value)
        {
            if (headers == null)
                headers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            List<string> values;
            if (!headers.TryGetValue(name, out values))
                headers.Add(name, values = new List<string>(1));

            values.Clear();
            values.Add(value);

            return headers;
        }

        public static bool RemoveHeader(this Dictionary<string, List<string>> headers, string name) => headers != null && headers.Remove(name);

        public static void RemoveHeaders(this Dictionary<string, List<string>> headers) => headers?.Clear();

        public static List<string> GetHeaderValues(this Dictionary<string, List<string>> headers, string name)
        {
            if (headers == null)
                return null;

            List<string> values;
            if (!headers.TryGetValue(name, out values) || values.Count == 0)
                return null;

            return values;
        }

        public static string GetFirstHeaderValue(this Dictionary<string, List<string>> headers, string name)
        {
            if (headers == null)
                return null;

            List<string> values;
            if (!headers.TryGetValue(name, out values) || values.Count == 0)
                return null;

            return values[0];
        }

        public static bool HasHeaderWithValue(this Dictionary<string, List<string>> headers, string headerName, string value)
        {
            var values = headers.GetHeaderValues(headerName);
            if (values == null)
                return false;

            for (int i = 0; i < values.Count; ++i)
                if (string.Compare(values[i], value, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        public static bool HasHeader(this Dictionary<string, List<string>> headers, string headerName)
            => headers != null && headers.ContainsKey(headerName);

        #endregion

        #region FileSystem WriteLine function support

        public static void WriteString(this Stream fs, string value)
        {
            int count = System.Text.Encoding.UTF8.GetByteCount(value);
            var buffer = BufferPool.Get(count, true);
            try
            {
                System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
                fs.Write(buffer, 0, count);
            }
            finally
            {
                BufferPool.Release(buffer);
            }
        }

        public static void WriteLine(this Stream fs)
        {
            fs.Write(EOL, 0, 2);
        }

        public static void WriteLine(this Stream fs, string line)
        {
            var buff = line.GetASCIIBytes();
            try
            {
                fs.Write(buff.Data, buff.Offset, buff.Count);
                fs.WriteLine();
            }
            finally
            {
                BufferPool.Release(buff);
            }
        }

        public static void WriteLine(this Stream fs, string format, params object[] values)
        {
            var buff = string.Format(format, values).GetASCIIBytes();
            try
            {
                fs.Write(buff.Data, buff.Offset, buff.Count);
                fs.WriteLine();
            }
            finally
            {
                BufferPool.Release(buff);
            }
        }

        #endregion

        #region Other Extensions

        public static AutoReleaseBuffer AsAutoRelease(this byte[] buffer) => new AutoReleaseBuffer(buffer);

        public static BufferSegment AsBuffer(this byte[] bytes)
        {
            return new BufferSegment(bytes, 0, bytes.Length);
        }

        public static BufferSegment AsBuffer(this byte[] bytes, int length)
        {
            return new BufferSegment(bytes, 0, length);
        }

        public static BufferSegment AsBuffer(this byte[] bytes, int offset, int length)
        {
            return new BufferSegment(bytes, offset, length);
        }

        public static BufferSegment CopyAsBuffer(this byte[] bytes, int offset, int length)
        {
            var newBuff = BufferPool.Get(length, true);

            Array.Copy(bytes, offset, newBuff, 0, length);

            return newBuff.AsBuffer(0, length);
        }

        public static string GetRequestPathAndQueryURL(this Uri uri)
        {
            string requestPathAndQuery = uri.GetComponents(UriComponents.PathAndQuery, UriFormat.UriEscaped);

            // http://forum.unity3d.com/threads/best-http-released.200006/page-26#post-2723250
            if (string.IsNullOrEmpty(requestPathAndQuery))
                requestPathAndQuery = "/";

            return requestPathAndQuery;
        }

        public static string[] FindOption(this string str, string option)
        {
            //s-maxage=2678400, must-revalidate, max-age=0
            string[] options = str.ToLowerInvariant().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            option = option.ToLowerInvariant();

            for (int i = 0; i < options.Length; ++i)
                if (options[i].Contains(option))
                    return options[i].Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

            return null;
        }

        public static string[] FindOption(this string[] options, string option)
        {
            for (int i = 0; i < options.Length; ++i)
                if (options[i].Contains(option))
                    return options[i].Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

            return null;
        }

        public static void WriteArray(this Stream stream, byte[] array)
        {
            stream.Write(array, 0, array.Length);
        }

        public static void WriteBufferSegment(this Stream stream, BufferSegment buffer)
        {
            stream.Write(buffer.Data, buffer.Offset, buffer.Count);
        }

        /// <summary>
        /// Returns true if the Uri's host is a valid IPv4 or IPv6 address.
        /// </summary>
        public static bool IsHostIsAnIPAddress(this Uri uri)
        {
            if (uri == null)
                return false;

            return IsIpV4AddressValid(uri.Host) || IsIpV6AddressValid(uri.Host);
        }

        // Original idea from: https://www.code4copy.com/csharp/c-validate-ip-address-string/
        // Working regex: https://www.regular-expressions.info/ip.html
        private static readonly System.Text.RegularExpressions.Regex validIpV4AddressRegex = new System.Text.RegularExpressions.Regex("\\b(?:\\d{1,3}\\.){3}\\d{1,3}\\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// Validates an IPv4 address.
        /// </summary>
        public static bool IsIpV4AddressValid(string address)
        {
            if (!string.IsNullOrEmpty(address))
                return validIpV4AddressRegex.IsMatch(address.Trim());

            return false;
        }

        /// <summary>
        /// Validates an IPv6 address.
        /// </summary>
        public static bool IsIpV6AddressValid(string address)
        {
            if (!string.IsNullOrEmpty(address))
            {
                System.Net.IPAddress ip;
                if (System.Net.IPAddress.TryParse(address, out ip))
                    return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
            }

            return false;
        }

        #endregion

        #region String Conversions

        public static int ToInt32(this string str, int defaultValue = default(int)) => int.TryParse(str, out var value) ? value : defaultValue;
        public static uint ToUInt32(this string str, uint defaultValue = default) => uint.TryParse(str, out var value) ? value : defaultValue;

        public static long ToInt64(this string str, long defaultValue = default(long)) => long.TryParse(str, out var value) ? value : defaultValue;

        public static ulong ToUInt64(this string str, ulong defaultValue = default(ulong)) => ulong.TryParse(str, out var value) ? value : defaultValue;

        public static DateTime ToDateTime(this string str, DateTime defaultValue = default(DateTime))
        {
            if (str == null)
                return defaultValue;

            if (DateTime.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var value))
                return value;

            return defaultValue;
        }

        public static string ToStrOrEmpty(this string str)
        {
            if (str == null)
                return String.Empty;

            return str;
        }

        public static string ToStr(this string str, string defaultVale)
        {
            if (str == null)
                return defaultVale;

            return str;
        }

        public static string ToBinaryStr(this byte value)
        {
            return Convert.ToString(value, 2).PadLeft(8, '0');
        }

        #endregion

        #region MD5 Hashing

        public static string CalculateMD5Hash(this string input)
        {
            var asciiBuff = input.GetASCIIBytes();
            var hash = asciiBuff.CalculateMD5Hash();
            BufferPool.Release(asciiBuff);
            return hash;
        }

        public static string CalculateMD5Hash(this BufferSegment input)
        {
            using (var md5 = Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(input.Data, input.Offset, input.Count);
                var sb = StringBuilderPool.Get(hash.Length); //new StringBuilder(hash.Length);
                for (int i = 0; i < hash.Length; ++i)
                    sb.Append(hash[i].ToString("x2"));
                BufferPool.Release(hash);
                return StringBuilderPool.ReleaseAndGrab(sb);
            }
        }

        #endregion

        #region Efficient String Parsing Helpers

        internal static string Read(this string str, ref int pos, char block, bool needResult = true)
        {
            return str.Read(ref pos, (ch) => ch != block, needResult);
        }

        internal static string Read(this string str, ref int pos, Func<char, bool> block, bool needResult = true)
        {
            if (pos >= str.Length)
                return string.Empty;

            str.SkipWhiteSpace(ref pos);

            int startPos = pos;

            while (pos < str.Length && block(str[pos]))
                pos++;

            string result = needResult ? str.Substring(startPos, pos - startPos) : null;

            // set position to the next char
            pos++;

            return result;
        }

        internal static string ReadPossibleQuotedText(this string str, ref int pos)
        {
            string result = string.Empty;
            if (str == null)
                return result;

            // It's a quoted text?
            if (str[pos] == '\"')
            {
                // Skip the starting quote
                str.Read(ref pos, '\"', false);

                // Read the text until the ending quote
                result = str.Read(ref pos, '\"');

                // Next option
                str.Read(ref pos, (ch) => ch != ',' && ch != ';', false);
            }
            else
                // It's not a quoted text, so we will read until the next option
                result = str.Read(ref pos, (ch) => ch != ',' && ch != ';');

            return result;
        }

        internal static void SkipWhiteSpace(this string str, ref int pos)
        {
            if (pos >= str.Length)
                return;

            while (pos < str.Length && char.IsWhiteSpace(str[pos]))
                pos++;
        }

        internal static string TrimAndLower(this string str)
        {
            if (str == null)
                return null;

            char[] buffer = new char[str.Length];
            int length = 0;

            for (int i = 0; i < str.Length; ++i)
            {
                char ch = str[i];
                if (!char.IsWhiteSpace(ch) && !char.IsControl(ch))
                    buffer[length++] = char.ToLowerInvariant(ch);
            }

            return new string(buffer, 0, length);
        }

        internal static char? Peek(this string str, int pos)
        {
            if (pos < 0 || pos >= str.Length)
                return null;

            return str[pos];
        }

        #endregion

        #region Specialized String Parsers

        //public, max-age=2592000
        internal static List<HeaderValue> ParseOptionalHeader(this string str)
        {
            List<HeaderValue> result = new List<HeaderValue>();

            if (str == null)
                return result;

            int idx = 0;

            // process the rest of the text
            while (idx < str.Length)
            {
                // Read key
                string key = str.Read(ref idx, (ch) => ch != '=' && ch != ',').TrimAndLower();
                HeaderValue qp = new HeaderValue(key);

                if (str[idx - 1] == '=')
                    qp.Value = str.ReadPossibleQuotedText(ref idx);

                result.Add(qp);
            }

            return result;
        }

        //deflate, gzip, x-gzip, identity, *;q=0
        internal static List<HeaderValue> ParseQualityParams(this string str)
        {
            List<HeaderValue> result = new List<HeaderValue>();

            if (str == null)
                return result;

            int idx = 0;
            while (idx < str.Length)
            {
                string key = str.Read(ref idx, (ch) => ch != ',' && ch != ';').TrimAndLower();

                HeaderValue qp = new HeaderValue(key);

                if (str[idx - 1] == ';')
                {
                    str.Read(ref idx, '=', false);
                    qp.Value = str.Read(ref idx, ',');
                }

                result.Add(qp);
            }

            return result;
        }

        #endregion

        #region Buffer Filling

        /// <summary>
        /// Will fill the entire buffer from the stream. Will throw an exception when the underlying stream is closed.
        /// </summary>
        public static void ReadBuffer(this Stream stream, byte[] buffer)
        {
            int count = 0;

            do
            {
                int read = stream.Read(buffer, count, buffer.Length - count);

                if (read <= 0)
                    throw ExceptionHelper.ServerClosedTCPStream();

                count += read;
            } while (count < buffer.Length);
        }

        public static void ReadBuffer(this Stream stream, byte[] buffer, int length)
        {
            int count = 0;

            do
            {
                int read = stream.Read(buffer, count, length - count);

                if (read <= 0)
                    throw ExceptionHelper.ServerClosedTCPStream();

                count += read;
            } while (count < length);
        }

        #endregion

        #region BufferPoolMemoryStream

        public static void WriteString(this BufferPoolMemoryStream ms, string str)
        {
            var byteCount = Encoding.UTF8.GetByteCount(str);
            byte[] buffer = BufferPool.Get(byteCount, true);
            Encoding.UTF8.GetBytes(str, 0, str.Length, buffer, 0);
            ms.Write(buffer, 0, byteCount);
            BufferPool.Release(buffer);
        }

        public static void WriteLine(this BufferPoolMemoryStream ms)
        {
            ms.Write(EOL, 0, EOL.Length);
        }

        public static void WriteLine(this BufferPoolMemoryStream ms, string str)
        {
            ms.WriteString(str);
            ms.Write(EOL, 0, EOL.Length);
        }

        #endregion

#if NET_STANDARD_2_0 || NET_4_6
        public static void Clear<T>(this System.Collections.Concurrent.ConcurrentQueue<T> queue)
        {
            T result;
            while (queue.TryDequeue(out result))
                ;
        }
#endif
    }

    public static class ExceptionHelper
    {
        public static Exception ServerClosedTCPStream()
        {
            return new Exception("TCP Stream closed unexpectedly by the remote server");
        }
    }
}
