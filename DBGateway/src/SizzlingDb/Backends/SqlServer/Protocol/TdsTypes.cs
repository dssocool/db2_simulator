namespace SizzlingDb.Backends.SqlServer.Protocol;

internal static class TdsTypes
{
    // Packet types
    public const byte PacketPreLogin = 0x12;
    public const byte PacketLogin = 0x10;
    public const byte PacketSqlBatch = 0x01;
    public const byte PacketAttention = 0x06;
    public const byte PacketResponse = 0x04;

    // Packet status
    public const byte StatusNormal = 0x01;
    public const byte StatusEom = 0x01;
    public const byte StatusIgnore = 0x00;

    // Pre-login tokens
    public const byte PreLoginVersion = 0x00;
    public const byte PreLoginEncryption = 0x01;
    public const byte PreLoginInstOpt = 0x02;
    public const byte PreLoginThreadId = 0x03;
    public const byte PreLoginMars = 0x04;
    public const byte PreLoginTraceId = 0x05;
    public const byte PreLoginFedAuth = 0x06;
    public const byte PreLoginNonce = 0x06;

    public const byte EncryptOff = 0x00;
    public const byte EncryptOn = 0x01;
    public const byte EncryptNotSup = 0x02;
    public const byte EncryptReq = 0x03;

    // Stream tokens
    public const byte TokenEnvChange = 0xE3;
    public const byte TokenLoginAck = 0xAD;
    public const byte TokenInfo = 0xAB;
    public const byte TokenError = 0xAA;
    public const byte TokenReturnStatus = 0x79;
    public const byte TokenColMetadata = 0x81;
    public const byte TokenRow = 0xD1;
    public const byte TokenNbcRow = 0xD2;
    public const byte TokenDone = 0xFD;
    public const byte TokenDoneProc = 0xFE;
    public const byte TokenDoneInProc = 0xFF;

    // ENVCHANGE types
    public const byte EnvDatabase = 0x01;
    public const byte EnvLanguage = 0x02;

    public const uint TdsVersion71 = 0x71000001;
    public const uint TdsVersion74 = 0x74000004;
}

internal static class TdsColumnTypes
{
    public const byte TypeInt4 = 0x38;
    public const byte TypeInt8 = 0x7F;
    public const byte TypeInt2 = 0x34;
    public const byte TypeFloat4 = 0x3B;
    public const byte TypeFloat8 = 0x3E;
    public const byte TypeDecimal = 0x6A;
    public const byte TypeMoney = 0x3C;
    public const byte TypeSmallMoney = 0x7B;
    public const byte TypeBit = 0x32;
    public const byte TypeUniqueIdentifier = 0x24;
    public const byte TypeDate = 0x28;
    public const byte TypeDateTime = 0x3D;
    public const byte TypeDateTime2 = 0x2A;
    public const byte TypeBigVarChar = 0xA7;
    public const byte TypeNVarChar = 0xE7;
}
