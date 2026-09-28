using SQLite;

namespace ShoppetApp.Models
{
    public class User
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string FullName { get; set; } = string.Empty;

        [Unique]
        public string Email { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        // RBAC Role: "Admin", "BusinessOwner", or "PetOwner"
        public string Role { get; set; } = "PetOwner";
        public string ProfilePicture { get; set; } = string.Empty;
    }
}