using HomeGPT_Messenger.Models;
using System.Text.Json;

namespace HomeGPT_Messenger.Services
{
    internal class ChatStorageService
    {
        private static string FilePath => Path.Combine(FileSystem.AppDataDirectory, "chats.json");

        public static async Task<List<Chat>> LoadChatsAsync()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new List<Chat>();

                var json = await File.ReadAllTextAsync(FilePath);
                var chats = JsonSerializer.Deserialize<List<Chat>>(json);
                return chats ?? new List<Chat>();
            }
            catch
            {
                return new List<Chat>();
            }
        }

        public static async Task SaveChatsAsync(List<Chat> chats)
        {
            var json = JsonSerializer.Serialize(chats, new JsonSerializerOptions {WriteIndented = true});
            await File.WriteAllTextAsync(FilePath, json);
        }
    }
}
