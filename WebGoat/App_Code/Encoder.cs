using System;
using System.Collections.Generic;
using System.Web;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Data;
using System.Web.Security;

namespace OWASP.WebGoat.NET.App_Code
{
    public class Encoder            
    {
        private static byte[] _salt = Encoding.ASCII.GetBytes("o6806642kbM7c5");
        private const int PBKDF2_ITERATIONS = 600000;

        /// <summary>
        /// Derives cryptographic key using PBKDF2 with SHA-1 and 600,000 iterations
        /// </summary>
        /// <param name="sharedSecret">Password for key derivation</param>
        /// <returns>Derived key bytes</returns>
        private static byte[] DeriveKeyPbkdf2(string sharedSecret)
        {
            // 修正：移除 using，直接建立實例，並於 finally 釋放資源
            Rfc2898DeriveBytes pbkdf2 = null;
            try
            {
                pbkdf2 = new Rfc2898DeriveBytes(sharedSecret, _salt, PBKDF2_ITERATIONS);
                return pbkdf2.GetBytes(32); // 256 bits for AES-256
            }
            finally
            {
                if (pbkdf2 != null)
                {
                #if NETSTANDARD2_0_OR_GREATER || NETCOREAPP2_0_OR_GREATER
                            pbkdf2.Dispose();
                #endif
                }
            }
        }

        /// <summary>
        /// Encrypt the given string using AES.  The string can be decrypted using 
        /// DecryptStringAES().  The sharedSecret parameters must match.
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <param name="sharedSecret">A password used to generate a key for encryption.</param>
        public static string EncryptStringAES(string plainText, string sharedSecret)
        {
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException("plainText");
            if (string.IsNullOrEmpty(sharedSecret))
                throw new ArgumentNullException("sharedSecret");

            string outStr = null;
            RijndaelManaged aesAlg = null;

            try
            {
                byte[] key = DeriveKeyPbkdf2(sharedSecret);

                aesAlg = new RijndaelManaged();
                aesAlg.Key = key;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
                    
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                    }
                    outStr = Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
            finally
            {
                if (aesAlg != null)
                    aesAlg.Clear();
            }

            return outStr;
        }

        /// <summary>
        /// Decrypt the given string.  Assumes the string was encrypted using 
        /// EncryptStringAES(), using an identical sharedSecret.
        /// </summary>
        /// <param name="cipherText">The text to decrypt.</param>
        /// <param name="sharedSecret">A password used to generate a key for decryption.</param>
        public static string DecryptStringAES(string cipherText, string sharedSecret)
        {
            if (string.IsNullOrEmpty(cipherText))
                throw new ArgumentNullException("cipherText");
            if (string.IsNullOrEmpty(sharedSecret))
                throw new ArgumentNullException("sharedSecret");

            RijndaelManaged aesAlg = null;
            string plaintext = null;

            try
            {
                byte[] key = DeriveKeyPbkdf2(sharedSecret);
                byte[] buffer = Convert.FromBase64String(cipherText);

                aesAlg = new RijndaelManaged();
                aesAlg.Key = key;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                byte[] iv = new byte[aesAlg.IV.Length];
                Array.Copy(buffer, 0, iv, 0, iv.Length);
                aesAlg.IV = iv;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(buffer, iv.Length, buffer.Length - iv.Length))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
            finally
            {
                if (aesAlg != null)
                    aesAlg.Clear();
            }

            return plaintext;
        }

        /// <summary>
        /// returns an base64 encoded string
        /// </summary>
        /// <param name="s">string to encode</param>
        /// <returns></returns>
        public static string Encode(string s)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
            string output = System.Convert.ToBase64String(bytes);
            return output;
        }

        /// <summary>
        /// Converts a string from Base64
        /// </summary>
        /// <param name="s">Base64 encoded string</param>
        /// <returns></returns>
        public static String Decode(string s)
        {
            byte[] bytes = System.Convert.FromBase64String(s);
            string output = System.Text.Encoding.UTF8.GetString(bytes);
            return output;
        }

        /// <summary>
        /// From http://weblogs.asp.net/navaidakhtar/archive/2008/07/08/converting-data-table-dataset-into-json-string.aspx
        /// </summary>
        /// <param name="dt"></param>
        /// <returns>string</returns>
        public static string ToJSONString(DataTable dt)
        {
            string[] StrDc = new string[dt.Columns.Count];

            string HeadStr = string.Empty;
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                StrDc[i] = dt.Columns[i].Caption;
                HeadStr += "\"" + StrDc[i] + "\" : \"" + StrDc[i] + i.ToString() + "¾" + "\",";
            }

            HeadStr = HeadStr.Substring(0, HeadStr.Length - 1);
            StringBuilder Sb = new StringBuilder();

            Sb.Append("{\"" + dt.TableName + "\" : [");
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                string TempStr = HeadStr;

                Sb.Append("{");
                for (int j = 0; j < dt.Columns.Count; j++)
                {
                    TempStr = TempStr.Replace(dt.Columns[j] + j.ToString() + "¾", dt.Rows[i][j].ToString());
                }
                Sb.Append(TempStr + "},");
            }
            Sb = new StringBuilder(Sb.ToString().Substring(0, Sb.ToString().Length - 1));

            Sb.Append("]}");
            return Sb.ToString();
        }

        public static string ToJSONSAutocompleteString(string query, DataTable dt)
        {
            char[] badvalues = { '[', ']', '{', '}' };

            foreach (char c in badvalues)
                query = query.Replace(c, '#');

            StringBuilder sb = new StringBuilder();

            sb.Append("{\nquery:'" + query + "',\n");
            sb.Append("suggestions:[");
            
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DataRow row = dt.Rows[i];
                string email = row[0].ToString();
                sb.Append("'" + email + "',");
            }
            
            sb = new StringBuilder(sb.ToString().Substring(0, sb.ToString().Length - 1));
            sb.Append("],\n");
            sb.Append("data:" + sb.ToString().Substring(sb.ToString().IndexOf('['), (sb.ToString().LastIndexOf(']') - sb.ToString().IndexOf('[')) + 1) + "\n}");

            return sb.ToString();
        }

        public string EncodeTicket(string token)
        {
            FormsAuthenticationTicket ticket =
                new FormsAuthenticationTicket(
                    1,
                    token,
                    DateTime.Now,
                    DateTime.Now.AddDays(14),
                    true,
                    "customer",
                    FormsAuthentication.FormsCookiePath
            );

            return FormsAuthentication.Encrypt(ticket);
        }
    }
}
