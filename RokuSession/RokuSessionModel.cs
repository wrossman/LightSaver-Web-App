public class RokuSession
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SourceAddress { get; set; } = "0.0.0.0";
    public string SessionCode { get; set; } = "";
    public bool ReadyForTransfer { get; set; } = false;

}