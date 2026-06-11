namespace Stokendra.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "user";
    
    public bool CanEdit => !Username.Equals("admin", System.StringComparison.OrdinalIgnoreCase);
}
