namespace ElmirClone.Models
{
    public class UserProfile1
    {
        public int? UserId { get; set; }
        public string Username { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public decimal Balance { get; set; }
        public bool IsSeller { get; set; } // Добавлено для совместимости с существующим кодом

        public UserProfile1()
        {
            UserId = -1;
            Balance = 0;
            IsSeller = false; // Значение по умолчанию
        }
    }
}