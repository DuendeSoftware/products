// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Duende.IdentityServer.Internal.Saml.Infrastructure;

namespace UnitTests.Saml;

public class LimitedReadStreamTests
{
    private const string Category = "SAML Limited Read Stream";

    [Fact]
    [Trait("Category", Category)]
    public void read_within_limit_should_succeed()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello World");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);
        var buffer = new byte[data.Length];

        // Act
        var bytesRead = limitedStream.Read(buffer, 0, buffer.Length);

        // Assert
        bytesRead.ShouldBe(data.Length);
        Encoding.UTF8.GetString(buffer).ShouldBe("Hello World");
    }

    [Fact]
    [Trait("Category", Category)]
    public void read_exactly_at_limit_should_succeed()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, data.Length);
        var buffer = new byte[data.Length];

        // Act
        var bytesRead = limitedStream.Read(buffer, 0, buffer.Length);

        // Assert
        bytesRead.ShouldBe(data.Length);
        Encoding.UTF8.GetString(buffer).ShouldBe("Hello");
    }

    [Fact]
    [Trait("Category", Category)]
    public void read_exceeds_limit_should_throw_invalid_operation_exception()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello World");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 5);
        var buffer = new byte[data.Length];

        // Act - first read should succeed
        limitedStream.ReadExactly(buffer, 0, 5);

        // Assert - second read should throw
        var exception = Should.Throw<InvalidOperationException>(() =>
            limitedStream.Read(buffer, 0, buffer.Length));

        exception.Message.ShouldBe("Maximum stream size exceeded.");
    }

    [Fact]
    [Trait("Category", Category)]
    public void read_multiple_reads_within_limit_should_succeed()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello World");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);
        var buffer1 = new byte[5];
        var buffer2 = new byte[6];

        // Act
        var bytesRead1 = limitedStream.Read(buffer1, 0, buffer1.Length);
        var bytesRead2 = limitedStream.Read(buffer2, 0, buffer2.Length);

        // Assert
        bytesRead1.ShouldBe(5);
        bytesRead2.ShouldBe(6);
        Encoding.UTF8.GetString(buffer1).ShouldBe("Hello");
        Encoding.UTF8.GetString(buffer2).ShouldBe(" World");
    }

    [Fact]
    [Trait("Category", Category)]
    public void read_multiple_reads_exceeding_limit_should_throw_on_excess()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello World");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 7);
        var buffer = new byte[5];

        // Act & Assert
        limitedStream.Read(buffer, 0, 5).ShouldBe(5); // 5 bytes read
        limitedStream.Read(buffer, 0, 2).ShouldBe(2); // 7 bytes total (at limit)

        Should.Throw<InvalidOperationException>(() =>
            limitedStream.Read(buffer, 0, 1)); // Would exceed limit
    }

    [Fact]
    [Trait("Category", Category)]
    public void read_empty_stream_should_return_zero()
    {
        // Arrange
        using var innerStream = new MemoryStream();
        using var limitedStream = new LimitedReadStream(innerStream, 100);
        var buffer = new byte[10];

        // Act
        var bytesRead = limitedStream.Read(buffer, 0, buffer.Length);

        // Assert
        bytesRead.ShouldBe(0);
    }

    [Fact]
    [Trait("Category", Category)]
    public void read_with_zero_max_bytes_should_throw_immediately()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 0);
        var buffer = new byte[5];

        // Act & Assert
        Should.Throw<InvalidOperationException>(() =>
            limitedStream.Read(buffer, 0, buffer.Length));
    }

    [Fact]
    [Trait("Category", Category)]
    public void can_read_should_delegate_to_inner_stream()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);

        // Assert
        limitedStream.CanRead.ShouldBe(innerStream.CanRead);
        limitedStream.CanRead.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void can_seek_should_delegate_to_inner_stream()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);

        // Assert
        limitedStream.CanSeek.ShouldBe(innerStream.CanSeek);
        limitedStream.CanSeek.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void can_write_should_delegate_to_inner_stream()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);

        // Assert
        limitedStream.CanWrite.ShouldBe(innerStream.CanWrite);
        limitedStream.CanWrite.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void length_should_delegate_to_inner_stream()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);

        // Assert
        limitedStream.Length.ShouldBe(innerStream.Length);
        limitedStream.Length.ShouldBe(data.Length);
    }

    [Fact]
    [Trait("Category", Category)]
    public void position_get_should_delegate_to_inner_stream()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);

        // Act
        var buffer = new byte[3];
        limitedStream.ReadExactly(buffer, 0, 3);

        // Assert
        limitedStream.Position.ShouldBe(innerStream.Position);
        limitedStream.Position.ShouldBe(3);
    }

    [Fact]
    [Trait("Category", Category)]
    public void position_set_should_delegate_to_inner_stream()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);

        // Act
        limitedStream.Position = 2;

        // Assert
        limitedStream.Position.ShouldBe(2);
        innerStream.Position.ShouldBe(2);
    }

    [Fact]
    [Trait("Category", Category)]
    public void seek_should_delegate_to_inner_stream()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello World");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);

        // Act
        var newPosition = limitedStream.Seek(6, SeekOrigin.Begin);

        // Assert
        newPosition.ShouldBe(6);
        limitedStream.Position.ShouldBe(6);
        innerStream.Position.ShouldBe(6);
    }

    [Fact]
    [Trait("Category", Category)]
    public void flush_should_delegate_to_inner_stream()
    {
        // Arrange
        using var innerStream = new MemoryStream();
        using var limitedStream = new LimitedReadStream(innerStream, 100);

        // Act & Assert - should not throw
        Should.NotThrow(() => limitedStream.Flush());
    }

    [Fact]
    [Trait("Category", Category)]
    public void write_should_delegate_to_inner_stream()
    {
        // Arrange
        using var innerStream = new MemoryStream();
        using var limitedStream = new LimitedReadStream(innerStream, 100);
        var data = Encoding.UTF8.GetBytes("Hello");

        // Act
        limitedStream.Write(data, 0, data.Length);

        // Assert
        innerStream.ToArray().ShouldBe(data);
    }

    [Fact]
    [Trait("Category", Category)]
    public void set_length_should_delegate_to_inner_stream()
    {
        // Arrange
        using var innerStream = new MemoryStream();
        using var limitedStream = new LimitedReadStream(innerStream, 100);

        // Act
        limitedStream.SetLength(50);

        // Assert
        limitedStream.Length.ShouldBe(50);
        innerStream.Length.ShouldBe(50);
    }

    [Fact]
    [Trait("Category", Category)]
    public void dispose_should_dispose_inner_stream()
    {
        // Arrange
        var innerStream = new MemoryStream();
        var limitedStream = new LimitedReadStream(innerStream, 100);

        // Act
        limitedStream.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() => innerStream.ReadByte());
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task dispose_async_should_dispose_inner_stream()
    {
        // Arrange
        var innerStream = new MemoryStream();
        var limitedStream = new LimitedReadStream(innerStream, 100);

        // Act
        await limitedStream.DisposeAsync();

        // Assert
        Should.Throw<ObjectDisposedException>(() => innerStream.ReadByte());
    }

    [Fact]
    [Trait("Category", Category)]
    public void read_limits_read_size_when_requested_count_exceeds_limit()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello World");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 5);
        var buffer = new byte[100];

        // Act - request 100 bytes but limit is 5
        var bytesRead = limitedStream.Read(buffer, 0, 100);

        // Assert
        bytesRead.ShouldBe(5);
        Encoding.UTF8.GetString(buffer, 0, bytesRead).ShouldBe("Hello");
    }

    [Fact]
    [Trait("Category", Category)]
    public void read_with_offset_should_write_to_correct_position()
    {
        // Arrange
        var data = Encoding.UTF8.GetBytes("Hello");
        using var innerStream = new MemoryStream(data);
        using var limitedStream = new LimitedReadStream(innerStream, 100);
        var buffer = new byte[10];

        // Act
        var bytesRead = limitedStream.Read(buffer, 3, 5);

        // Assert
        bytesRead.ShouldBe(5);
        buffer[0].ShouldBe((byte)0);
        buffer[1].ShouldBe((byte)0);
        buffer[2].ShouldBe((byte)0);
        Encoding.UTF8.GetString(buffer, 3, bytesRead).ShouldBe("Hello");
    }
}
