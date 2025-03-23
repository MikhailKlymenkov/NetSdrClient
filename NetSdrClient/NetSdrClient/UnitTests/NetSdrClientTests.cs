using Moq;
using NetSdrApp.NetSdr.Models;
using NetSdrApp.NetSdr;
using NetSdrApp.Network;
using Xunit;

namespace UnitTests
{
    public class NetSdrClientTests
    {
        private readonly Mock<ITcpProvider> _tcpProviderMock;
        private readonly Mock<IUdpProvider> _udpProviderMock;
        private readonly NetSdrClient _client;

        public NetSdrClientTests()
        {
            _tcpProviderMock = new Mock<ITcpProvider>();
            _udpProviderMock = new Mock<IUdpProvider>();
            _client = new NetSdrClient(_tcpProviderMock.Object, _udpProviderMock.Object);
        }

        [Fact]
        public void Ctor_ShouldThrowArgumentNullException_WhenTcpProviderIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new NetSdrClient(null, _udpProviderMock.Object));
        }

        [Fact]
        public void Ctor_ShouldThrowArgumentNullException_WhenUdpProviderIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new NetSdrClient(_tcpProviderMock.Object, null));
        }

        [Fact]
        public async Task DisposeAsync_ShouldDisposeResources_WhenCalled()
        {
            // Act
            await _client.DisposeAsync();

            // Assert
            _tcpProviderMock.Verify(x => x.DisposeAsync(), Times.Once);
            _udpProviderMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_ShouldDisposeResources_WhenCalled()
        {
            // Act
            _client.Dispose();

            // Assert
            _tcpProviderMock.Verify(x => x.Dispose(), Times.Once);
            _udpProviderMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public async Task ConnectAsync_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            await _client.DisposeAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => _client.ConnectAsync("localhost"));

            Assert.Equal("NetSdrClient", exception.ObjectName);
        }

        [Fact]
        public async Task ConnectAsync_ShouldThrowArgumentException_WhenHostIsNullOrWhiteSpace()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(() => _client.ConnectAsync(" ", 50000));

            Assert.Equal("host", exception.ParamName);
        }

        [Fact]
        public async Task ConnectAsync_ShouldThrowArgumentOutOfRangeException_WhenPortIsInvalid()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _client.ConnectAsync("localhost", 0));

            Assert.Equal("port", exception.ParamName);
        }

        [Fact]
        public async Task ConnectAsync_ShouldCallTcpProviderConnect_WhenArgumentsAreValid()
        {
            // Act
            await _client.ConnectAsync("localhost", 50000);

            // Assert
            _tcpProviderMock.Verify(x => x.ConnectAsync("localhost", 50000), Times.Once);
        }

        [Fact]
        public async Task DisconnectAsync_ShouldCallDisposeAsync()
        {
            // Act
            await _client.DisconnectAsync();

            // Assert
            _tcpProviderMock.Verify(x => x.DisposeAsync(), Times.Once);
            _udpProviderMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public async Task SetReceiverStateAsync_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            await _client.DisposeAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => _client.SetReceiverStateAsync(ReceiverState.Run, DataMode.Iq, CaptureMode.Contiguous16Bit));

            Assert.Equal("NetSdrClient", exception.ObjectName);
        }

        [Fact]
        public async Task SetReceiverStateAsync_ShouldReturnFailure_WhenNotConnected()
        {
            // Act
            var result = await _client.SetReceiverStateAsync(ReceiverState.Run, DataMode.Iq, CaptureMode.Contiguous16Bit);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task SetReceiverStateAsync_ShouldCallTcpProviderSendAndReceive_WhenConnected()
        {
            // Arrange
            _tcpProviderMock.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>())).Returns(Task.CompletedTask);
            await _client.ConnectAsync("localhost", 50000);

            // Act
            await _client.SetReceiverStateAsync(ReceiverState.Run, DataMode.Iq, CaptureMode.Contiguous16Bit);

            // Assert
            _tcpProviderMock.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Once);
            _tcpProviderMock.Verify(x => x.ReceiveAsync(), Times.Once);
        }

        [Fact]
        public async Task SetReceiverFrequencyAsync_ShouldThrowObjectDisposedException_WhenDisposed()
        {
            // Arrange
            await _client.DisposeAsync();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => _client.SetReceiverFrequencyAsync(ChannelId.Channel1, 100000));

            Assert.Equal("NetSdrClient", exception.ObjectName);
        }

        [Fact]
        public async Task SetReceiverFrequencyAsync_ShouldThrowArgumentOutOfRangeException_WhenFrequencyIsInvalid()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _client.SetReceiverFrequencyAsync(ChannelId.Channel1, 0));

            Assert.Equal("frequencyHz", exception.ParamName);
        }

        [Fact]
        public async Task SetReceiverFrequencyAsync_ShouldThrowArgumentOutOfRangeException_WhenFrequencyIsMoreThan40bit()
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _client.SetReceiverFrequencyAsync(ChannelId.Channel1, ulong.MaxValue));

            Assert.Equal("frequencyHz", exception.ParamName);
        }

        [Fact]
        public async Task SetReceiverFrequencyAsync_ShouldReturnFailure_WhenNotConnected()
        {
            // Act
            var result = await _client.SetReceiverFrequencyAsync(ChannelId.Channel1, 100000);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task SetReceiverFrequencyAsync_ShouldCallTcpProviderSendAndReceive_WhenConnected()
        {
            // Arrange
            _tcpProviderMock.Setup(x => x.ConnectAsync(It.IsAny<string>(), It.IsAny<int>())).Returns(Task.CompletedTask);
            await _client.ConnectAsync("localhost", 50000);

            // Act
            await _client.SetReceiverFrequencyAsync(ChannelId.Channel1, 100000);

            // Assert
            _tcpProviderMock.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Once);
            _tcpProviderMock.Verify(x => x.ReceiveAsync(), Times.Once);
        }

        [Fact]
        public async Task ReceiveAndSaveIQSamplesAsync_ThrowsArgumentNullException_WhenFilePathIsNullOrWhiteSpace()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            string invalidFilePath = null;
            string hostname = "localhost";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _client.ReceiveAndSaveIQSamplesAsync(cancellationToken, invalidFilePath, hostname));
        }

        [Fact]
        public async Task ReceiveAndSaveIQSamplesAsync_ThrowsArgumentNullException_WhenHostNameIsNullOrWhiteSpace()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            string filePath = "validPath.dat";
            string invalidHostName = null;
            int port = 60000;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _client.ReceiveAndSaveIQSamplesAsync(cancellationToken, filePath, invalidHostName, port));
        }

        [Fact]
        public async Task ReceiveAndSaveIQSamplesAsync_ThrowsArgumentOutOfRangeException_WhenPortIsInvalid()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            string filePath = "validPath.dat";
            string hostname = "localhost";
            int invalidPort = 0;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _client.ReceiveAndSaveIQSamplesAsync(cancellationToken, filePath, hostname, invalidPort));
        }
    }
}
