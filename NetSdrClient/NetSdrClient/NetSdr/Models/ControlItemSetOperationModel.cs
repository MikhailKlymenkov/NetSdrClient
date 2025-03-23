namespace NetSdrApp.NetSdr.Models
{
    public class ControlItemSetOperationModel
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public byte[] RawResponse { get; set; }
    }
}
