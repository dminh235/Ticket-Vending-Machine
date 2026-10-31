namespace MetroTicketSystem.Models
{
    public static class StationNames
    {
        private static readonly Dictionary<string, string> ViToEn = new()
        {
            { "Bến Thành", "Ben Thanh" },
            { "Tân Mỹ", "Tan My" },
            { "Cần Giờ", "Can Gio" },
            { "Lê Thị Riêng", "Le Thi Rieng" },
            { "Sân bay Tân Sơn Nhất", "Tan Son Nhat Airport" },
            { "Suối Tiên", "Suoi Tien" },
            { "Sân bay Long Thành", "Long Thanh Airport" }
        };

        // Tên ga lưu trong DB là tiếng Việt -> đổi sang tiếng Anh khi cần
        public static string Localize(string viName, bool vi)
        {
            if (vi) return viName;
            return ViToEn.TryGetValue(viName, out var en) ? en : viName;
        }

        public static string FromKey(string key, bool vi)
        {
            if (!vi) return key;
            foreach (var pair in ViToEn)
            {
                if (pair.Value == key) return pair.Key;
            }
            return key;
        }
    }
}
