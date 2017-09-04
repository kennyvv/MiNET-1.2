namespace MiNET.Net
{
	public partial class McpeText : Package<McpeText>
	{
		public string source; // = null;
		public string message; // = null;
		public string xuid; //  = null;
		public string[] parameters;

		partial void AfterEncode()
		{
			//Write(islocalized);
			ChatTypes chatType = (ChatTypes)type;
			switch (chatType)
			{
				case ChatTypes.Chat:
				case ChatTypes.Whisper:
				case ChatTypes.Announcement:
					Write(source);
					Write(message);
					break;
				case ChatTypes.Raw:
				case ChatTypes.Tip:
				case ChatTypes.System:
					Write(message);
					break;
				case ChatTypes.Popup:
				case ChatTypes.Translation:
				case ChatTypes.JukeboxPopup:
					Write(message);
					WriteVarInt(parameters.Length);
					foreach (var p in parameters)
					{
						Write(p);
					}
					break;
			}
			Write(xuid);
		}

		public override void Reset()
		{
			type = 0;
			source = null;
			message = null;
			xuid = null;
			parameters = new string[0];

			base.Reset();
		}

		partial void AfterDecode()
		{
			//ReadBool(); // localization

			ChatTypes chatType = (ChatTypes)type;
			switch (chatType)
			{
				case ChatTypes.Chat:
				case ChatTypes.Whisper:
				case ChatTypes.Announcement:
					source = ReadString();
					message = ReadString();
					break;
				case ChatTypes.Raw:
				case ChatTypes.Tip:
				case ChatTypes.System:
					message = ReadString();
					break;

				case ChatTypes.Popup:
				case ChatTypes.Translation:
				case ChatTypes.JukeboxPopup:
					message = ReadString();
					int count = ReadVarInt();
					parameters = new string[count];
					for (int i = 0; i < parameters.Length; i++)
					{
						parameters[i] = ReadString();
					}
					// More stuff
					break;
			}

			xuid = ReadString();
		}
	}
}