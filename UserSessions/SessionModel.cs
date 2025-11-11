public class UserSession
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SourceAddress { get; set; } = "0.0.0.0";
    public int SessionCode { get; set; }

}