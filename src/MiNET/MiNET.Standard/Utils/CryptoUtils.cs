#region LICENSE

// The contents of this file are subject to the Common Public Attribution
// License Version 1.0. (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// https://github.com/NiclasOlofsson/MiNET/blob/master/LICENSE. 
// The License is based on the Mozilla Public License Version 1.1, but Sections 14 
// and 15 have been added to cover use of software over a computer network and 
// provide for limited attribution for the Original Developer. In addition, Exhibit A has 
// been modified to be consistent with Exhibit B.
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// The Original Code is Niclas Olofsson.
// 
// The Original Developer is the Initial Developer.  The Initial Developer of
// the Original Code is Niclas Olofsson.
// 
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2017 Niclas Olofsson. 
// All Rights Reserved.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using JWT;
using log4net;
using MiNET.Net;
using MiNET.Utils.Skins;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace MiNET.Utils
{
	public static class CryptoUtils
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(CryptoUtils));
		private static JwtBase64UrlEncoder Base64Url = new JwtBase64UrlEncoder();
		public static byte[] DecodeBase64Url(this string input)
		{
			return Base64Url.Decode(input);
		}

		public static string EncodeBase64Url(this byte[] input)
		{
			return Base64Url.Encode(input);
		}

		public static byte[] DecodeBase64(this string input)
		{
			return Convert.FromBase64String(input);
		}

		public static string EncodeBase64(this byte[] input)
		{
			return Convert.ToBase64String(input);
		}

		public static byte[] ToDerEncoded(this ECDiffieHellmanPublicKey key)
		{
			byte[] asn = new byte[24]
			{
				0x30, 0x76, 0x30, 0x10, 0x6, 0x7, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x2,
				0x1, 0x6, 0x5, 0x2b, 0x81, 0x4, 0x0, 0x22, 0x3, 0x62, 0x0, 0x4
			};

			return asn.Concat(key.ToByteArray().Skip(8)).ToArray();
		}

		public static byte[] ToDerEncoded(this ECPublicKeyParameters key)
		{
			byte[] asn = new byte[24]
			{
				0x30, 0x76, 0x30, 0x10, 0x6, 0x7, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x2,
				0x1, 0x6, 0x5, 0x2b, 0x81, 0x4, 0x0, 0x22, 0x3, 0x62, 0x0, 0x4
			};

			return asn.Concat(key.Q.GetEncoded().Skip(8)).ToArray();
		}

		private static byte[] FixPublicKey(byte[] publicKeyBlob)
		{
			var keyType = new byte[] { 0x45, 0x43, 0x4b, 0x33 };
			var keyLength = new byte[] { 0x30, 0x00, 0x00, 0x00 };

			return keyType.Concat(keyLength).Concat(publicKeyBlob.Skip(1)).ToArray();
		}

		public static byte[] ImportECDsaCngKeyFromCngKey(byte[] inKey)
		{
			inKey[2] = 83;
			return inKey;
		}

		public static byte[] Encrypt(byte[] payload, CryptoContext cryptoContext)
		{
			var csEncrypt = cryptoContext.CryptoStreamOut;
			var output = cryptoContext.OutputStream;
			output.Position = 0;
			output.SetLength(0);

			using (MemoryStream hashStream = new MemoryStream())
			{
				// hash

				SHA256Managed crypt = new SHA256Managed();

				hashStream.Write(BitConverter.GetBytes(Interlocked.Increment(ref cryptoContext.SendCounter)), 0, 8);
				hashStream.Write(payload, 0, payload.Length);
				hashStream.Write(cryptoContext.Secret, 0, cryptoContext.Secret.Length);
				var hashBuffer = hashStream.ToArray();

				byte[] validationCheckSum = crypt.ComputeHash(hashBuffer, 0, hashBuffer.Length);

				byte[] content = payload.Concat(validationCheckSum.Take(8)).ToArray();

				csEncrypt.Write(content, 0, content.Length);
				csEncrypt.Flush();
			}

			return output.ToArray();
		}

		public static byte[] Decrypt(byte[] payload, CryptoContext cryptoContext)
		{
			byte[] checksum;
			byte[] clearBytes;

			using (MemoryStream clearBuffer = new MemoryStream())
			{
				//if (Log.IsDebugEnabled)
				//	Log.Debug($"Full payload\n{Package.HexDump(payload)}");

				var input = cryptoContext.InputStream;
				var csDecrypt = cryptoContext.CryptoStreamIn;

				input.Position = 0;
				input.SetLength(0);
				input.Write(payload, 0, payload.Length);
				input.Position = 0;

				var buffer = new byte[payload.Length];
				var read = csDecrypt.Read(buffer, 0, buffer.Length);
				if (read <= 0) Log.Warn("Read 0 lenght from crypto stream");
				clearBuffer.Write(buffer, 0, read);
				csDecrypt.Flush();

				var fullResult = clearBuffer.ToArray();

				//if (Log.IsDebugEnabled)
				//	Log.Debug($"Full content\n{Package.HexDump(fullResult)}");

				clearBytes = (byte[])fullResult.Take(fullResult.Length - 8).ToArray();
				checksum = fullResult.Skip(fullResult.Length - 8).ToArray();
			}

			return clearBytes;
		}

		public static string Encode(object payload, object key, IDictionary<string, object> extraHeaders = null)
		{
			return Encode(JsonConvert.SerializeObject(payload), key, extraHeaders);
		}

		public static string Encode(string payload, object key, IDictionary<string, object> extraHeaders = null)
		{
			return EncodeBytes(Encoding.UTF8.GetBytes(payload), key, extraHeaders);
		}

		public static string EncodeBytes(byte[] payload, object key,  IDictionary<string, object> extraHeaders = null)
		{
			if (payload == null)
				throw new ArgumentNullException(nameof(payload));

			if (extraHeaders == null) //allow overload, but keep backward compatible defaults
			{
				extraHeaders = new Dictionary<string, object> { { "typ", "JWT" } };
			}

			var jwtHeader = new Dictionary<string, object> { { "alg", "ES384" } };
			foreach (var k in extraHeaders)
			{
				jwtHeader.Add(k.Key, k.Value);
			}
		//	Dictionaries.Append(jwtHeader, extraHeaders);
			byte[] headerBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(jwtHeader));

			var bytesToSign = Encoding.UTF8.GetBytes(Serialize(headerBytes, payload));

			var signer = SignerUtilities.GetSigner("SHA-384withECDSA");
			signer.Init(true, (ECPrivateKeyParameters)key);

			signer.BlockUpdate(bytesToSign, 0, bytesToSign.Length);
			byte[] signature = signer.GenerateSignature();

			return Serialize(headerBytes, payload, transcodeSignatureToConcat(signature, 96));
		}

		public static byte[] transcodeSignatureToConcat(byte[] derSignature, int outputLength) {

		if (derSignature.Length < 8 || derSignature[0] != 48) {
			throw new Exception("Invalid ECDSA signature format");
		}

		int offset;
		if (derSignature[1] > 0) {
			offset = 2;
		} else if (derSignature[1] == (byte) 0x81) {
			offset = 3;
		} else {
			throw new Exception("Invalid ECDSA signature format");
		}

		byte rLength = derSignature[offset + 1];

		int i;
		for (i = rLength; (i > 0) && (derSignature[(offset + 2 + rLength) - i] == 0); i--) {
			// do nothing
		}

		byte sLength = derSignature[offset + 2 + rLength + 1];

		int j;
		for (j = sLength; (j > 0) && (derSignature[(offset + 2 + rLength + 2 + sLength) - j] == 0); j--) {
			// do nothing
		}

		int rawLen = Math.Max(i, j);
		rawLen = Math.Max(rawLen, outputLength / 2);

		if ((derSignature[offset - 1] & 0xff) != derSignature.Length - offset
			|| (derSignature[offset - 1] & 0xff) != 2 + rLength + 2 + sLength
			|| derSignature[offset] != 2
			|| derSignature[offset + 2 + rLength] != 2) {
			throw new Exception("Invalid ECDSA signature format");
		}

		byte[] concatSignature = new byte[2 * rawLen];

	    Array.Copy(derSignature, (offset + 2 + rLength) - i, concatSignature, rawLen - i, i);
		Array.Copy(derSignature, (offset + 2 + rLength + 2 + sLength) - j, concatSignature, 2 * rawLen - j, j);

		return concatSignature;
	}

		private static string Serialize(params byte[][] parts)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (byte[] part in parts)
				stringBuilder.Append(Base64Url.Encode(part)).Append(".");
			stringBuilder.Remove(stringBuilder.Length - 1, 1);
			return stringBuilder.ToString();
		}

		public static CertificateData Decode(string token, AsymmetricKeyParameter key)
		{
			string[] parts = token.Split('.');
			string header = parts[0];
			string payload = parts[1];
			byte[] crypto = parts[2].DecodeBase64Url();

			string headerJson = Encoding.UTF8.GetString(header.DecodeBase64Url());
			JObject headerData = JObject.Parse(headerJson);

			string payloadJson = Encoding.UTF8.GetString(payload.DecodeBase64Url());
			JObject payloadData = JObject.Parse(payloadJson);
			return payloadData.ToObject<CertificateData>();
		}

		// CLIENT TO SERVER STUFF

		public static byte[] CompressJwtBytes(byte[] certChain, byte[] skinData, CompressionLevel compressionLevel)
		{
			using (MemoryStream stream = MiNetServer.MemoryStreamManager.GetStream())
			{
				{
					{
						byte[] lenBytes = BitConverter.GetBytes(certChain.Length);
						//Array.Reverse(lenBytes);
						stream.Write(lenBytes, 0, lenBytes.Length); // ??
						stream.Write(certChain, 0, certChain.Length);
					}
					{
						byte[] lenBytes = BitConverter.GetBytes(skinData.Length);
						//Array.Reverse(lenBytes);
						stream.Write(lenBytes, 0, lenBytes.Length); // ??
						stream.Write(skinData, 0, skinData.Length);
					}
				}

				var bytes = stream.ToArray();

				return bytes;
			}
		}
	}
}