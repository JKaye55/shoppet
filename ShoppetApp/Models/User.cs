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

        // Final Scope v4.0 RBAC: active roles are "Pet Owner" and "Admin".
        // Seller/buyer are Pet Owner marketplace capabilities, not roles.
        public string Role { get; set; } = "Pet Owner";
        public string ProfilePicture { get; set; } = string.Empty;
    }
}