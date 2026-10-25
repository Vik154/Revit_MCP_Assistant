using System.Text.Json;
using RevitMcp.Shared.Serialization;

namespace RevitMcp.Shared.Pipe;

/// <summary>
/// Протокол кадрирования сообщений в Named Pipe.
/// Формат кадра: [4 байта: длина нагрузки (int32, little-endian)] [N байт: UTF-8 JSON].
/// Кадрирование гарантирует целостность сообщений произвольного размера.
/// </summary>
public static class PipeFraming
{
    /// <summary>Размер префикса длины (в байтах).</summary>
    public const int LengthPrefixSize = sizeof(int);

    /// <summary>Максимальный размер полезной нагрузки (защита от повреждённых данных).</summary>
    public const int MaxPayloadSize = 64 * 1024 * 1024; // 64 МБ

    /// <summary>
    /// Сериализует объект и записывает его в поток как кадрированное сообщение.
    /// </summary>
    public static async Task WriteMessageAsync<T>(Stream stream, T message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Сериализуем объект в UTF-8 байты.
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, SharedJson.Options);

        // Префикс длины. На Windows BitConverter всегда little-endian,
        // но добавляем проверку для переносимости.
        byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthPrefix);
        }

        await stream.WriteAsync(lengthPrefix, ct).ConfigureAwait(false);
        await stream.WriteAsync(payload, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает одно кадрированное сообщение из потока и десериализует его в тип T.
    /// Возвращает default (null для ссылочных), если соединение закрыто.
    /// </summary>
    public static async Task<T?> ReadMessageAsync<T>(Stream stream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // 1. Читаем префикс длины (ровно 4 байта).
        byte[] lengthBuffer = new byte[LengthPrefixSize];
        if (!await ReadExactAsync(stream, lengthBuffer, ct).ConfigureAwait(false))
        {
            return default; // Соединение закрыто.
        }

        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBuffer);
        }
        int payloadLength = BitConverter.ToInt32(lengthBuffer, 0);

        // Защита от повреждённых или враждебных данных.
        if (payloadLength < 0 || payloadLength > MaxPayloadSize)
        {
            throw new InvalidOperationException($"Некорректная длина кадра: {payloadLength}");
        }

        // 2. Читаем полезную нагрузку (ровно payloadLength байт).
        byte[] payload = new byte[payloadLength];
        if (!await ReadExactAsync(stream, payload, ct).ConfigureAwait(false))
        {
            return default; // Соединение закрыто посреди кадра.
        }

        return JsonSerializer.Deserialize<T>(payload, SharedJson.Options);
    }

    /// <summary>
    /// Читает ровно buffer.Length байт из потока.
    /// ВАЖНО: Stream.ReadAsync может вернуть меньше байтов, чем запрошено,
    /// поэтому читаем в цикле, пока не наберём нужное количество.
    /// Возвращает false, если конец потока достигнут раньше.
    /// </summary>
    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = await stream
                .ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct)
                .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                return false; // Конец потока.
            }

            totalRead += bytesRead;
        }
        return true;
    }
}