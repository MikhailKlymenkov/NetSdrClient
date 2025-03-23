namespace NetSdrApp.Network
{
    public interface ITcpProvider : IAsyncDisposable, IDisposable
    {
        Task ConnectAsync(string host, int port);
        Task SendAsync(byte[] buffer);
        Task<byte[]> ReceiveAsync();
    }
}
