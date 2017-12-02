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
using JetBrains.Annotations;
using Jose;
using Jose.netstandard1_4;
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

		public static byte[] ToDerEncoded([NotNull] this ECDiffieHellmanPublicKey key)
		{
			byte[] asn = new byte[24]
			{
				0x30, 0x76, 0x30, 0x10, 0x6, 0x7, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x2,
				0x1, 0x6, 0x5, 0x2b, 0x81, 0x4, 0x0, 0x22, 0x3, 0x62, 0x0, 0x4
			};

			return asn.Concat(key.ToByteArray().Skip(8)).ToArray();
		}

		public static byte[] ToDerEncoded([NotNull] this ECPublicKeyParameters key)
		{
			byte[] asn = new byte[24]
			{
				0x30, 0x76, 0x30, 0x10, 0x6, 0x7, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x2,
				0x1, 0x6, 0x5, 0x2b, 0x81, 0x4, 0x0, 0x22, 0x3, 0x62, 0x0, 0x4
			};

			return asn.Concat(key.Q.GetEncoded().Skip(8)).ToArray();
		}

		public static ECDiffieHellmanPublicKey FromDerEncoded(byte[] keyBytes)
		{
			var clientPublicKeyBlob = FixPublicKey(keyBytes.Skip(23).ToArray());
			ECDiffieHellmanPublicKey clientKey = ECDiffieHellmanCngPublicKey.FromByteArray(clientPublicKeyBlob, CngKeyBlobFormat.EccPublicBlob);
			return clientKey;
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
				hashStream.Write(cryptoContext.Algorithm.Key, 0, cryptoContext.Algorithm.Key.Length);
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

		public static string Encode(object payload, object key, JwsAlgorithm algorithm, IDictionary<string, object> extraHeaders = null, JwtSettings settings = null)
		{
			return Encode(JsonConvert.SerializeObject(payload), key, algorithm, extraHeaders, settings);
		}

		public static string Encode(string payload, object key, JwsAlgorithm algorithm, IDictionary<string, object> extraHeaders = null, JwtSettings settings = null)
		{
			return EncodeBytes(Encoding.UTF8.GetBytes(payload), key, algorithm, extraHeaders, settings);
		}

		public static string EncodeBytes(byte[] payload, object key, JwsAlgorithm algorithm, IDictionary<string, object> extraHeaders = null, JwtSettings settings = null)
		{
			if (payload == null)
				throw new ArgumentNullException(nameof(payload));

			if (extraHeaders == null) //allow overload, but keep backward compatible defaults
			{
				extraHeaders = new Dictionary<string, object> { { "typ", "JWT" } };
			}

			var jwtHeader = new Dictionary<string, object> { { "alg", algorithm.ToString() } };

			Dictionaries.Append(jwtHeader, extraHeaders);
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

		public static CngKey GenerateClientKey()
		{
			CngKey newKey = CngKey.Create(CngAlgorithm.ECDiffieHellmanP384, null, new CngKeyCreationParameters() { ExportPolicy = CngExportPolicies.AllowPlaintextExport, KeyUsage = CngKeyUsages.AllUsages });
			return newKey;
		}

		public static byte[] EncodeJwt(string username, CngKey newKey, bool isEmulator)
		{
			byte[] t = ImportECDsaCngKeyFromCngKey(newKey.Export(CngKeyBlobFormat.EccPrivateBlob));
			CngKey tk = CngKey.Import(t, CngKeyBlobFormat.EccPrivateBlob);

			ECDiffieHellmanCng ecKey = new ECDiffieHellmanCng(newKey);
			ecKey.HashAlgorithm = CngAlgorithm.Sha256;
			ecKey.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;

			string b64Key = Convert.ToBase64String(ecKey.PublicKey.ToDerEncoded());

			long exp = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeMilliseconds();

			CertificateData certificateData = new CertificateData
			{
				Exp = exp,
				Iat = exp,
				ExtraData = new ExtraData
				{
					DisplayName = username,
					Identity = isEmulator ? Guid.NewGuid().ToString() : "85e4febd-3d33-4008-b044-1ad9fb85b26c",
				},
				Iss = "self",
				IdentityPublicKey = b64Key,
				CertificateAuthority = true,
				Nbf = exp,
				RandomNonce = new Random().Next(),
			};

			//			string txt = $@"{{
			//	""exp"": 1467508449,
			//	""extraData"": {{
			//		""displayName"": ""gurunxx"",
			//		""identity"": ""4e0199c6-7cfd-3550-b676-74398e0a5f1a""
			//	}},
			//	""identityPublicKey"": ""{b64Key}"",
			//	""nbf"": 1467508448
			//}}";

			string val = JWT.Encode(certificateData, tk, JwsAlgorithm.ES384, new Dictionary<string, object> { { "x5u", b64Key } });

			Log.Warn(JWT.Payload(val));

			Log.Warn(string.Join(";", JWT.Headers(val)));

			//val = "eyJhbGciOiJFUzM4NCIsIng1dSI6Ik1IWXdFQVlIS29aSXpqMENBUVlGSzRFRUFDSURZZ0FFREVLck5xdk93Y25iV3I5aUtVQ0MyeklFRmZ6Q0VnUEhQdG5Kd3VEdnZ3VjVtd1E3QzNkWmhqd0g0amxWc2RDVTlNdVl2QllQRktCTEJkWU52K09ZeW1MTFJGTU9odVFuSDhuZFRRQVV6VjJXRTF4dHdlVG1wSVFzdXdmVzRIdzAifQo.eyJleHAiOjE0Njc1MDg0NDksImV4dHJhRGF0YSI6eyJkaXNwbGF5TmFtZSI6Imd1cnVueHgiLCJpZGVudGl0eSI6IjRlMDE5OWM2LTdjZmQtMzU1MC1iNjc2LTc0Mzk4ZTBhNWYxYSJ9LCJpZGVudGl0eVB1YmxpY0tleSI6Ik1IWXdFQVlIS29aSXpqMENBUVlGSzRFRUFDSURZZ0FFREVLck5xdk93Y25iV3I5aUtVQ0MyeklFRmZ6Q0VnUEhQdG5Kd3VEdnZ3VjVtd1E3QzNkWmhqd0g0amxWc2RDVTlNdVl2QllQRktCTEJkWU52K09ZeW1MTFJGTU9odVFuSDhuZFRRQVV6VjJXRTF4dHdlVG1wSVFzdXdmVzRIdzAiLCJuYmYiOjE0Njc1MDg0NDh9Cg.jpCqzTo8nNVEW8ArK1NFBaqLx6kyJV6wPF8cAU6UGav6cfMc60o3m5DjwspN-JcyC14AlcNiPdWX8TEm1QFhtScb-bXo4WOJ0dNYXV8iI_eCTCcXMFjX4vgIHpb9xfjv";
			val = $@"{{ ""chain"": [""{val}""] }}";

			return Encoding.UTF8.GetBytes(val);
		}

		public static byte[] EncodeSkinJwt(CngKey newKey)
		{
			byte[] t = ImportECDsaCngKeyFromCngKey(newKey.Export(CngKeyBlobFormat.EccPrivateBlob));
			CngKey tk = CngKey.Import(t, CngKeyBlobFormat.EccPrivateBlob);

			ECDiffieHellmanCng ecKey = new ECDiffieHellmanCng(newKey);
			ecKey.HashAlgorithm = CngAlgorithm.Sha256;
			ecKey.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;

			var b64Key = Base64Url.Encode(ecKey.PublicKey.ToDerEncoded());

			Skin skin = new Skin
			{
				Slim = false,
				SkinData = Encoding.Default.GetBytes(new string('Z', 8192)),
				SkinId = "Standard_Custom"
			};

			string skin64 = Convert.ToBase64String(skin.SkinData);


			//{
			//	"ADRole": 2,
			//	"ClientRandomId": 4670680294016914277,
			//	"CurrentInputMode": 2,
			//	"DefaultInputMode": 2,
			//	"DeviceModel": "SAMSUNG GT-P5210",
			//	"DeviceOS": 1,
			//	"GameVersion": "1.1.0.4",
			//	"GuiScale": 0,
			//	"LanguageCode": "en_US",
			//	"ServerAddress": "yodamine.com:19132",
			//	"SkinData": "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79m9/+zUuP/v2r//8t/I//Ldwv/y3cL/7NS4/+zUuP/v2r//7NS4//LfyP/y3cL/8t3C/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABkQSz/ZEEs/2RBLP9kQSz/KCgo/ygoKP8oKCj/KCgo/wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA6J1M/3q1d/96r3f/2LqU/+vTs//YupT/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA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",
			//	"SkinId": "Standard_Alex",
			//	"TenantId": "",
			//	"UIProfile": 1
			//}

			string skinData = $@"
{{
	""ADRole"": 0,
	""ClientRandomId"": {new Random().Next()},
	""CurrentInputMode"": 1,
	""DefaultInputMode"": 1,
	""DeviceModel"": ""MINET CLIENT"",
	""DeviceOS"": 7,
	""GameVersion"": ""1.2.0.15"",
	""GuiScale"": 0,
	""LanguageCode"": ""en_US"",
	""ServerAddress"": ""yodamine.com:19132"",
	""SkinData"": ""{skin64}"",
	""SkinId"": ""{skin.SkinId}"",
	""TenantId"": ""75a3f792-a259-4428-9a8d-4e832fb960e4"",
	""UIProfile"": 0
}}";

			string val = JWT.Encode(skinData, tk, JwsAlgorithm.ES384, new Dictionary<string, object> { { "x5u", b64Key } });

			return Encoding.UTF8.GetBytes(val);
		}

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