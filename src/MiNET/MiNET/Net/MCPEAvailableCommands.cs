using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using MiNET.Plugins;

namespace MiNET.Net
{
	public partial class McpeAvailableCommands
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(McpeAvailableCommands));

		public CommandSet CommandSet { get; set; }

		partial void AfterDecode()
		{
			{
				uint count = ReadUnsignedVarInt();
				Log.Warn($"Enum values {count}");
				for (int i = 0; i < count; i++)
				{
					string s = ReadString();
					Log.Debug(s);
				}
			}

			{
				uint count = ReadUnsignedVarInt();
				Log.Warn($"Postfix values {count}");
				for (int i = 0; i < count; i++)
				{
					string s = ReadString();
					Log.Debug(s);
				}
			}

			{
				uint count = ReadUnsignedVarInt();
				Log.Warn($"Enum indexes {count}");

				for (int i = 0; i < count; i++)
				{
					string s = ReadString();
					uint c = ReadUnsignedVarInt();
					Log.Debug($"{s}:{c}");
					for (int j = 0; j < c; j++)
					{
						int idx = ReadShort();
						Log.Debug($"{s}:{c}:{idx}");
					}
				}
			}

			{
				uint count = ReadUnsignedVarInt();
				Log.Warn($"Commands definitions {count}");
				for (int i = 0; i < count; i++)
				{
					string commandName = ReadString();
					string description = ReadString();
					int flags = ReadByte();
					int permissions = ReadByte();

					int aliasEnumIndex = ReadInt();

					uint overloadCount = ReadUnsignedVarInt();
					Log.Debug($"{commandName}, {description}, {flags}, {permissions}, {aliasEnumIndex}, {overloadCount}");
					for (int j = 0; j < overloadCount; j++)
					{
						uint parameterCount = ReadUnsignedVarInt();
						for (int k = 0; k < parameterCount; k++)
						{
							string commandParamName = ReadString();
							int tmp = ReadShort();
							int tmp1 = ReadShort();
							bool isEnum = (tmp1 & 0x30) == 0x30;
							int commandParamType = -1;
							int commandParamEnumIndex = -1;
							int commandParamPostfixIndex = -1;
							if ((tmp1 & 0x30) == 0x30)
							{
								commandParamEnumIndex = tmp & 0xffff;
							}
							else if ((tmp1 & 0x100) == 0x100)
							{
								commandParamPostfixIndex = tmp & 0xffff;
							}
							else if ((tmp1 & 0x10) == 0x10)
							{
								commandParamType = tmp & 0xffff;
							}
							else
							{
								Log.Warn("No parameter style read (enum, valid, postfix)");
							}

							bool optional = ReadBool();
							Log.Debug($"{commandName}, {parameterCount}, {commandParamName}, 0x{tmp1:X4}, {isEnum}, {commandParamType}, {commandParamEnumIndex}, {commandParamPostfixIndex}, {optional}");
						}
					}
				}
			}
		}

		partial void AfterEncode()
		{
			try
			{
				if (CommandSet == null)
				{
					Log.Warn("No commands");
					return;
				}

				var commands = CommandSet;

				List<string> stringList = new List<string>();
				{
					foreach (var command in commands.Values)
					{
						var overloads = command.Versions[0].Overloads;
						foreach (var overload in overloads.Values)
						{
							var parameters = overload.Input.Parameters;
							if (parameters == null) continue;
							foreach (var parameter in parameters)
							{
								if (parameter.Type == "stringenum")
								{
									if (parameter.EnumValues == null) continue;
									foreach (var enumValue in parameter.EnumValues)
									{
										if (!stringList.Contains(enumValue))
										{
											stringList.Add(enumValue);
										}
									}
								}
							}
						}
					}

					WriteUnsignedVarInt((uint)stringList.Count); // Enum values
					foreach (var s in stringList)
					{
						Write(s);
						Log.Debug($"String: {s}, {(short)stringList.IndexOf(s)} ");
					}
				}

				WriteUnsignedVarInt(0); // Postfixes

				List<string> enumList = new List<string>();
				foreach (var command in commands.Values)
				{
					var overloads = command.Versions[0].Overloads;
					foreach (var overload in overloads.Values)
					{
						var parameters = overload.Input.Parameters;
						if (parameters == null) continue;
						foreach (var parameter in parameters)
						{
							if (parameter.Type == "stringenum")
							{
								if (parameter.EnumValues == null) continue;

								if (!enumList.Contains(parameter.EnumType))
								{
									enumList.Add(parameter.EnumType);
								}
							}
						}
					}
				}

				//WriteUnsignedVarInt(0); // Enum indexes
				WriteUnsignedVarInt((uint)enumList.Count); // Enum indexes
				foreach (var command in commands.Values)
				{
					var overloads = command.Versions[0].Overloads;
					foreach (var overload in overloads.Values)
					{
						var parameters = overload.Input.Parameters;
						if (parameters == null) continue;
						foreach (var parameter in parameters)
						{
							if (parameter.Type == "stringenum")
							{
								if (parameter.EnumValues == null) continue;

								Write(parameter.EnumType);
								WriteUnsignedVarInt((uint)parameter.EnumValues.Length);
								foreach (var enumValue in parameter.EnumValues)
								{
									if (stringList.Count < 256)
									{
										Write((byte)stringList.IndexOf(enumValue));
									}
									else
									{
										Write((byte)stringList.IndexOf(enumValue));
									}
									Log.Debug($"EnumType: {parameter.EnumType}, {enumValue}, {(short)stringList.IndexOf(enumValue)} ");
								}
							}
						}
					}
				}

				WriteUnsignedVarInt((uint)commands.Count);

				foreach (var command in commands.Values)
				{
					//if(!command.Name.Equals("op")) continue;

					Write(command.Name);
					Write(command.Versions[0].Description);
					Write((byte)0); // flags
					Write((byte)0); // permissions
					Write((int)-1); // Enum index


					Log.Warn($"Writing command {command.Name}");

					var overloads = command.Versions[0].Overloads;
					WriteUnsignedVarInt((uint)overloads.Count); // Overloads
					foreach (var overload in overloads.Values)
					{
						Log.Warn($"Writing command overload {command.Name}");

						var parameters = overload.Input.Parameters;
						if (parameters == null)
						{
							WriteUnsignedVarInt(0); // Parameter count
							continue;
						}
						WriteUnsignedVarInt((uint)parameters.Length); // Parameter count
						foreach (var parameter in parameters)
						{
							Log.Warn($"Writing command overload parameter {command.Name}, {parameter.Name}, {parameter.Type}");

							Write(parameter.Name); // parameter name
							if (parameter.Type == "stringenum" && parameter.EnumValues != null)
							{
								Write((short)enumList.IndexOf(parameter.EnumType));
								Write((short)0x30);
							}
							else
							{
								Write((short)GetParameterTypeId(parameter.Type)); // param type
								Write((short)0x10);
							}

							Write(parameter.Optional); // optional
						}
					}
				}
			}
			catch (Exception e)
			{
				Log.Error("Sending commands", e);
				//throw;
			}
		}

		private int GetParameterTypeId(string type)
		{
			if (type == "int") return 0x03;
			if (type == "float") return 0x02;
			if (type == "value") return 0x03;
			if (type == "target") return 0x04;
			if (type == "string") return 0x0d;
			if (type == "stringenum") return 0x0d;
			if (type == "blockpos") return 0x0e;

			return 0x0d;
		}
	}
}
