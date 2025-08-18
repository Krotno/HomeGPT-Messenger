using System;
using System.Collections.Generic;

namespace HomeGPT_Messenger.Models
{
    public enum ChatKid { Text, Image}
    public class Chat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public ChatKid Kind { get; set; } = ChatKid.Text;
        public List<Message> Messages { get; set; } = new List<Message>();
        public bool IsFrozen { get; set; }


        public string? ModelName { get; set; }//Запоминаем модель LLM в чате
        public string? SystemPromt { get; set; } = "";

        public int SdSteps { get; set; } = 4;
        public int SdWidth { get; set; } = 512;
        public int SdHeight { get; set; } = 512;
        public string? SdNegative { get; set; }
    }
}
