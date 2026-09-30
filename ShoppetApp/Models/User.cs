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

        // Active roles: "Admin" or "Pet Owner". A Pet Owner may both buy and sell.
        public string Role { get; set; } = "Pet Owner";
        public string ProfilePicture { get; set; } = string.Empty;
    }
}