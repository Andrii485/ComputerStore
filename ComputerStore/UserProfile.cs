namespace ElmirClone
{
    public class UserProfile1
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; } // Добавляем поле для фамилии
        public string MiddleName { get; set; }
        public string Phone { get; set; }
        public object UserId { get; internal set; }
    }
}