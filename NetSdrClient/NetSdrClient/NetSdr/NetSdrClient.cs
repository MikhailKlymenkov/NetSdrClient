using NetSdrApp.NetSdr.Models;
using NetSdrApp.Network;
using System.Net.Sockets;

namespace NetSdrApp.NetSdr
{
    public class NetSdrClient : IAsyncDisposable, IDisposable
    {
        private const int DefaultTcpPort = 50000;
        private const int DefaultUdpPort = 60000;
        private const int FileBufferSize = 4096;

        private readonly ITcpProvider _tcpProvider;
        private readonly IUdpProvider _udpProvider;

        private bool _isTcpConnected;
        private bool _isUdpConnected;
        private bool _disposed;

        public NetSdrClient(ITcpProvider tcpProvider, IUdpProvider udpProvider)
        {
            _tcpProvider = tcpProvider ?? throw new ArgumentNullException(nameof(tcpProvider));
            _udpProvider = udpProvider ?? throw new ArgumentNullException(nameof(udpProvider));
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _isTcpConnected = false;
                _isUdpConnected = false;

                await _tcpProvider.DisposeAsync();
                _udpProvider?.Dispose();

                GC.SuppressFinalize(this);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _isTcpConnected = false;
                _isUdpConnected = false;

                _tcpProvider?.Dispose();
                _udpProvider?.Dispose();

                GC.SuppressFinalize(this);
            }
        }

        public async Task ConnectAsync(string host, int port = DefaultTcpPort)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetSdrClient));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(host);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);

            if (!_isTcpConnected)
            {
                await _tcpProvider.ConnectAsync(host, port).ConfigureAwait(false);

                _isTcpConnected = true;
            }
        }

        public async ValueTask DisconnectAsync()
        {
            await DisposeAsync();
        }

        public async Task<ControlItemSetOperationModel> SetReceiverStateAsync(ReceiverState receiverState, DataMode dataMode, CaptureMode captureMode, byte fifoSamplesNumber = 0x00)
        {
            const byte messageLength = 0x08;

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetSdrClient));
            }

            if (!_isTcpConnected)
            {
                return new ControlItemSetOperationModel
                {
                    IsSuccess = false,
                    ErrorMessage = "Not connected to the device."
                };
            }

            byte[] message =
            {
                messageLength,  // Length (LSB)
                0x00,  // Length (MSB)
                (byte)ControlItem.ReceiverState,  // Control Item Code
                0x00,  // Control Item SubCode
                (byte)dataMode,  // Data mode
                (byte)receiverState,  // Run/Stop
                (byte)captureMode,  // Capture mode
                captureMode == CaptureMode.Fifo16Bit ? fifoSamplesNumber : (byte)0x00, // the number of 4096 16 bit data samples to capture in the FIFO mode
            };

            await _tcpProvider.SendAsync(message);
            byte[] response = await _tcpProvider.ReceiveAsync();

            bool isValidResponse = response != null && response.Length == 8 &&
                                   response[0] == message[0] && response[1] == message[1] &&
                                   response[2] == message[2] && response[3] == message[3] &&
                                   response[5] == message[5];

            return new ControlItemSetOperationModel
            {
                IsSuccess = isValidResponse,
                ErrorMessage = isValidResponse ? null : GetSetControlItemError(response, "Failed to change receiver state.")
            };
        }

        public async Task<ControlItemSetOperationModel> SetReceiverFrequencyAsync(ChannelId channelId, ulong frequencyHz)
        {
            const byte messageLength = 0x0A;

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetSdrClient));
            }

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequencyHz);

            // Validate frequency range (must fit within 40 bits)
            const ulong frequencyMaxValue = 0xFFFFFFFFFF;

            if (frequencyHz > frequencyMaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(frequencyHz), "Frequency value exceeds 40-bit limit.");
            }

            if (!_isTcpConnected)
            {
                return new ControlItemSetOperationModel
                {
                    IsSuccess = false,
                    ErrorMessage = "Not connected to the device."
                };
            }

            byte[] message =
            {
                messageLength,  // Length (LSB)
                0x00,  // Length (MSB)
                (byte)ControlItem.ReceiverFrequency,  // Control Item Code
                0x00,  // Control Item SubCode
                (byte)channelId,  // Channel ID
                (byte)(frequencyHz & 0xFF), // 5-byte frequency (Little Endian)
                (byte)(frequencyHz >> 8 & 0xFF),
                (byte)(frequencyHz >> 16 & 0xFF),
                (byte)(frequencyHz >> 24 & 0xFF),
                (byte)(frequencyHz >> 32 & 0xFF),
            };

            await _tcpProvider.SendAsync(message);
            byte[] response = await _tcpProvider.ReceiveAsync();

            bool isValidResponse = response != null
                                    && response.Length == message.Length
                                    && response.SequenceEqual(message);

            return new ControlItemSetOperationModel
            {
                IsSuccess = isValidResponse,
                ErrorMessage = isValidResponse ? null : GetSetControlItemError(response, "Failed to set frequency.")
            };
        }

        public async Task<bool> ReceiveAndSaveIQSamplesAsync(CancellationToken cancellationToken, string filePath, string hostname, int port = DefaultUdpPort)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetSdrClient));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
            ArgumentOutOfRangeException.ThrowIfNullOrWhiteSpace(hostname);

            const int headerSize = 4;
            bool dataSaved = false;

            ConnectToUdp(hostname, port);

            // TODO: Add FileProvider
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, FileBufferSize, true))
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        Task<UdpReceiveResult> receiveTask = _udpProvider.ReceiveAsync();

                        await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, cancellationToken)).ConfigureAwait(false);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        UdpReceiveResult receivedResult = await receiveTask.ConfigureAwait(false);
                        byte[] receivedData = receivedResult.Buffer;

                        if (receivedData.Length > headerSize)
                        {
                            await fileStream.WriteAsync(receivedData, headerSize, receivedData.Length - headerSize, cancellationToken).ConfigureAwait(false);
                            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                            dataSaved = true;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }

            if (!dataSaved && File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return dataSaved;
        }

        private void ConnectToUdp(string hostname, int port)
        {
            if (!_isUdpConnected)
            {
                _udpProvider.Connect(hostname, port);
                _isUdpConnected = true;
            }
        }

        private string GetNakMessage(byte[] response)
        {
            if (response != null && response.Length > 2 && response[0] == 0x02 && response[1] == 0x00) // NAK response [02][00]
            {
                return "Received NAK: Control item not supported.";
            }

            return null;
        }

        private string GetSetControlItemError(byte[] response, string defaultMessage)
        {
            string nakMessage = GetNakMessage(response);
            string errorMessage = string.IsNullOrEmpty(nakMessage) ? defaultMessage : nakMessage;

            return errorMessage;
        }
    }
}
