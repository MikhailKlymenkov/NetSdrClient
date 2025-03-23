using System.Net.Sockets;

namespace NetSdrApp.Network
{
    public interface IUdpProvider : IDisposable
    {
        void Connect(string host, int port);
        Task<UdpReceiveResult> ReceiveAsync();
    }
}
