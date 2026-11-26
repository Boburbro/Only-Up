namespace OnlyUp
{
    /// <summary>
    /// Harakatlanuvchi platformalar vaqti. Yolg'iz o'yinda har bir platforma o'z vaqtini sanaydi;
    /// ko'p o'yinchili o'yinda hammasi xona egasining soatiga qaraydi, shunda ular bir joyda ko'rinadi.
    /// </summary>
    public static class WorldClock
    {
        public static System.Func<double> Shared;
    }
}
