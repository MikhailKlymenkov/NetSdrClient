using System.Net.Sockets;

namespace NetSdrApp.Network
{
    public class UdpProvider : IUdpProvider
    {
        private UdpClient _udpClient;

        public void Connect(string host, int port)
        {
            _udpClient = new UdpClient(host, port);
        }

        public async Task<UdpReceiveResult> ReceiveAsync()
        {
            if (_udpClient == null)
            {
                throw new InvalidOperationException("Client is not initialized.");
            }

            return await _udpClient.ReceiveAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            _udpClient?.Dispose();
        }
    }
}
