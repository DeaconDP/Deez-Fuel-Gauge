namespace CursorUsageWidget.Services;

internal readonly struct ProtobufField
{
    public int FieldNumber { get; init; }
    public int WireType { get; init; }
    public ulong VarintValue { get; init; }
    public byte[] BytesValue { get; init; }
}

internal ref struct ProtobufWireReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _offset;

    public ProtobufWireReader(ReadOnlySpan<byte> data) => _data = data;

    public bool TryReadField(out ProtobufField field)
    {
        field = default;
        if (!TryReadVarint(out var tag))
            return false;

        var wireType = (int)(tag & 0x7);
        var fieldNumber = (int)(tag >> 3);
        field = new ProtobufField { FieldNumber = fieldNumber, WireType = wireType };

        switch (wireType)
        {
            case 0:
                if (!TryReadVarint(out var varintValue))
                    return false;
                field = field with { VarintValue = varintValue };
                return true;
            case 1:
                if (_offset + 8 > _data.Length)
                    return false;
                field = field with { BytesValue = _data.Slice(_offset, 8).ToArray() };
                _offset += 8;
                return true;
            case 2:
                if (!TryReadVarint(out var length) || length > int.MaxValue)
                    return false;
                var len = (int)length;
                if (_offset + len > _data.Length)
                    return false;
                field = field with { BytesValue = _data.Slice(_offset, len).ToArray() };
                _offset += len;
                return true;
            case 5:
                if (_offset + 4 > _data.Length)
                    return false;
                field = field with { BytesValue = _data.Slice(_offset, 4).ToArray() };
                _offset += 4;
                return true;
            default:
                return false;
        }
    }

    private bool TryReadVarint(out ulong value)
    {
        value = 0;
        var shift = 0;
        while (_offset < _data.Length)
        {
            var b = _data[_offset++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return true;
            shift += 7;
            if (shift > 63)
                return false;
        }

        return false;
    }
}

internal static class ProtobufWireParser
{
    internal static string? ReadStringField(ReadOnlySpan<byte> message, int targetField)
    {
        var reader = new ProtobufWireReader(message);
        while (reader.TryReadField(out var field))
        {
            if (field.FieldNumber == targetField && field.WireType == 2)
                return System.Text.Encoding.UTF8.GetString(field.BytesValue);
        }

        return null;
    }

    internal static ReadOnlySpan<byte> ReadBytesField(ReadOnlySpan<byte> message, int targetField)
    {
        var reader = new ProtobufWireReader(message);
        while (reader.TryReadField(out var field))
        {
            if (field.FieldNumber == targetField && field.WireType == 2)
                return field.BytesValue;
        }

        return ReadOnlySpan<byte>.Empty;
    }

    internal static long? ReadVarintField(ReadOnlySpan<byte> message, int targetField)
    {
        var reader = new ProtobufWireReader(message);
        while (reader.TryReadField(out var field))
        {
            if (field.FieldNumber == targetField && field.WireType == 0)
                return (long)field.VarintValue;
        }

        return null;
    }

    internal static long? ReadTimestampSeconds(ReadOnlySpan<byte> message, int targetField)
    {
        var timestampBytes = ReadBytesField(message, targetField);
        return timestampBytes.IsEmpty ? null : ReadVarintField(timestampBytes, 1);
    }
}
