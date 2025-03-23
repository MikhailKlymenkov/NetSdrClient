using System.Buffers;
using System.Net.Sockets;

namespace NetSdrApp.Network
{
    public class TcpProvider : ITcpProvider
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private bool _disposed;

        private const int TcpBufferSize = 512;

        public async Task ConnectAsync(string host, int port)
        {
            _tcpClient = new TcpClient();

            await _tcpClient.ConnectAsync(host, port).ConfigureAwait(false);

            _networkStream = _tcpClient.GetStream();
        }

        public async Task SendAsync(byte[] buffer)
        {
            if (_networkStream == null)
            {
                throw new InvalidOperationException("Network stream is not initialized.");
            }

            await _networkStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
        }

        public async Task<byte[]> ReceiveAsync()
        {
            if (_networkStream == null)
            {
                throw new InvalidOperationException("Network stream is not initialized.");
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(TcpBufferSize);

            try
            {
                int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                return buffer.Take(bytesRead).ToArray();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_networkStream != null)
                {
                    await _networkStream.DisposeAsync();
                }

                _tcpClient?.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _networkStream?.Dispose();
                _tcpClient?.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
