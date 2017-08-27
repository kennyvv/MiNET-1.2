namespace MiNET.Net
{
	public partial class McpeText : Package<McpeText>
	{
		public string source; // = null;
		public string message; // = null;

		partial void AfterEncode()
		{
			Write(false);
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
					// More stuff
					break;
			}
		}

		public override void Reset()
		{
			type = 0;
			source = null;
			message = null;

			base.Reset();
		}

		partial void AfterDecode()
		{
			ReadBool(); // localization

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
					// More stuff
					break;
			}
		}
	}
}