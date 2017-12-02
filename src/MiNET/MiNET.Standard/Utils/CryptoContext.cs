using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.IO;
using Org.BouncyCastle.Crypto.Parameters;

namespace MiNET.Utils
{
	public class CryptoContext
	{
		public bool UseEncryption;

		//public RijndaelManaged Algorithm { get; set; }
		public ParametersWithIV Algorithm { get; set; }

		public ICryptoTransform Decryptor { get; set; }
		public MemoryStream InputStream { get; set; }
		public CipherStream CryptoStreamIn { get; set; }

		public ICryptoTransform Encryptor { get; set; }
		public MemoryStream OutputStream { get; set; }
		public CipherStream CryptoStreamOut { get; set; }

		public long SendCounter = -1;

		public byte[] Secret;

	}
}